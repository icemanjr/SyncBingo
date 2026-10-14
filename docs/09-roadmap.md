# 09 — Roadmap

## Phase 0 — Foundations
- [x] Repo, solution layout, Docker Compose (`web`, `api`, `redis`), CI skeleton
- [x] Domain: variant model, card generation, bitmask patterns, state machine (with tests)

## Phase 1 — Core game (MVP)
- [ ] Create session, game code, QR, password; join by QR and by code
- [ ] Name uniqueness, session tokens, reconnect + snapshot
- [ ] Manual and auto calling: rounds start manual ("Grab ball"); "Auto mode" draws at once then on a timer; "Pause" returns to manual
- [ ] Marking with server validation; auto-fill; Bingo button
- [ ] Win conditions: Normal, Four Corners, B/I/N/G/O, Blackout; change mid-round
- [ ] Win detection modes; host awards wins; win highlight
- [ ] Rounds: end, clear cards, redraw policy; joins between rounds only
- [ ] Leaderboard and session stats; host inspect card; kick
- [x] API: display links, the displays list and disconnect, and the join note (FR-28, FR-49); TV pairing with a short code (ADR 0010)
- [ ] Call board as the host's default view (Options button for rules), shareable with participants; pop-out read-only display view for projectors (no ads); smart TV / projector pairing with a short code (ADR 0010)
- [ ] Ad layout with placeholder slots; greyed-out Serious Bingo tile

## Phase 1b — Lean launch (live at SyncBingo.com, ADR 0011, docs/10)
- [ ] Switch Redis to **Valkey** (Compose, Testcontainers); the integration tests prove compatibility
- [ ] **arm64 + amd64** images, cross-compiled (keep the CI image job under 2 minutes)
- [ ] Production Compose profile: **Caddy** (Let's Encrypt, SPA, proxy), API, Valkey with append-only persistence; forwarded headers from Caddy trusted, so rate limits see each client's address
- [x] Domain: `syncbingo.com`, registered on Cloudflare (2026-10-14)
- [ ] AWS account (Paid plan, credits), **Budgets alerts at $10 and $25**, the Lean instance, Cloudflare DNS records
- [ ] Deploy pipeline to the instance (pull and restart off-hours); measure real bytes per game; verify the docs/10 figures for Lean

## Phase 2 — Polish and hardening
- [ ] Accessibility pass, reduced motion, screen-reader labels
- [x] Rate limiting (wrong passwords, game creation, marks), `Idempotency-Key` on awards, OpenAPI document
- [ ] Profanity filter, TTL/expiry handling
- [ ] Playwright suite, OpenAPI-generated client
- [ ] **k6 WebSocket load test against the real Lean box** with `docker stats` (target ~10 sessions × 30 players); record the result in docs/10

## Phase 3 — Full AWS design (portfolio)
- [ ] CDK stacks (network, data, app, web), GitHub OIDC pipeline; context flag **`profile=lean|full`**, `cdk synth` for both in CI
- [ ] Price the synthesized `full` stacks (AWS Pricing Calculator / Pricing API); mark docs/10 figures verified
- [ ] Deploy **Full** once on free credits: screenshots, dashboards, a demo recording; then `cdk destroy`
- [ ] Dashboards and alarms; teardown checklist (docs/10)
- [ ] Terms of Use and privacy pages (incl. host responsibility for custom content), linked from the footer; legal review before public launch

## Phase 4 — Future expansion
- [ ] **Serious Bingo:** host-generated card claims (single-use QR), host-added cards, multiple cards per participant, cards expire at round end (no payments in-app; legal review first)
- [ ] Custom variants (other ball ranges, card sizes), custom patterns
- [ ] **Custom content, text only** (ADR 0009, FR-44): call faces and card faces, e.g. "2 + 5" called and "7" marked, definitions and words, word lists; a **set editor** in the lobby to add entries one by one (with a *Ball and square match* checkbox), add from another set file, edit and delete entries; clashes skipped and reported; **limits** (24–500 pairs, ball text ≤ 120, square text ≤ 30, ≤ 10 groups with names ≤ 20, file ≤ 64 KB); free-form card generator; upload page with a host-responsibility notice and acknowledgement; custom sets private to the host; one stored set per session, downloadable as a single file and re-uploaded to reuse; switch sets between rounds, redealing every card; optional groups saved in the file and a host "Group items" toggle, independent of card size, free space and patterns; card-size options and a "no free space" option as their own settings
- [ ] *Deferred:* images on balls or squares (storage and data-transfer cost, moderation; docs/10)
- [ ] Waiting room for late arrivals, spoken ball calls, themes, real ad provider integration
- [ ] Optional end-of-session summary export

## Portfolio notes
Pin a short README with: architecture diagram, a GIF of host + phone play, the ADR list, the test pyramid, and a "run it locally in one command" section. These docs, the ADRs and the CDK stacks are the parts most worth highlighting.
