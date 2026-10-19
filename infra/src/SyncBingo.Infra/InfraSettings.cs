using Amazon.CDK;

namespace SyncBingo.Infra;

public enum InfraProfile
{
    Lean,
    Full,
}

/// <summary>Everything the stacks take from CDK context (<c>-c key=value</c> or <c>cdk.json</c>).</summary>
public sealed record InfraSettings
{
    /// <summary>The project's Region: an AWS new-experience project can create resources only there (docs/10 §3).</summary>
    public const string DefaultRegion = "us-east-2";

    public required InfraProfile Profile { get; init; }

    /// <summary><c>dev</c> or <c>prod</c>: part of every stack's name, so both can live in one account.</summary>
    public required string Stage { get; init; }

    /// <summary>The API image tag in GHCR: <c>latest</c>, or <c>sha-&lt;commit&gt;</c> to pin one build.</summary>
    public string ImageTag { get; init; } = "latest";

    /// <summary>
    /// A custom domain for the Full profile (say <c>full.syncbingo.com</c>), with an ACM certificate for it in
    /// us-east-1, where CloudFront needs it. Without both, the site is served on CloudFront's own domain.
    /// </summary>
    public string? Domain { get; init; }

    public string? CertificateArn { get; init; }

    /// <summary>Where alarms are emailed. Without it the alarms still exist and show in the console.</summary>
    public string? AlarmEmail { get; init; }

    /// <summary>The GitHub repository the CI role trusts, as <c>owner/name</c>.</summary>
    public string GitHubRepository { get; init; } = "icemanjr/SyncBingo";

    public string? Account { get; init; }

    public string Region { get; init; } = DefaultRegion;

    public bool IsProduction => Stage == "prod";

    public static InfraSettings From(App app)
    {
        ArgumentNullException.ThrowIfNull(app);

        string? Get(string key) => app.Node.TryGetContext(key) is string { Length: > 0 } value ? value : null;

        var profile = Get("profile") ?? "full";
        var stage = Get("stage") ?? "dev";
        if (stage is not ("dev" or "prod"))
        {
            throw new ArgumentException($"stage must be dev or prod, not '{stage}'.");
        }

        var settings = new InfraSettings
        {
            Profile = profile switch
            {
                "lean" => InfraProfile.Lean,
                "full" => InfraProfile.Full,
                _ => throw new ArgumentException($"profile must be lean or full, not '{profile}'."),
            },
            Stage = stage,
            ImageTag = Get("imageTag") ?? "latest",
            Domain = Get("domain"),
            CertificateArn = Get("certificateArn"),
            AlarmEmail = Get("alarmEmail"),
            GitHubRepository = Get("githubRepository") ?? "icemanjr/SyncBingo",
            Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
            Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION") ?? DefaultRegion,
        };

        if ((settings.Domain is null) != (settings.CertificateArn is null))
        {
            throw new ArgumentException("domain and certificateArn go together: give both, or neither.");
        }

        return settings;
    }

    public string StackName(string part) => $"SyncBingo-{Stage}-{part}";

    public StackProps StackProps(string description) => new()
    {
        Env = new Amazon.CDK.Environment { Account = Account, Region = Region },
        Description = $"SyncBingo ({Stage}): {description}",
        Tags = new Dictionary<string, string>
        {
            ["project"] = "syncbingo",
            ["stage"] = Stage,
            ["profile"] = Profile.ToString().ToLowerInvariant(),
        },
    };
}
