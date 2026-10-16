using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElastiCache;
using Constructs;

namespace SyncBingo.Infra;

/// <summary>
/// ElastiCache for Valkey (ADR 0011), in the isolated subnets. One node for a demo (dev); a primary and a
/// replica in another zone, with automatic failover, for production. Encrypted in transit and at rest.
/// All game state is ephemeral and every key has a TTL (ADR 0003), so there are no backups.
/// </summary>
public sealed class DataStack : Stack
{
    public const int ValkeyPort = 6379;

    public DataStack(Construct scope, string id, IStackProps props, InfraSettings settings, NetworkStack network)
        : base(scope, id, props)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(network);

        var subnets = new CfnSubnetGroup(this, "Subnets", new CfnSubnetGroupProps
        {
            Description = "SyncBingo Valkey: the isolated subnets",
            SubnetIds = network.Vpc.SelectSubnets(new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED }).SubnetIds,
        });

        // docs/07 §6: when memory runs out, evict the keys closest to expiring (idle and ended games) first.
        var parameters = new CfnParameterGroup(this, "Parameters", new CfnParameterGroupProps
        {
            CacheParameterGroupFamily = "valkey8",
            Description = "SyncBingo: evict by TTL",
            Properties = new Dictionary<string, string> { ["maxmemory-policy"] = "volatile-ttl" },
        });

        var replicated = settings.IsProduction;
        Cache = new CfnReplicationGroup(this, "Valkey", new CfnReplicationGroupProps
        {
            ReplicationGroupDescription = $"SyncBingo {settings.Stage} game state",
            Engine = "valkey",
            EngineVersion = "8.1",
            CacheNodeType = "cache.t4g.micro",
            NumCacheClusters = replicated ? 2 : 1,
            AutomaticFailoverEnabled = replicated,
            MultiAzEnabled = replicated,
            CacheSubnetGroupName = subnets.Ref,
            CacheParameterGroupName = parameters.Ref,
            SecurityGroupIds = [network.CacheSecurityGroup.SecurityGroupId],
            Port = ValkeyPort,
            TransitEncryptionEnabled = true,
            AtRestEncryptionEnabled = true,
            SnapshotRetentionLimit = 0,
        });
    }

    public CfnReplicationGroup Cache { get; }

    /// <summary>The API's <c>Redis:ConnectionString</c> (StackExchange.Redis): the primary, over TLS.</summary>
    public string ConnectionString =>
        $"{Cache.AttrPrimaryEndPointAddress}:{Cache.AttrPrimaryEndPointPort},ssl=true,abortConnect=false";
}
