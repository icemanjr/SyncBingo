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

## Deploy for a demo, then tear down (the maintainer)

Signed in with `aws login --region us-east-2`, from the repository root:

```bash
infra/scripts/full-up.sh                                  # asks first; ALARM_EMAIL=you@example.com to email alarms
infra/scripts/full-down.sh                                # the same day
```

- **`full-up.sh`:** bootstraps CDK if needed, deploys all five stacks, builds the web app with the Serious Bingo teaser, uploads it to the bucket (hashed assets cached for a year, `index.html` revalidated), invalidates `index.html`, then waits until the page and the API both answer through CloudFront. It prints the address and what to try.
- **`full-down.sh`:** says how long it was up and roughly what that cost, destroys the stacks, then checks that nothing billable is left (docs/10 §6). It keeps the `CDKToolkit` stack, which costs cents.
- About $0.08 an hour while up (docs/10 §5c). The live site is untouched throughout.
