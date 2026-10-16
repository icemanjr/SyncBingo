using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Constructs;

namespace SyncBingo.Infra;

/// <summary>
/// The VPC, its security groups and the load balancer in front of the API.
/// <list type="bullet">
/// <item>Two Availability Zones, each with a public subnet (the load balancer and the tasks) and an isolated one
/// (Valkey, which has no route to the internet at all).</item>
/// <item>No NAT gateway: one costs about $33 a month before any traffic, several times the rest of a demo. The
/// tasks run in the public subnets with a public address instead, to pull their image from GHCR, and their
/// security group lets in only the load balancer. With private subnets, use a NAT gateway, or ECR with VPC endpoints.</item>
/// <item>The load balancer accepts only CloudFront's origin-facing addresses, so nobody reaches the API around
/// CloudFront, and the client address CloudFront sends (docs/07 §1) can be trusted.</item>
/// </list>
/// </summary>
public sealed class NetworkStack : Stack
{
    /// <summary>
    /// The AWS-managed prefix list <c>com.amazonaws.global.cloudfront.origin-facing</c>, per Region.
    /// Its ID differs by Region; look it up with <c>aws ec2 describe-managed-prefix-lists</c>.
    /// </summary>
    private static readonly Dictionary<string, string> CloudFrontPrefixLists = new()
    {
        ["us-east-1"] = "pl-3b927c52",
        ["us-east-2"] = "pl-b6a144df",
        ["us-west-2"] = "pl-82a045eb",
    };

    public const int ApiPort = 8080;

    public NetworkStack(Construct scope, string id, IStackProps props, InfraSettings settings)
        : base(scope, id, props)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Vpc = new Vpc(this, "Vpc", new VpcProps
        {
            IpAddresses = IpAddresses.Cidr("10.40.0.0/16"),
            MaxAzs = 2,
            NatGateways = 0,
            SubnetConfiguration =
            [
                new SubnetConfiguration { Name = "public", SubnetType = SubnetType.PUBLIC, CidrMask = 24 },
                new SubnetConfiguration { Name = "data", SubnetType = SubnetType.PRIVATE_ISOLATED, CidrMask = 24 },
            ],
        });

        LoadBalancerSecurityGroup = new SecurityGroup(this, "LoadBalancerSg", new SecurityGroupProps
        {
            Vpc = Vpc,
            Description = "The load balancer: HTTP from CloudFront only",
            AllowAllOutbound = false,
        });
        if (!CloudFrontPrefixLists.TryGetValue(settings.Region, out var prefixList))
        {
            throw new ArgumentException($"No CloudFront prefix list known for {settings.Region}; add it to {nameof(CloudFrontPrefixLists)}.");
        }

        LoadBalancerSecurityGroup.AddIngressRule(Peer.PrefixList(prefixList), Port.Tcp(80), "CloudFront origin-facing");

        ApiSecurityGroup = new SecurityGroup(this, "ApiSg", new SecurityGroupProps
        {
            Vpc = Vpc,
            Description = "API tasks: from the load balancer only; out to GHCR and Valkey",
            AllowAllOutbound = true,
        });
        ApiSecurityGroup.AddIngressRule(LoadBalancerSecurityGroup, Port.Tcp(ApiPort), "The load balancer");
        LoadBalancerSecurityGroup.AddEgressRule(ApiSecurityGroup, Port.Tcp(ApiPort), "To the API tasks");

        CacheSecurityGroup = new SecurityGroup(this, "CacheSg", new SecurityGroupProps
        {
            Vpc = Vpc,
            Description = "Valkey: from the API tasks only",
            AllowAllOutbound = false,
        });
        CacheSecurityGroup.AddIngressRule(ApiSecurityGroup, Port.Tcp(DataStack.ValkeyPort), "The API tasks");

        LoadBalancer = new ApplicationLoadBalancer(this, "Alb", new ApplicationLoadBalancerProps
        {
            Vpc = Vpc,
            InternetFacing = true,
            SecurityGroup = LoadBalancerSecurityGroup,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },

            // Above SignalR's 15 s keep-alive with room to spare, so a quiet game's WebSockets stay open.
            IdleTimeout = Duration.Seconds(120),
            DropInvalidHeaderFields = true,
        });

        // Anything the app stack doesn't route answers 404.
        Listener = LoadBalancer.AddListener("Http", new BaseApplicationListenerProps
        {
            Port = 80,
            Open = false,
            DefaultAction = ListenerAction.FixedResponse(404, new FixedResponseOptions { ContentType = "text/plain", MessageBody = "Not found" }),
        });
    }

    public Vpc Vpc { get; }

    public SecurityGroup LoadBalancerSecurityGroup { get; }

    public SecurityGroup ApiSecurityGroup { get; }

    public SecurityGroup CacheSecurityGroup { get; }

    public ApplicationLoadBalancer LoadBalancer { get; }

    public ApplicationListener Listener { get; }
}
