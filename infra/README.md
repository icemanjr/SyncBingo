# SyncBingo infrastructure (AWS CDK, C#)

The AWS design as code: [docs/07 §2–3](../docs/07-infrastructure.md) explains it, and [docs/10](../docs/10-cost-and-hosting.md) prices it and lists the teardown checks.

| Profile | Stacks | What it is |
|---------|--------|------------|
| `full` | `SyncBingo-<stage>-Pipeline`, `-Network`, `-Data`, `-Web`, `-App` | The portfolio design: Fargate (arm64) behind an ALB, ElastiCache for Valkey, S3 + CloudFront, alarms and a dashboard. About $61 a month in dev |
| `lean` | `SyncBingo-<stage>-Lean` | The live site's shape: one $7 Lightsail instance running `docker-compose.lean.yml` |

## Synthesize (creates nothing)

```bash
cd infra
npx aws-cdk synth -c profile=full -c stage=dev
npx aws-cdk synth -c profile=lean
```

CI runs these with `--strict` (any warning fails), and `tests/SyncBingo.Infra.Tests` checks the templates.

## Context

| Key | Default | Meaning |
|-----|---------|---------|
| `profile` | `full` | `full` or `lean` |
| `stage` | `dev` | `dev` or `prod`; prod replicates Valkey, scales to 6 tasks and keeps the bucket |
| `imageTag` | `latest` | The API image in GHCR, say `sha-<commit>` |
| `domain`, `certificateArn` | none | A custom domain and its us-east-1 ACM certificate, together; otherwise CloudFront's own address |
| `alarmEmail` | none | Emails the alarms (confirm the subscription email) |
| `sshCidr` | none | Lean only: the one address SSH is open to, besides Lightsail's browser console |
| `githubOidcProviderArn` | none | Reuse an account's existing GitHub OIDC provider |

## Deploy (the one-time Full deploy, by the maintainer)

All in the project's Region, us-east-2. Signed in with `aws login`:

```bash
npx aws-cdk bootstrap                                  # once per account and Region
npx aws-cdk deploy -c profile=full --all -c alarmEmail=you@example.com
# then upload the app: build web/, aws s3 sync web/dist s3://<BucketName> --delete,
# and aws cloudfront create-invalidation --distribution-id <DistributionId> --paths /index.html
npx aws-cdk destroy -c profile=full --all             # and run the checks in docs/10 §6
```
