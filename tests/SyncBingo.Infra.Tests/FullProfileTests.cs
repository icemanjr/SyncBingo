using Amazon.CDK.Assertions;

namespace SyncBingo.Infra.Tests;

/// <summary>The Full profile's design decisions (docs/07 §2–3), read off the synthesized templates.</summary>
public sealed class FullProfileTests
{
    private static readonly IReadOnlyDictionary<string, Template> Stacks = Synth.Full();

    [Fact]
    public void Five_stacks_deploy_the_design() =>
        Stacks.Keys.Order().ShouldBe(["App", "Data", "Network", "Pipeline", "Web"]);

    [Fact]
    public void There_is_no_NAT_gateway() =>
        Stacks["Network"].ResourceCountIs("AWS::EC2::NatGateway", 0);

    [Fact]
    public void Only_CloudFront_reaches_the_load_balancer()
    {
        var ingress = Stacks["Network"].FindResources("AWS::EC2::SecurityGroupIngress");
        var fromPrefixLists = ingress.Values
            .Select(resource => (Dictionary<string, object>)((Dictionary<string, object>)resource)["Properties"])
            .Where(properties => properties.ContainsKey("SourcePrefixListId"))
            .ToList();

        fromPrefixLists.Count.ShouldBe(1);
        fromPrefixLists[0]["SourcePrefixListId"].ShouldBe("pl-b6a144df", "us-east-2's CloudFront origin-facing list");
        Stacks["Network"].HasResourceProperties("AWS::EC2::SecurityGroup", new Dictionary<string, object>
        {
            ["GroupDescription"] = "The load balancer: HTTP from CloudFront only",
            ["SecurityGroupIngress"] = Match.Absent(),
        });
    }

    [Fact]
    public void WebSockets_outlive_the_keep_alive() =>
        Stacks["Network"].HasResourceProperties("AWS::ElasticLoadBalancingV2::LoadBalancer", new Dictionary<string, object>
        {
            ["LoadBalancerAttributes"] = Match.ArrayWith([new Dictionary<string, object> { ["Key"] = "idle_timeout.timeout_seconds", ["Value"] = "120" }]),
        });

    [Fact]
    public void Valkey_is_encrypted_and_in_the_isolated_subnets()
    {
        Stacks["Data"].HasResourceProperties("AWS::ElastiCache::ReplicationGroup", new Dictionary<string, object>
        {
            ["Engine"] = "valkey",
            ["TransitEncryptionEnabled"] = true,
            ["AtRestEncryptionEnabled"] = true,
            ["NumCacheClusters"] = 1,
        });
        Stacks["Data"].HasResourceProperties("AWS::ElastiCache::ParameterGroup", new Dictionary<string, object>
        {
            ["Properties"] = new Dictionary<string, object> { ["maxmemory-policy"] = "volatile-ttl" },
        });
    }

    [Fact]
    public void Production_replicates_Valkey_across_zones() =>
        Synth.Full(new() { ["stage"] = "prod" })["Data"].HasResourceProperties("AWS::ElastiCache::ReplicationGroup", new Dictionary<string, object>
        {
            ["NumCacheClusters"] = 2,
            ["AutomaticFailoverEnabled"] = true,
            ["MultiAZEnabled"] = true,
        });

    [Fact]
    public void The_API_runs_on_arm64_and_trusts_CloudFronts_client_address()
    {
        var app = Stacks["App"];
        app.HasResourceProperties("AWS::ECS::TaskDefinition", new Dictionary<string, object>
        {
            ["RuntimePlatform"] = new Dictionary<string, object> { ["CpuArchitecture"] = "ARM64" },
            ["ContainerDefinitions"] = Match.ArrayWith([Match.ObjectLike(new Dictionary<string, object>
            {
                ["Image"] = "ghcr.io/icemanjr/syncbingo-api:latest",
                ["ReadonlyRootFilesystem"] = true,
                ["Environment"] = Match.ArrayWith([
                    new Dictionary<string, object> { ["Name"] = "ForwardedHeaders__ClientAddressHeader", ["Value"] = "CloudFront-Viewer-Address" },
                ]),
            })]),
        });
        app.HasResourceProperties("AWS::ECS::Service", new Dictionary<string, object> { ["DesiredCount"] = 2 });
    }

