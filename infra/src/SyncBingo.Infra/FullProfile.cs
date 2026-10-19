using Amazon.CDK;

namespace SyncBingo.Infra;

/// <summary>
/// The Full profile's stacks (docs/07 §3) and the order they deploy in. The load balancer sits in the network
/// stack, so the CloudFront stack can point at it and the app stack can learn CloudFront's address for its
/// join links, without the two stacks needing each other.
/// </summary>
public static class FullProfile
{
    public static void Add(App app, InfraSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var pipeline = new PipelineRoleStack(
            app, settings.StackName("Pipeline"), settings.StackProps("the role GitHub Actions deploys with"), settings);
        var network = new NetworkStack(
            app, settings.StackName("Network"), settings.StackProps("VPC, security groups and the load balancer"), settings);
        var data = new DataStack(
            app, settings.StackName("Data"), settings.StackProps("ElastiCache for Valkey"), settings, network);
        var web = new WebStack(
            app, settings.StackName("Web"), settings.StackProps("S3 and CloudFront"), settings, network, pipeline);
        _ = new AppStack(
            app, settings.StackName("App"), settings.StackProps("the API on ECS Fargate, alarms and a dashboard"), settings, network, data, web);
    }
}
