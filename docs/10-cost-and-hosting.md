# 10 — Cost and Hosting

SyncBingo is a personal project paid for out of pocket; banner ads are only a hope. The goal is a **real site, always on, at SyncBingo.com, for the lowest monthly cost**, while the full AWS design in CDK remains the portfolio showpiece. The running site uses the **Lean** tier (ADR 0011); the **Full** tier is deployed once, on free credits, for screenshots and a demo recording, then torn down.

> **Figures:** every price below is marked **unverified (research notes)** until it is checked against the AWS Pricing Calculator / Pricing API or measured on the real deployment, when it is changed to **verified** with the date. Prices are US East, October 2026.

## 1. Tiers

| Tier | What runs | Monthly cost | Gets you | Trade-offs |
|------|-----------|--------------|----------|------------|
| **Lean** (the live site) | One small instance running Docker Compose: Caddy (TLS + static SPA + reverse proxy), the API, and a **Valkey** container | **$7** + domain *(verified 2026-10-15: Lightsail `micro_3_0`, us-east-2)* | A real, always-on site; the same code and store as Full | One instance, no high availability. A deploy or restart briefly drops connections, but **games survive**: state is in Valkey, and clients reconnect and resync from the snapshot. A box failure ends live games |
| **Middle** | One Fargate ARM task behind an ALB, ElastiCache Valkey (t4g.micro), S3 + CloudFront | ~$35–45 *(unverified)* | Managed containers and cache, TLS at the edge | The ALB's fixed hourly charge dominates; little gained over Lean for this traffic |
| **Full** (the reference design, docs/07) | 2 Fargate ARM tasks across AZs, ALB, ElastiCache Valkey, S3 + CloudFront, CloudWatch (DNS on Cloudflare) | ~$50–60 *(unverified)* | High availability, autoscaling, the complete portfolio architecture | Mostly fixed hourly charges whatever the traffic |

Middle is documented for comparison; it is not planned.

## 2. Cost drivers (research notes, all unverified)

| Item | Price | Note |
|------|-------|------|
| ALB | ~$0.0225/h (~$16.40/mo) + LCUs | The largest fixed cost of Middle and Full |
| Fargate ARM (Graviton) | $0.03238 per vCPU-h, $0.00356 per GB-h | ~20% cheaper than x86 ($0.04048 / $0.004446). A 0.25 vCPU / 0.5 GB task ≈ $7/mo |
| Public IPv4 | ~$0.005/h (~$3.60/mo) each | Per task or ALB subnet; the alternative, a NAT gateway (~$32/mo), is worse |
| ElastiCache Valkey cache.t4g.micro | ~$0.0128/h (~$9.34/mo) | Redis OSS is ~$0.016/h. Serverless Valkey floor ~$6/mo; serverless Redis OSS ~$91/mo floor. **Use Valkey** |
| Route 53 hosted zone | $0.50/mo | **Not needed for Lean**: DNS is on Cloudflare, free |
| Domain | `syncbingo.com`, **registered on Cloudflare** (2026-10-14) | Cloudflare charges the registry's price on renewal too, with no markup |
| CloudFront | Always-free 1 TB egress and 10 M requests/mo | ACM certificates are free |
| Data transfer | Expected small | Text-only play; estimated a few tens of MB per 30-player game, mostly the first SPA load. **To be measured** (§5) |

Text-only custom sets (ADR 0009, amended) keep data transfer and storage out of the cost picture: no S3 uploads and no image egress.

## 2a. Domain and DNS

`syncbingo.com` is registered with **Cloudflare Registrar**, and its DNS is managed there for free.