    [Fact]
    public void An_image_tag_pins_a_build() =>
        Synth.Full(new() { ["imageTag"] = "sha-abc1234" })["App"].HasResourceProperties("AWS::ECS::TaskDefinition", new Dictionary<string, object>
        {
            ["ContainerDefinitions"] = Match.ArrayWith([Match.ObjectLike(new Dictionary<string, object>
            {
                ["Image"] = "ghcr.io/icemanjr/syncbingo-api:sha-abc1234",
            })]),
        });

    [Fact]
    public void The_load_balancer_sends_only_the_API_paths_to_the_tasks() =>
        Stacks["App"].HasResourceProperties("AWS::ElasticLoadBalancingV2::ListenerRule", new Dictionary<string, object>
        {
            ["Conditions"] = Match.ArrayWith([Match.ObjectLike(new Dictionary<string, object>
            {
                ["Field"] = "path-pattern",
                ["PathPatternConfig"] = new Dictionary<string, object> { ["Values"] = new[] { "/api/*", "/hubs/*" } },
            })]),
        });

    [Fact]
    public void Alarms_cover_health_errors_tasks_and_Valkey()
    {
        Stacks["App"].ResourceCountIs("AWS::CloudWatch::Alarm", 5);
        Stacks["App"].ResourceCountIs("AWS::SNS::Subscription", 0);
        Synth.Full(new() { ["alarmEmail"] = "ops@example.com" })["App"].HasResourceProperties("AWS::SNS::Subscription", new Dictionary<string, object>
        {
            ["Protocol"] = "email",
            ["Endpoint"] = "ops@example.com",
        });
    }

    [Fact]
    public void CloudFront_sends_the_API_paths_to_the_load_balancer_uncached()
    {
        var behaviors = Stacks["Web"].FindResources("AWS::CloudFront::Distribution").Values.Single();
        var json = System.Text.Json.JsonSerializer.Serialize(behaviors);

        json.ShouldContain("\"PathPattern\":\"/api/*\"");
        json.ShouldContain("\"PathPattern\":\"/hubs/*\"");
        json.ShouldContain("4135ea2d-6df8-44a3-9df3-4b5a84be39ad", customMessage: "the managed CachingDisabled policy");
        json.ShouldContain("33f36d7e-f396-46d9-90e0-52428a34d9dc", customMessage: "the managed AllViewerAndCloudFrontHeaders-2022-06 policy");
    }

    [Fact]
    public void The_bucket_is_private() =>
        Stacks["Web"].HasResourceProperties("AWS::S3::Bucket", new Dictionary<string, object>
        {
            ["PublicAccessBlockConfiguration"] = new Dictionary<string, object>
            {
                ["BlockPublicAcls"] = true,
                ["BlockPublicPolicy"] = true,
                ["IgnorePublicAcls"] = true,
                ["RestrictPublicBuckets"] = true,
            },
        });

    [Fact]
    public void Only_this_repository_can_take_the_deploy_role() =>
        Stacks["Pipeline"].HasResourceProperties("AWS::IAM::Role", new Dictionary<string, object>
        {
            ["AssumeRolePolicyDocument"] = Match.ObjectLike(new Dictionary<string, object>
            {
                ["Statement"] = Match.ArrayWith([Match.ObjectLike(new Dictionary<string, object>
                {
                    ["Action"] = "sts:AssumeRoleWithWebIdentity",
                    ["Condition"] = new Dictionary<string, object>
                    {
                        ["StringEquals"] = new Dictionary<string, object> { ["token.actions.githubusercontent.com:aud"] = "sts.amazonaws.com" },
                        ["StringLike"] = new Dictionary<string, object>
                        {
                            ["token.actions.githubusercontent.com:sub"] = new[]
                            {
                                "repo:icemanjr/SyncBingo:ref:refs/heads/master",
                                "repo:icemanjr/SyncBingo:environment:dev",
                            },
                        },
                    },
                })]),
            }),
        });

    [Fact]
    public void A_domain_needs_its_certificate() =>
        Should.Throw<ArgumentException>(() => Synth.Full(new() { ["domain"] = "full.syncbingo.com" }));

    [Fact]
    public void A_custom_domain_is_served_with_its_certificate() =>
        Synth.Full(new()
        {
            ["domain"] = "full.syncbingo.com",
            ["certificateArn"] = "arn:aws:acm:us-east-1:111111111111:certificate/example",
        })["Web"].HasResourceProperties("AWS::CloudFront::Distribution", new Dictionary<string, object>
        {
            ["DistributionConfig"] = Match.ObjectLike(new Dictionary<string, object>
            {
                ["Aliases"] = new[] { "full.syncbingo.com" },
            }),
        });
}
