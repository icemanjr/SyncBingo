using Amazon.CDK.Assertions;

namespace SyncBingo.Infra.Tests;

/// <summary>The Lean stack describes the live server (docs/07 §1a).</summary>
public sealed class LeanProfileTests
{
    private static readonly Template Lean = Synth.Lean();

    [Fact]
    public void One_seven_dollar_Ubuntu_instance_runs_the_setup_script()
    {
        var setup = File.ReadAllText(Path.Combine(RepoRoot(), "deploy", "setup.sh"));

        Lean.HasResourceProperties("AWS::Lightsail::Instance", new Dictionary<string, object>
        {
            ["BundleId"] = "micro_3_0",
            ["BlueprintId"] = "ubuntu_24_04",
            ["AvailabilityZone"] = "us-east-2a",
            ["UserData"] = setup,
        });
    }

    [Fact]
    public void Only_web_ports_are_open_to_everyone()
    {
        var instance = Lean.FindResources("AWS::Lightsail::Instance").Values.Single();
        var json = System.Text.Json.JsonSerializer.Serialize(instance);

        json.ShouldContain("\"FromPort\":80");
        json.ShouldContain("\"FromPort\":443");
        json.ShouldContain("\"CidrListAliases\":[\"lightsail-connect\"]", customMessage: "SSH: Lightsail's browser console");
        json.ShouldNotContain("\"FromPort\":22,\"ToPort\":22,\"Protocol\":\"tcp\",\"Cidrs\":[\"0.0.0.0/0\"]");
    }

    [Fact]
    public void SSH_can_be_opened_to_one_address() =>
        System.Text.Json.JsonSerializer.Serialize(Synth.Lean(new() { ["sshCidr"] = "203.0.113.4/32" }).ToJSON())
            .ShouldContain("203.0.113.4/32");

    [Fact]
    public void It_has_a_static_address() =>
        Lean.ResourceCountIs("AWS::Lightsail::StaticIp", 1);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "SyncBingo.slnx")))
        {
            dir = dir.Parent;
        }

        return dir.FullName;
    }
}
