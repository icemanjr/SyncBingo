using Amazon.CDK;
using Amazon.CDK.AWS.Lightsail;
using Constructs;

namespace SyncBingo.Infra;

/// <summary>
/// The Lean profile (ADR 0011, docs/07 §1a): one Lightsail instance running <c>docker-compose.lean.yml</c>,
/// with a static IP, the same firewall as the live server, and <c>deploy/setup.sh</c> as its launch script.
/// Lightsail has no higher-level CDK constructs, so these are the CloudFormation resources themselves.
/// <para>
/// The live server (<c>syncbingo-1</c>) was made by hand with the CLI before this stack existed, and this stack
/// does not manage it: deploying it makes a second server, <c>syncbingo-&lt;stage&gt;</c>. It is the record of how
/// the live one is set up, and how to rebuild it.
/// </para>
/// </summary>
public sealed class LeanStack : Stack
{
    /// <summary>1 GB, 2 vCPUs, 40 GB, 2 TB transfer: $7 a month (docs/10 §3).</summary>
    public const string Bundle = "micro_3_0";

    public const string Blueprint = "ubuntu_24_04";

    public LeanStack(Construct scope, string id, IStackProps props, InfraSettings settings)
        : base(scope, id, props)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // SSH only from the maintainer (-c sshCidr=203.0.113.4/32) and Lightsail's browser console.
        var sshCidr = Node.TryGetContext("sshCidr") as string;
        var name = $"syncbingo-{settings.Stage}";

        var instance = new CfnInstance(this, "Server", new CfnInstanceProps
        {
            InstanceName = name,
            BlueprintId = Blueprint,
            BundleId = Bundle,
            AvailabilityZone = $"{settings.Region}a",
            UserData = File.ReadAllText(SetupScript()),
            Networking = new CfnInstance.NetworkingProperty
            {
                Ports = new[]
                {
                    Open(80, "tcp"),
                    Open(443, "tcp"),
                    Open(443, "udp"), // HTTP/3
                    new CfnInstance.PortProperty
                    {
                        FromPort = 22,
                        ToPort = 22,
                        Protocol = "tcp",
                        Cidrs = string.IsNullOrEmpty(sshCidr) ? [] : [sshCidr],
                        CidrListAliases = ["lightsail-connect"],
                    },
                },
            },
        });

        var address = new CfnStaticIp(this, "Address", new CfnStaticIpProps
        {
            StaticIpName = $"{name}-ip",
            AttachedTo = instance.InstanceName,
        });
        address.AddResourceDependency(instance);

        _ = new CfnOutput(this, "PublicAddress", new CfnOutputProps
        {
            Value = address.AttrIpAddress,
            Description = "Point the domain's A records here (Cloudflare, DNS only), then run sudo /opt/syncbingo/update.sh",
        });
    }

    private static CfnInstance.PortProperty Open(int port, string protocol) => new()
    {
        FromPort = port,
        ToPort = port,
        Protocol = protocol,
        Cidrs = ["0.0.0.0/0"],
        Ipv6Cidrs = ["::/0"],
    };

    /// <summary><c>deploy/setup.sh</c>, found from wherever <c>cdk</c> runs inside the repository.</summary>
    private static string SetupScript()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "deploy", "setup.sh");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("deploy/setup.sh not found above the current directory.");
    }
}
