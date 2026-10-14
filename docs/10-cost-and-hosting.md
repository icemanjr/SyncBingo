# 10 — Cost and Hosting

SyncBingo is a personal project paid for out of pocket; banner ads are only a hope. The goal is a **real site, always on, at SyncBingo.com, for the lowest monthly cost**, while the full AWS design in CDK remains the portfolio showpiece. The running site uses the **Lean** tier (ADR 0011); the **Full** tier is deployed once, on free credits, for screenshots and a demo recording, then torn down.

> **Figures:** every price below is marked **unverified (research notes)** until it is checked against the AWS Pricing Calculator / Pricing API or measured on the real deployment, when it is changed to **verified** with the date. Prices are US East, October 2026.

## 1. Tiers

| Tier | What runs | Monthly cost | Gets you | Trade-offs |
|------|-----------|--------------|----------|------------|
| **Lean** (the live site) | One small instance running Docker Compose: Caddy (TLS + static SPA + reverse proxy), the API, and a **Valkey** container | ~$5–7 + domain *(unverified)* | A real, always-on site; the same code and store as Full | One instance, no high availability. A deploy or restart briefly drops connections, but **games survive**: state is in Valkey, and clients reconnect and resync from the snapshot. A box failure ends live games |
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

- **Lean:** an `A` record (and `AAAA`, if the instance has IPv6) for `syncbingo.com` and `www` points at the instance's static IP. Start **DNS only** (grey cloud), so Caddy gets Let's Encrypt certificates directly. Turning on Cloudflare's proxy later (free TLS at the edge, caching, DDoS protection; WebSockets are supported) means switching Caddy to a DNS-01 challenge with a Cloudflare API token, or to a Cloudflare origin certificate. That decision belongs to Phase 1b.
- **Full:** CloudFront's alias (`CNAME`) and the ACM validation record go in Cloudflare too, so the Full profile needs no Route 53 zone. CDK outputs the records to add; they are added by hand or through the Cloudflare API.
- **Account hygiene:** two-factor authentication on the Cloudflare account, registrar lock on, and auto-renew on (an expired domain can be taken by someone else).

## 3. Free credits (new account)

There is no AWS account yet, so it will be created under the post-15-July-2025 rules *(unverified, to check at sign-up)*:

- $100 in credits on sign-up, plus up to $100 more for onboarding tasks (e.g. launching and terminating an EC2 instance, creating an AWS Budgets alert).
- The **Free plan** lasts 6 months, then the account closes unless upgraded (90 days to recover). The **Paid plan** keeps the same credits and then bills normally. An always-on site needs the Paid plan, or an upgrade before the 6 months end.
- To confirm: which services the Free plan allows (Lightsail? Fargate, ALB, ElastiCache?), and whether credits apply to Lightsail.

**Strategy:**
1. Create the account; set **AWS Budgets alerts at $10 and $25** on day one (one of the onboarding tasks).
2. Launch **Lean** as the live site.
3. Once the game is complete, deploy **Full** once with `cdk deploy -c profile=full`, capture screenshots, a dashboard view and a short demo recording, then `cdk destroy`.
4. Keep Lean running; credits cover it for a long time if they apply to it.

## 4. Target budgets

| Tier | Target |
|------|--------|
| Lean | **≤ $7/month** plus the domain |
| Full | **≤ $60/month**, and only for the days it is deployed |
| Alerts | AWS Budgets at **$10** (warning) and **$25** (act) |

## 5. Verification plan

| What | How | When (roadmap) |
|------|-----|----------------|
| Capacity of the Lean box (target ~10 sessions × 30 players) | k6 WebSocket load test against the real box, `docker stats` for memory and CPU | Phase 2 |
| Bytes per game | Measure a scripted 30-player game: SPA load, snapshot, events | Phase 1b |
| Lean instance size and price | Lightsail bundle that fits API + Valkey + Caddy with headroom (check: whether the $5 bundle now has 512 MB or 1 GB, and that the IPv6-only bundle is ruled out for phones on IPv4-only networks) | Phase 1b |
| Full stack price | AWS Pricing Calculator / Pricing API on the synthesized `profile=full` stacks | Phase 3 |

## 6. Teardown

- **Full:** `cdk destroy -c profile=full --all`. Check afterwards: no running tasks, ALB, ElastiCache cluster, NAT gateway or elastic IPs; ECR images and CloudWatch log groups deleted or set to expire.
- **Lean:** stop or delete the instance and release its static IP (an unattached static IP is billed).
- Leave the Budgets alerts in place.
