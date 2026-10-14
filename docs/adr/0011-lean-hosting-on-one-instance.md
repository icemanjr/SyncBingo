# ADR 0011 — Lean hosting on one instance; Valkey; Full design kept in CDK

**Status:** Accepted (2026-10-14)

## Context
The site should be live and always on at SyncBingo.com, so a recruiter can simply use it, while the project is paid for out of pocket. The reference architecture (ADR 0007: Fargate, ALB, ElastiCache, CloudFront) costs roughly $50–60 a month in fixed hourly charges whatever the traffic (docs/10, unverified figures). Traffic will be small, and text-only play moves very little data.

## Decision
- **Two deployment profiles from the same code.**
  - **Lean** (the live site): one small instance running Docker Compose: **Caddy** (Let's Encrypt TLS, serves the SPA, proxies `/api` and `/hubs`), the API, and a **Valkey** container with append-only persistence.
  - **Full** (the portfolio design): unchanged from ADR 0007 and docs/07, deployed once on free credits for screenshots and a demo, then torn down.
  - The CDK app takes a context flag, `profile=lean|full`; `cdk synth` must pass for both.
- **Valkey, not Redis OSS**, everywhere: Compose, Testcontainers and ElastiCache. Valkey is Redis-compatible (StackExchange.Redis and our Lua scripts, including `cjson`, work unchanged) and cheaper on ElastiCache. ADR 0003's design is unchanged; only the engine changes.
- **One store implementation.** Lean runs the same `RedisSessionStore` against the local Valkey container, rather than a second in-memory `ISessionStore`. It costs ~10–20 MB of RAM and keeps live games through API restarts and deploys; the scheduler lease and the SignalR backplane work unchanged with one instance.
- **Images for arm64 and amd64**, built by cross-compiling on the build machine (`BUILDPLATFORM`), not emulation, so the CI image job stays fast. Graviton (arm64) is the cheaper Fargate option; the Lean instance uses whichever architecture its bundle offers.

## Consequences
- The live site costs about a bundle's price plus the domain; the Full design stays real code, synthesized in CI.
- Lean has no high availability: a deploy or restart briefly drops connections (clients reconnect and resync from the snapshot, ADR 0002), and a failure of the instance ends live games. Deploys happen off-hours.
- Caddy replaces nginx in the Lean profile. The `web` image (nginx) stays for local Compose; the Full profile serves the same SPA build from S3 + CloudFront.
- The live Lean site shows no "Serious Bingo — coming soon" teaser, so it promises nothing that may not come; the Full build turns it on (`VITE_SERIOUS_BINGO_TEASER=true`).
- An in-memory store remains possible behind `ISessionStore` if a store-less host is ever wanted; not planned.
