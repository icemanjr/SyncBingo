using System.Text.RegularExpressions;
using Amazon.CDK.Assertions;

namespace SyncBingo.Infra.Tests;

/// <summary>
/// CloudFront serves the web app's pre-built content pages and the app shell for every other route (docs/07),
/// so its function must know every page the build pre-renders (<c>web/src/pages.ts</c>).
/// </summary>
public sealed partial class PrebuiltPagesTests
{
    [Fact]
    public void CloudFront_serves_every_pre_built_page_and_the_app_shell_for_the_rest()
    {
        var web = Synth.Full()["Web"];
        var code = web.FindResources("AWS::CloudFront::Function").Values
            .Select(resource => ((Dictionary<string, object>)((Dictionary<string, object>)resource)["Properties"])["FunctionCode"])
            .OfType<string>()
            .Single();

        foreach (var page in PagesTheWebAppPrebuilds().Where(page => page != "/"))
        {
            code.ShouldContain($"'{page}'", customMessage: $"{page} is pre-built by the web app");
        }

        code.ShouldContain("/app.html");
    }

    private static IEnumerable<string> PagesTheWebAppPrebuilds()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "SyncBingo.slnx")))
        {
            dir = dir.Parent;
        }

        var pages = File.ReadAllText(Path.Combine(dir.FullName, "web", "src", "pages.ts"));
        return [.. PathPattern().Matches(pages).Select(match => match.Groups[1].Value)];
    }

    [GeneratedRegex("path: '([^']+)'")]
    private static partial Regex PathPattern();
}
