using Amazon.CDK;
using Amazon.CDK.Assertions;

namespace SyncBingo.Infra.Tests;

/// <summary>Synthesizes a profile in memory (no AWS calls) and hands out its stacks' templates.</summary>
internal static class Synth
{
    public static IReadOnlyDictionary<string, Template> Full(Dictionary<string, object>? context = null) =>
        Run(new Dictionary<string, object> { ["profile"] = "full", ["stage"] = "dev" }, context, FullProfile.Add);

    public static Template Lean(Dictionary<string, object>? context = null) =>
        Run(new Dictionary<string, object> { ["profile"] = "lean", ["stage"] = "dev" }, context, (app, settings) =>
            _ = new LeanStack(app, settings.StackName("Lean"), settings.StackProps("test"), settings))["Lean"];

    private static Dictionary<string, Template> Run(
        Dictionary<string, object> defaults, Dictionary<string, object>? context, Action<App, InfraSettings> add)
    {
        foreach (var (key, value) in context ?? [])
        {
            defaults[key] = value;
        }

        var app = new App(new AppProps { Context = defaults });
        add(app, InfraSettings.From(app));
        return app.Node.Children.OfType<Stack>().ToDictionary(stack => stack.StackName.Split('-')[^1], stack => Template.FromStack(stack));
    }
}
