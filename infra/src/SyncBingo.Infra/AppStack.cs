using Amazon.CDK;
using Amazon.CDK.AWS.ApplicationAutoScaling;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.CloudWatch.Actions;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Constructs;
using HealthCheck = Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck;
using Protocol = Amazon.CDK.AWS.ECS.Protocol;

namespace SyncBingo.Infra;

/// <summary>
/// The API on ECS Fargate (docs/07 §2), with what operating it needs (§6):
/// <list type="bullet">
/// <item>arm64 (Graviton) tasks of 0.25 vCPU and 0.5 GB, the smallest Fargate size. The live box carries
/// 10 games of 30 players on a similar share of a CPU (docs/10 §5b).</item>
/// <item>At least two tasks, in two zones; more as CPU or requests rise. SignalR fans events out through
/// Valkey, so any task serves any game, and clients skip negotiation, so they need no sticky sessions.</item>
/// <item>The image comes from GHCR, where CI already publishes it for the live site; an ECR repository would
/// be a second copy to keep. Pin a build with <c>-c imageTag=sha-&lt;commit&gt;</c>.</item>
/// <item>Logs kept a week. Alarms on unhealthy targets, 5xx responses, no running task, and Valkey memory and evictions, to
/// email when <c>alarmEmail</c> is set; and a dashboard.</item>
/// </list>
/// </summary>
public sealed class AppStack : Stack
{
    public AppStack(
        Construct scope, string id, IStackProps props, InfraSettings settings, NetworkStack network, DataStack data, WebStack web)
        : base(scope, id, props)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(web);

        var cluster = new Cluster(this, "Cluster", new ClusterProps
        {
            Vpc = network.Vpc,
            ContainerInsightsV2 = ContainerInsights.DISABLED, // about $0.50 a task a month more; the alarms below cover a demo
        });

        var logs = new LogGroup(this, "Logs", new LogGroupProps
        {
            Retention = RetentionDays.ONE_WEEK,
            RemovalPolicy = RemovalPolicy.DESTROY,
        });