- **Lean (done 2026-10-15):** an `A` record for `syncbingo.com` and `www` points at the instance's static IP. Start **DNS only** (grey cloud), so Caddy gets Let's Encrypt certificates directly. Turning on Cloudflare's proxy later (free TLS at the edge, caching, DDoS protection; WebSockets are supported) means switching Caddy to a DNS-01 challenge with a Cloudflare API token, or to a Cloudflare origin certificate. That decision belongs to Phase 1b.
- **Full:** CloudFront's alias (`CNAME`) and the ACM validation record go in Cloudflare too, so the Full profile needs no Route 53 zone. CDK outputs the records to add; they are added by hand or through the Cloudflare API.
- **Account hygiene:** the Cloudflare account signs in with Google, so its security is the **Google account's**: keep 2-Step Verification on there (a passkey or an authenticator app rather than SMS). Registrar lock on, and auto-renew on (an expired domain can be taken by someone else).

## 3. The AWS project

Created 2026-10-15 through AWS's **new sign-up experience**: a *project* signed in with a social login, rather than a classic account with a root user.

- **Region:** fixed at **us-east-2 (Ohio)**. AWS assigns it from the contact address (every US address gets us-east-2), and new-experience projects can't create Regional resources anywhere else. Oregon would be closer to the maintainer in Utah, but the extra 20–30 ms doesn't matter for a Bingo game. Choosing a Region would mean activating advanced features, which isn't worth it. So Lean runs in us-east-2. **For Phase 3:** CloudFront requires its ACM certificate in us-east-1, which these project rules may not allow; check this before building the Full stacks.
- **Plan:** **Paid**, upgraded on day one, with $100 of credits, which are used first. On the Free plan the project would have closed after 6 months (2027-04-15) unless upgraded in time.
- **Services:** Lightsail, Budgets, ECS, ElastiCache, CloudFront and S3 are all available to such projects.

**Strategy:**
1. Launch **Lean** as the live site ✓ (2026-10-15).
2. Once the game is complete, deploy **Full** once with `cdk deploy -c profile=full`, capture screenshots, a dashboard view and a short demo recording, then `cdk destroy`.
3. Keep Lean running. The credits cover roughly the first year of it.

## 4. Target budgets and cost controls

| Tier | Target |
|------|--------|
| Lean | **≤ $7/month** plus the domain ✓ (the `micro_3_0` plan, $7) |
| Full | **≤ $60/month**, and only for the days it is deployed |

- **Spend limit (AWS Settings): $30/month.** Past the limit, AWS pauses the project, which takes the site down, so it sits well above the normal ~$7–8. Of its early controls, only *Stop new resource launches* is on. *Pause idle resources* is off, because a party site is idle most of the day. *Pause top cost drivers* is off, because that would be the server itself. The limit emails at 50%, 75% and 90% ($15, $22.50, $27).
- **AWS Budgets: `syncbingo-monthly-10`,** a $10 monthly cost budget that excludes credits and refunds, so it tracks real usage. It emails on actual and on forecast spend above $10, which is earlier than the spend limit's first email at $15. The planned $25 alert was dropped, because the spend limit's emails cover it.

## 5. Verification plan

| What | How | When (roadmap) |
|------|-----|----------------|
| Capacity of the Lean box (target ~10 sessions × 30 players) | k6 WebSocket load test against the real box, `docker stats` for memory and CPU | Phase 2 |
| Bytes per game | Measure a scripted 30-player game: SPA load, snapshot, events | Phase 1b |
| Lean instance size and price | ✓ 2026-10-15. Lightsail plans in us-east-2: nano $5 (0.5 GB), **micro $7 (1 GB)**, small $12 (2 GB). Nano is too small for .NET + Valkey (256 MB cap) + Caddy. The IPv6-only plans ($1.50–2 cheaper) are ruled out, because many phone and venue networks are IPv4-only. At idle on micro, the containers use about 130 MB of 909 MB | Phase 1b |
| Full stack price | AWS Pricing Calculator / Pricing API on the synthesized `profile=full` stacks | Phase 3 |

## 6. Teardown

- **Full:** `cdk destroy -c profile=full --all`. Check afterwards: no running tasks, ALB, ElastiCache cluster, NAT gateway or elastic IPs; ECR images and CloudWatch log groups deleted or set to expire.
- **Lean:** stop or delete the instance and release its static IP (an unattached static IP is billed).
- Leave the Budgets alerts in place.
