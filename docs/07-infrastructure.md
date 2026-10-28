# 07 — Infrastructure

## 1. Local development and test (Docker)

`docker-compose.yml` defines three services:

| Service | Image | Notes |
|---------|-------|-------|
| `web` | multi-stage build: `node` → `nginx-unprivileged` serving the Vite build on port 8080 (planned dev profile: Vite dev server with hot reload) | SPA fallback to `index.html`; proxies `/api` and `/hubs` to `api`, including WebSocket upgrade. Host port 8080 |
| `api` | multi-stage build: .NET SDK → chiseled ASP.NET runtime (no shell, non-root) | Env: `Redis__ConnectionString`, `ForwardedHeaders__Enabled` (see below), later `Cors__Origins` and token/password settings. Host port 5080 for debugging |
| `valkey` | `valkey/valkey:8.1-alpine`: Redis-compatible (ADR 0011), the 8.x line ElastiCache offers. The API's settings keep the `Redis__` names, since they describe the protocol | No volume; data is disposable |

Commands: `docker compose up --build` (everything, then open http://localhost:8080), then `docker compose --profile tests run --rm tests` for the Playwright end-to-end suite (docs/08). The `tests` service runs in Playwright's own image (pinned to the `@playwright/test` version in `e2e/package.json`) on the host's network, so the browser uses http://localhost:8080 as a person would and the join and display links the API hands out work. Locally, game creation allows 1,000 an hour (the live site keeps 30), since every test creates a game. Planned: `docker compose --profile dev up` (hot reload).

nginx re-resolves `api` (a `resolver` filled in from the container's DNS servers at start-up), so restarting the API container doesn't leave every request failing with 502. The end-to-end tests found this. The .NET and web unit/integration suites run directly (locally and in CI); integration tests use **Testcontainers** for Redis, so they do not need Compose.

CI builds both images on every PR, for **linux/amd64 and linux/arm64** (ADR 0011), so a broken Dockerfile is caught before merge. Neither needs emulation: the build stages run on the build machine's own architecture (a framework-dependent .NET publish and a Vite build are the same for every CPU), and the runtime stages only copy files onto the target architecture's base image. Image names are fully qualified (`docker.io/…`), which Podman requires.

**Client addresses behind a proxy.** The rate limits key on the caller's address (docs/05 §5), but behind nginx or Caddy every request comes from the proxy. With `ForwardedHeaders__Enabled=true` the API takes the address the proxy appended to `X-Forwarded-For` (only the last one, so a client can't prepend a made-up address), and only when the request comes from loopback or a private network, where the proxy runs. A header from anywhere else is ignored. In the Full profile there are two proxies, CloudFront and then the ALB, so the client's address isn't the last one in `X-Forwarded-For`. There `ForwardedHeaders__ClientAddressHeader=CloudFront-Viewer-Address` takes it from the header CloudFront sets itself (`address:port`; a client's own copy is replaced), still only from a private address, the ALB's. The ALB accepts only CloudFront's origin-facing addresses, so nobody can go around CloudFront to send a made-up one.

Podman works in place of Docker: enable the user socket (`systemctl --user enable --now podman.socket`), set `DOCKER_HOST=unix://$XDG_RUNTIME_DIR/podman/podman.sock`, and use `podman compose` or `docker-compose`.

## 1a. Lean deployment (the live site, ADR 0011)

One small instance (AWS Lightsail `micro_3_0`, below) runs `docker-compose.lean.yml`: `SITE_DOMAIN=syncbingo.com docker compose -f docker-compose.lean.yml up -d`. Without `SITE_DOMAIN` it serves `https://localhost` with Caddy's own local certificate, to try the production setup on a laptop.

| Service | Role |
|---------|------|
| `caddy` | The `caddy` target of `web/Dockerfile`: the same SPA build with `web/Caddyfile`. Let's Encrypt TLS for `syncbingo.com` and a redirect from `www` (DNS on Cloudflare, docs/10 §2a), HTTP/2 and HTTP/3, zstd/gzip, the same caching as nginx (hashed assets for a year, pages `no-cache`), proxies `/api` and `/hubs` (WebSockets) to `api`. Certificates live on a volume, so rebuilds don't run into Let's Encrypt's rate limits |
| `api` | The same image as everywhere else, with no published port: only Caddy reaches it. `ForwardedHeaders__Enabled=true` (Caddy replaces any client-sent `X-Forwarded-For` with the real address); `Web__PublicOrigin` from `SITE_DOMAIN` for join and display links |
| `valkey` | Append-only file (`everysec`) on a volume and no RDB snapshots, so live games survive API restarts, deploys and reboots. `maxmemory 256mb` with `noeviction`: every key has a TTL, and if memory still fills up, new writes fail (new games are refused) rather than a live game being evicted. 256 MB is a first guess until bytes per game are measured (docs/10) |

All three restart unless stopped, and logs are capped at 3 × 10 MB per service for a small disk.

**Deploys: the server pulls, nightly.**
- After a merge to master, and only when every check passes, CI's `publish` job pushes both images to GitHub's container registry: `ghcr.io/icemanjr/syncbingo-api` and `ghcr.io/icemanjr/syncbingo-web-caddy`. Each is tagged `latest` and `sha-<commit>` and labelled with its commit. Both are for amd64 and arm64.
- At 04:00 server time, a systemd timer (`deploy/syncbingo-update.timer`) runs `deploy/update.sh` in `/opt/syncbingo`. The script:
  1. pulls `latest` and reads the commit from its label;
  2. stops there if that commit is already running;
  3. otherwise fetches that commit's `docker-compose.lean.yml` and pulls both images by their `sha-` tag, so the API, the web app and the Compose settings always come from the same commit;
  4. runs `up -d`, which restarts only what changed. Players reconnect and resync from the snapshot (ADR 0002).
- If any step fails, nothing restarts. For an urgent fix, run the same script by hand over SSH.
- GitHub needs no key or access to the server, and the server needs no registry credentials: the packages are public, like the repository.

**The server** (live since 2026-10-15):
- **Instance:** Lightsail `syncbingo-1`, the `micro_3_0` plan (1 GB, 2 vCPUs, 40 GB, 2 TB transfer, $7/month), Ubuntu 24.04 LTS, in `us-east-2a`. The project's Region is fixed at us-east-2 (docs/10 §3).
- **Address:** static IP `syncbingo-ip` (18.223.223.138), with Cloudflare `A` records for `syncbingo.com` and `www`, DNS only.
- **Firewall:** 80/tcp and 443/tcp+udp open to all; 22/tcp only from the maintainer's IP and Lightsail's browser SSH (`lightsail-connect`).
- **SSH:** the maintainer's own ed25519 key with a passphrase (`ssh-add -t 4h`); only the public half is uploaded. Host keys are pinned from `aws lightsail get-instance-access-details`, not trusted on first use.
- **Setup:** `deploy/setup.sh`, given as the instance's launch script, runs once as root. It:
  - sets the time zone to America/Denver, so the nightly update runs at 04:00 Mountain;
  - adds 1 GB of swap, with `vm.swappiness=10` and `vm.overcommit_memory=1` for Valkey;
  - installs Docker from Docker's repository;
  - installs `update.sh`, `.env` and the timer.
- **Lightsail quirk:** Lightsail puts its own lines in front of a launch script and runs the file with `/bin/sh`, so the script switches itself to bash.
- **Kept on purpose:** the `ubuntu` user is not in the `docker` group (that is root without a prompt), and Ubuntu's automatic security updates stay on but don't reboot, so a live game is never cut short. Reboot for kernel updates at a quiet time.
- **Starting the site:** once DNS pointed at the server, `sudo /opt/syncbingo/update.sh` started it, and Caddy got its certificates on the first try.
- **At idle:** the API used about 100 MB, Caddy 20 MB and Valkey 7 MB, with about 420 MB of memory still free and swap unused.

The packages are public with nothing to set: the images' `org.opencontainers.image.source` label links them to this public repository, and they take its visibility. Checked 2026-10-15 with an anonymous pull of both, on arm64. The first run of `update.sh` was tested the same day against the registry, on a laptop: it started both images by commit, and a second run found nothing to do.

**Cost controls** (docs/10 §4): a project spend limit of $30, and an AWS Budgets alert at $10.

## 2. AWS target (Full profile)

```mermaid
flowchart TB
  U["Users"] --> DNS["Cloudflare DNS"]
  DNS --> CF["CloudFront"]
  CF -- "static" --> S3["S3 (SPA)"]
  CF -- "/api/*, /hubs/*" --> ALB["ALB"]
  subgraph VPC
    ALB --> T1["Fargate task"]
    ALB --> T2["Fargate task"]
    T1 & T2 --> EC[("ElastiCache Redis")]
  end
  T1 & T2 --> CW["CloudWatch Logs/Metrics"]
```

| Concern | Choice |
|---------|--------|
| Compute | ECS on Fargate, ≥ 2 tasks across AZs, autoscaling on CPU and connection count |
| Real-time | WebSocket-only SignalR; ALB idle timeout raised above the heartbeat interval; Redis backplane |
| State | ElastiCache for **Valkey** (single node for demo, replicated for production) |
| Static site | S3 + CloudFront with SPA fallback to `index.html` |
| Edge routing | CloudFront behaviours: default → S3; `/api/*` and `/hubs/*` → ALB with caching disabled and WebSocket support |
| TLS and DNS | ACM certificates; DNS records in Cloudflare, where `syncbingo.com` is registered (no Route 53 zone needed; docs/10 §2a) |
| Secrets/config | SSM Parameter Store / Secrets Manager injected into tasks |
| Network | Valkey in isolated subnets (no route out). The tasks run in public subnets with a public address and no NAT gateway, which would cost about $33 a month on its own; their security group lets in only the ALB, and the ALB only CloudFront (the AWS-managed origin-facing prefix list). For production, private subnets with a NAT gateway, or ECR with VPC endpoints |
| Images | GHCR, where CI already publishes the arm64 API image for the live site; no ECR copy to keep. Pin a build with `-c imageTag=sha-<commit>` |
| Protection | Security groups scoped tightly. Edge rate rules (AWS WAF in front of CloudFront, or Cloudflare) only with **very high per-IP thresholds**, or none: a whole event of up to 100 people shares one WiFi address, and a busy game is thousands of requests from that address in a few minutes. The API's own limits (docs/05 §5) are keyed by player token where it matters |

## 3. AWS CDK (C#)

Stacks in `infra/`:

| Stack | Contents |
|-------|----------|
| `NetworkStack` | VPC (2 zones, public and isolated subnets, no NAT), security groups, the ALB and its listener |
| `DataStack` | ElastiCache for Valkey 8.1: subnet group, parameter group (`volatile-ttl`), replication group (1 node in dev; 2 with failover in prod), TLS and encryption at rest |
| `WebStack` | Private S3 bucket (origin access control), CloudFront distribution: a CloudFront Function serves `index.html` for app routes; `/api/*` and `/hubs/*` go to the ALB uncached with all viewer and CloudFront headers; optional custom domain and certificate |
| `AppStack` | ECS cluster, Fargate service (arm64, 2–3 tasks in dev, up to 6 in prod), target group on `/readyz`, the listener rule, autoscaling on CPU and requests, a log group, alarms to SNS and a dashboard |
| `PipelineRoleStack` | GitHub OIDC provider and a deploy role that only this repository's `master` or a stage environment can take; it assumes the CDK bootstrap roles, and can upload the app and invalidate CloudFront |
| `LeanStack` (profile `lean`) | The live server as code: a Lightsail `micro_3_0` instance with `deploy/setup.sh` as its launch script, its firewall and a static IP. The live `syncbingo-1` was made by hand before this existed and is not managed by it |

The ALB sits in the network stack so that CloudFront can point at it and the app stack can use CloudFront's address for join links, without the two stacks depending on each other.

Stage configuration (`dev`, `prod`) comes from CDK context so the same code deploys both. A second context flag, `profile=lean|full` (ADR 0011), selects the Lean instance or the full set of stacks; CI runs `cdk synth --strict` for Lean and for Full in both stages, and `tests/SyncBingo.Infra.Tests` checks the templates (CDK assertions). Optional context: `imageTag`, `domain` with `certificateArn`, `alarmEmail`, `sshCidr` (Lean), `githubOidcProviderArn`. See `infra/README.md`.

**Trade-offs worth knowing:**
- **CloudFront to the ALB is plain HTTP.** The ALB has no certificate without a domain of ours. For production, give the ALB an HTTPS listener (a us-east-2 certificate for an origin name) and set the origin to HTTPS only, or use a CloudFront VPC origin with an internal ALB, once WebSocket support for VPC origins is confirmed.
- **Cross-stack references are strong** (CloudFormation exports), so a producing stack can't drop an output another stack still uses.

## 4. CI/CD (GitHub Actions)

1. **PR:** restore, build, unit tests, integration tests (Testcontainers), web lint/typecheck/tests, `cdk synth`, container image build.
2. **Main:** build and push images to GHCR (done for the live site), `cdk diff`, deploy `dev` automatically (planned, once Full is deployed).
3. **Release tag:** manual approval, deploy `prod`; sync SPA to S3 and invalidate CloudFront.
4. AWS access via GitHub **OIDC** (no long-lived keys). Dependabot for NuGet and npm. Optional container and dependency scanning.

## 5. Cost

See [10 — Cost and Hosting](10-cost-and-hosting.md): the Lean, Middle and Full tiers, cost drivers, the free-credit strategy, budgets and teardown. Fargate tasks use **arm64** (Graviton) images; ElastiCache uses **Valkey**.

## 6. Operations

Dashboards: active sessions, connected clients, draws/sec, join failures, Redis memory. Alarms: unhealthy targets, 5xx rate, Redis memory/evictions, task restarts. Redis eviction policy `volatile-ttl`; since all keys carry TTLs, memory is bounded by session count.
