using Amazon.CDK;
using SyncBingo.Infra;

// The AWS design in code (docs/07 §2–3, ADR 0011). Two profiles from CDK context:
//   cdk synth -c profile=full -c stage=dev     the portfolio design: Fargate, ALB, ElastiCache, S3 + CloudFront
//   cdk synth -c profile=lean                  the live site: one Lightsail instance
// Synthesizing creates nothing; only `cdk deploy` does (docs/10 §6 has the teardown checklist).
var app = new App();
var settings = InfraSettings.From(app);

switch (settings.Profile)
{
    case InfraProfile.Lean:
        _ = new LeanStack(app, settings.StackName("Lean"), settings.StackProps("The live site: one Lightsail instance"), settings);
        break;
    case InfraProfile.Full:
        FullProfile.Add(app, settings);
        break;
}

app.Synth();