        var task = new FargateTaskDefinition(this, "Task", new FargateTaskDefinitionProps
        {
            Cpu = 256,
            MemoryLimitMiB = 512,
            RuntimePlatform = new RuntimePlatform
            {
                CpuArchitecture = CpuArchitecture.ARM64,
                OperatingSystemFamily = OperatingSystemFamily.LINUX,
            },
        });
        // A read-only root file system, with somewhere writable for .NET's temporary files.
        task.AddVolume(new Amazon.CDK.AWS.ECS.Volume { Name = "tmp" });
        var api = task.AddContainer("api", new ContainerDefinitionOptions
        {
            Image = ContainerImage.FromRegistry($"ghcr.io/icemanjr/syncbingo-api:{settings.ImageTag}"),
            PortMappings = [new PortMapping { ContainerPort = NetworkStack.ApiPort, Protocol = Protocol.TCP }],
            Logging = LogDriver.AwsLogs(new AwsLogDriverProps { LogGroup = logs, StreamPrefix = "api" }),
            ReadonlyRootFilesystem = true,
            Environment = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["Redis__ConnectionString"] = data.ConnectionString,
                ["Web__PublicOrigin"] = web.PublicOrigin,

                // Two proxies (CloudFront, then the load balancer): take the client's address from CloudFront.
                ["ForwardedHeaders__Enabled"] = "true",
                ["ForwardedHeaders__ClientAddressHeader"] = "CloudFront-Viewer-Address",
            },
        });

        api.AddMountPoints(new MountPoint { SourceVolume = "tmp", ContainerPath = "/tmp", ReadOnly = false });

        var service = new FargateService(this, "Api", new FargateServiceProps
        {
            Cluster = cluster,
            TaskDefinition = task,
            DesiredCount = 2,
            MinHealthyPercent = 100,
            MaxHealthyPercent = 200,
            AssignPublicIp = true, // no NAT gateway: see NetworkStack
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
            SecurityGroups = [network.ApiSecurityGroup],
            CircuitBreaker = new DeploymentCircuitBreaker { Enable = true, Rollback = true },
            HealthCheckGracePeriod = Duration.Seconds(30),
        });

        var targets = new ApplicationTargetGroup(this, "ApiTargets", new ApplicationTargetGroupProps
        {
            Vpc = network.Vpc,
            Port = NetworkStack.ApiPort,
            Protocol = ApplicationProtocol.HTTP,
            TargetType = TargetType.IP,
            Targets = [service],
            HealthCheck = new HealthCheck
            {
                Path = "/readyz", // the process is up and Valkey answers
                Interval = Duration.Seconds(15),
                HealthyThresholdCount = 2,
                UnhealthyThresholdCount = 3,
            },

            // Let open WebSockets finish briefly on a deploy; clients reconnect and resync (ADR 0002).
            DeregistrationDelay = Duration.Seconds(30),
        });

        // Created here, on the network stack's listener, so that stack never needs this one.
        _ = new ApplicationListenerRule(this, "ApiRoute", new ApplicationListenerRuleProps
        {
            Listener = network.Listener,
            Priority = 10,
            Conditions = [ListenerCondition.PathPatterns(["/api/*", "/hubs/*"])],
            Action = ListenerAction.Forward([targets]),
        });

        var scaling = service.AutoScaleTaskCount(new EnableScalingProps { MinCapacity = 2, MaxCapacity = settings.IsProduction ? 6 : 3 });
        scaling.ScaleOnCpuUtilization("Cpu", new CpuUtilizationScalingProps { TargetUtilizationPercent = 60 });
        scaling.ScaleOnRequestCount("Requests", new RequestCountScalingProps
        {
            TargetGroup = targets,
            RequestsPerTarget = 3000, // a minute's requests; the load test peaked near 1,500 for 300 players
        });

        AddAlarmsAndDashboard(settings, network, data, service, targets);
    }

    private void AddAlarmsAndDashboard(
        InfraSettings settings, NetworkStack network, DataStack data, FargateService service, ApplicationTargetGroup targets)
    {
        var topic = new Topic(this, "Alarms", new TopicProps { DisplayName = $"SyncBingo {settings.Stage} alarms" });
        if (settings.AlarmEmail is { } email)
        {
            topic.AddSubscription(new EmailSubscription(email));
        }

        var notify = new SnsAction(topic);
        var fiveMinutes = Duration.Minutes(5);

        var unhealthy = targets.Metrics.UnhealthyHostCount(new MetricOptions { Period = Duration.Minutes(1), Statistic = "Maximum" });
        var errors = targets.Metrics.HttpCodeTarget(HttpCodeTarget.TARGET_5XX_COUNT, new MetricOptions { Period = fiveMinutes });
        var requests = targets.Metrics.RequestCount(new MetricOptions { Period = fiveMinutes });
        var connections = network.LoadBalancer.Metrics.ActiveConnectionCount(new MetricOptions { Period = Duration.Minutes(1) });
        var cpu = service.MetricCpuUtilization(new MetricOptions { Period = Duration.Minutes(1) });
        var memory = service.MetricMemoryUtilization(new MetricOptions { Period = Duration.Minutes(1) });
        var valkeyMemory = new Metric(new MetricProps
        {
            Namespace = "AWS/ElastiCache",
            MetricName = "DatabaseMemoryUsagePercentage",
            DimensionsMap = new Dictionary<string, string> { ["CacheClusterId"] = $"{data.Cache.Ref}-001" },
            Period = fiveMinutes,
            Statistic = "Maximum",
        });
        var evictions = new Metric(new MetricProps
        {
            Namespace = "AWS/ElastiCache",
            MetricName = "Evictions",
            DimensionsMap = new Dictionary<string, string> { ["CacheClusterId"] = $"{data.Cache.Ref}-001" },
            Period = fiveMinutes,
            Statistic = "Sum",
        });

        Alarm Alarm(
            string id, string description, IMetric metric, double threshold, ComparisonOperator comparison,
            int periods = 1, TreatMissingData missing = TreatMissingData.NOT_BREACHING)
        {
            var alarm = new Alarm(this, id, new AlarmProps
            {
                AlarmDescription = description,
                Metric = metric,
                Threshold = threshold,
                ComparisonOperator = comparison,
                EvaluationPeriods = periods,
                TreatMissingData = missing,
            });
            alarm.AddAlarmAction(notify);
            alarm.AddOkAction(notify);
            return alarm;
        }

        _ = Alarm("UnhealthyTargets", "An API task is failing its health check", unhealthy, 0, ComparisonOperator.GREATER_THAN_THRESHOLD, periods: 3);
        _ = Alarm("ServerErrors", "The API answered 5xx more than 10 times in 5 minutes", errors, 10, ComparisonOperator.GREATER_THAN_THRESHOLD);
        _ = Alarm("ValkeyMemory", "Valkey is over 80% of its memory", valkeyMemory, 80, ComparisonOperator.GREATER_THAN_THRESHOLD);
        _ = Alarm("ValkeyEvictions", "Valkey evicted keys: games may have lost state", evictions, 0, ComparisonOperator.GREATER_THAN_THRESHOLD);

        // ECS reports a running-task count only with Container Insights; without it the service's CPU metric
        // has no data points while no task runs, so alarm on those going missing.
        _ = Alarm(
            "NoTasks", "No API task has reported for 5 minutes", cpu.With(new MetricOptions { Statistic = "SampleCount" }),
            1, ComparisonOperator.LESS_THAN_THRESHOLD, periods: 5, missing: TreatMissingData.BREACHING);

        var dashboard = new Dashboard(this, "Dashboard", new DashboardProps
        {
            DashboardName = $"SyncBingo-{settings.Stage}",
            DefaultInterval = Duration.Hours(3),
        });
        dashboard.AddWidgets(
            new GraphWidget(new GraphWidgetProps { Title = "Requests and 5xx", Left = [requests], Right = [errors], Width = 12 }),
            new GraphWidget(new GraphWidgetProps { Title = "Open connections (WebSockets)", Left = [connections], Width = 12 }));
        dashboard.AddWidgets(
            new GraphWidget(new GraphWidgetProps { Title = "API CPU and memory (%)", Left = [cpu, memory], Width = 12 }),
            new GraphWidget(new GraphWidgetProps { Title = "Healthy and unhealthy tasks", Left = [targets.Metrics.HealthyHostCount(), unhealthy], Width = 12 }));
        dashboard.AddWidgets(
            new GraphWidget(new GraphWidgetProps { Title = "Valkey memory (%)", Left = [valkeyMemory], Width = 12 }),
            new GraphWidget(new GraphWidgetProps { Title = "Valkey evictions", Left = [evictions], Width = 12 }));
    }
}
