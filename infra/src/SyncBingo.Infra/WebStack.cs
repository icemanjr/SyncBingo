using Amazon.CDK;
using Amazon.CDK.AWS.CertificateManager;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace SyncBingo.Infra;

/// <summary>
/// The web app on S3 behind CloudFront, and CloudFront's routes to the API (docs/07 §2):
/// <list type="bullet">
/// <item>default: the private bucket, read through origin access control. Paths without a file extension are
/// pages: the content pages are pre-built (<c>/faq</c> is <c>/faq/index.html</c>), and the app's own routes
/// (<c>/join/…</c>, <c>/host/…</c>) load the empty shell, <c>app.html</c>; a CloudFront Function picks which.
/// That is done per request, not with error pages, which would turn the API's own 404s into the app's page.</item>
/// <item><c>/api/*</c> and <c>/hubs/*</c>: the load balancer, uncached, every method, with the viewer's headers
/// and CloudFront's own (<c>CloudFront-Viewer-Address</c> among them, for the rate limits). WebSockets pass through.</item>
/// </list>
/// <c>full-up.sh</c> uploads the built app with <c>aws s3 sync</c>, the pages uncached, then invalidates them;
/// hashed assets never change.
/// </summary>
public sealed class WebStack : Stack
{
    /// <summary>The pre-built content pages, as <c>web/src/pages.ts</c> lists them (the infra tests check they agree).</summary>
    internal static readonly string[] PrebuiltPages = ["/how-to-play", "/faq", "/sets", "/terms", "/privacy"];

    private static readonly string SpaRoutes = $$"""
        function handler(event) {
          var request = event.request;
          var uri = request.uri.replace(/\/$/, '');
          var last = uri.split('/').pop();
          if (uri === '') {
            request.uri = '/index.html';
          } else if ([{{string.Join(", ", PrebuiltPages.Select(page => $"'{page}'"))}}].indexOf(uri) !== -1) {
            request.uri = uri + '/index.html';
          } else if (last.indexOf('.') === -1) {
            request.uri = '/app.html';
          }
          return request;
        }
        """;

    public WebStack(Construct scope, string id, IStackProps props, InfraSettings settings, NetworkStack network, PipelineRoleStack pipeline)
        : base(scope, id, props)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(pipeline);

        Domain = settings.Domain;
        Bucket = new Bucket(this, "Site", new BucketProps
        {
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            EnforceSSL = true,

            // Only the build's output is in it, and CI can upload it again: a dev teardown removes it all.
            RemovalPolicy = settings.IsProduction ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY,
            AutoDeleteObjects = !settings.IsProduction,
        });

        var spaRoutes = new Function(this, "SpaRoutes", new FunctionProps
        {
            Code = FunctionCode.FromInline(SpaRoutes),
            Runtime = FunctionRuntime.JS_2_0,
            Comment = "Pre-built pages, or the app shell for app routes",
        });

        var api = new BehaviorOptions
        {
            Origin = new LoadBalancerV2Origin(network.LoadBalancer, new LoadBalancerV2OriginProps
            {
                ProtocolPolicy = OriginProtocolPolicy.HTTP_ONLY,
                ReadTimeout = Duration.Seconds(30),
                KeepaliveTimeout = Duration.Seconds(60),
            }),
            ViewerProtocolPolicy = ViewerProtocolPolicy.HTTPS_ONLY,
            AllowedMethods = AllowedMethods.ALLOW_ALL,
            CachePolicy = CachePolicy.CACHING_DISABLED,
            OriginRequestPolicy = OriginRequestPolicy.ALL_VIEWER_AND_CLOUDFRONT_2022,
            Compress = false,
        };

        Distribution = new Distribution(this, "Cdn", new DistributionProps
        {
            Comment = $"SyncBingo {settings.Stage}",
            DefaultRootObject = "index.html",
            HttpVersion = HttpVersion.HTTP2_AND_3,
            PriceClass = PriceClass.PRICE_CLASS_100,
            // CloudFront's own domain has a fixed TLS policy; a custom domain's certificate can require TLS 1.2.
            MinimumProtocolVersion = settings.CertificateArn is null ? null : SecurityPolicyProtocol.TLS_V1_2_2021,
            DomainNames = settings.Domain is null ? null : [settings.Domain],
            Certificate = settings.CertificateArn is null ? null : Certificate.FromCertificateArn(this, "Certificate", settings.CertificateArn),
            DefaultBehavior = new BehaviorOptions
            {
                Origin = S3BucketOrigin.WithOriginAccessControl(Bucket),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                CachePolicy = CachePolicy.CACHING_OPTIMIZED,
                ResponseHeadersPolicy = ResponseHeadersPolicy.SECURITY_HEADERS,
                FunctionAssociations =
                [
                    new FunctionAssociation { Function = spaRoutes, EventType = FunctionEventType.VIEWER_REQUEST },
                ],
            },
            AdditionalBehaviors = new Dictionary<string, IBehaviorOptions>
            {
                ["/api/*"] = api,
                ["/hubs/*"] = api,
            },
        });

        Bucket.GrantReadWrite(pipeline.Role);
        Bucket.GrantDelete(pipeline.Role);
        Distribution.GrantCreateInvalidation(pipeline.Role);

        _ = new CfnOutput(this, "SiteUrl", new CfnOutputProps { Value = PublicOrigin });
        _ = new CfnOutput(this, "BucketName", new CfnOutputProps { Value = Bucket.BucketName });
        _ = new CfnOutput(this, "DistributionId", new CfnOutputProps { Value = Distribution.DistributionId });
    }

    public Bucket Bucket { get; }

    public Distribution Distribution { get; }

    /// <summary>Where people open the site, for the API's join and display links.</summary>
    public string PublicOrigin => $"https://{Domain ?? Distribution.DistributionDomainName}";

    private string? Domain { get; }
}
