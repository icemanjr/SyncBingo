using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace SyncBingo.Infra;

/// <summary>
/// The role GitHub Actions deploys with, through OIDC (docs/07 §4): no long-lived AWS keys in GitHub.
/// <list type="bullet">
/// <item>Only this repository's workflows can take it, and only on <c>master</c> or a deployment to the stage's
/// environment (GitHub's <c>dev</c> or <c>prod</c>, where prod can require an approval).</item>
/// <item>It can do little itself: it assumes the roles <c>cdk bootstrap</c> made (they deploy the stacks), and the
/// web stack lets it upload the app and invalidate CloudFront.</item>
/// </list>
/// An account has one GitHub OIDC provider; if another project made it, pass its ARN as <c>githubOidcProviderArn</c>.
/// </summary>
public sealed class PipelineRoleStack : Stack
{
    private const string GitHubIssuer = "token.actions.githubusercontent.com";

    /// <summary>The roles <c>cdk bootstrap</c> makes, by their tag.</summary>
    private static readonly string[] BootstrapRoles = ["deploy", "file-publishing", "image-publishing", "lookup"];

    public PipelineRoleStack(Construct scope, string id, IStackProps props, InfraSettings settings)
        : base(scope, id, props)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var existing = Node.TryGetContext("githubOidcProviderArn") as string;
        IOpenIdConnectProvider provider = string.IsNullOrEmpty(existing)
            ? new OpenIdConnectProvider(this, "GitHub", new OpenIdConnectProviderProps
            {
                Url = $"https://{GitHubIssuer}",
                ClientIds = ["sts.amazonaws.com"],
            })
            : OpenIdConnectProvider.FromOpenIdConnectProviderArn(this, "GitHub", existing);

        var repo = settings.GitHubRepository;
        Role = new Role(this, "Deploy", new RoleProps
        {
            Description = $"GitHub Actions in {repo} deploys SyncBingo {settings.Stage}",
            MaxSessionDuration = Duration.Hours(1),
            AssumedBy = new WebIdentityPrincipal(provider.OpenIdConnectProviderArn, new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, object> { [$"{GitHubIssuer}:aud"] = "sts.amazonaws.com" },
                ["StringLike"] = new Dictionary<string, object>
                {
                    [$"{GitHubIssuer}:sub"] = new[]
                    {
                        $"repo:{repo}:ref:refs/heads/master",
                        $"repo:{repo}:environment:{settings.Stage}",
                    },
                },
            }),
        });

        Role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "UseTheCdkBootstrapRoles",
            Actions = ["sts:AssumeRole"],
            Resources = [$"arn:{Aws.PARTITION}:iam::{Aws.ACCOUNT_ID}:role/cdk-*"],
            Conditions = new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, object>
                {
                    ["iam:ResourceTag/aws-cdk:bootstrap-role"] = BootstrapRoles,
                },
            },
        }));

        _ = new CfnOutput(this, "RoleArn", new CfnOutputProps
        {
            Value = Role.RoleArn,
            Description = "aws-actions/configure-aws-credentials: role-to-assume",
        });
    }

    public Role Role { get; }
}
