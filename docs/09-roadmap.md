# 09 — Roadmap

## Phase 0 — Foundations
- [x] Repo, solution layout, Docker Compose (`web`, `api`, `redis`), CI skeleton
- [x] Domain: variant model, card generation, bitmask patterns, state machine (with tests)

## Phase 1 — Core game (MVP)
- [x] Create session, game code, QR, password; join by QR and by code
- [ ] Name uniqueness, session tokens, reconnect + snapshot
- [ ] Manual and auto calling: rounds start manual ("Grab ball"); "Auto mode" draws at once then on a timer; "Pause" returns to manual
- [ ] Marking with server validation; auto-fill; Bingo button
- [ ] Win conditions: Normal, Four Corners, B/I/N/G/O, Blackout; change mid-round
- [ ] Win detection modes; host awards wins; win highlight
- [ ] Rounds: end, clear cards, redraw policy; joins between rounds only
- [ ] Leaderboard and session stats; host inspect card; kick
- [x] API: display links, the displays list and disconnect, and the join note (FR-28, FR-49); TV pairing with a short code (ADR 0010)
- [x ] Call board as the host's default view (Options button for rules), shareable with participants; pop-out read-only display view for projectors (no ads); smart TV / projector pairing with a short code (ADR 0010)
- [ ] Ad layout with placeholder slots (web: done); greyed-out Serious Bingo tile in the Full build only (`VITE_SERIOUS_BINGO_TEASER`)
- [x] Dark mode with a System / Light / Dark switch (FR-50)
- [x] Player-chosen mark shape, colour and opacity (FR-51)
- [x] Players: patterns and current rules (FR-48), call board, auto-fill switch, card swaps, leaderboard

## Phase 1b — Lean launch (live at SyncBingo.com, ADR 0011, docs/10)
- [x] Switch Redis to **Valkey** (Compose, Testcontainers); the integration tests prove compatibility
- [x] **arm64 + amd64** images, cross-compiled (keep the CI image job under 2 minutes)
- [x] Production Compose profile: **Caddy** (Let's Encrypt, SPA, proxy), API, Valkey with append-only persistence; forwarded headers from Caddy trusted, so rate limits see each client's address
- [x] Domain: `syncbingo.com`, registered on Cloudflare (2026-10-14)
- [x] AWS project (Paid plan, $100 credits, us-east-2), a $30 spend limit and a $10 Budgets alert, the Lean instance (`deploy/setup.sh`), Cloudflare DNS records. **Live at https://syncbingo.com** (2026-10-15)
- [x] Deploy pipeline: CI publishes images to GHCR after merges; the server pulls nightly (`deploy/update.sh`, docs/07 §1a)
- [x] Measure real bytes per game: about 7 MB per 30-player party of 5 rounds, 26.7 KB of Valkey memory per game (docs/10 §5a); Lean price verified ($7)

## Phase 2 — Polish and hardening
- [ ] Accessibility pass, reduced motion, screen-reader labels
- [x] Rate limiting (wrong passwords, game creation, marks), `Idempotency-Key` on awards, OpenAPI document
- [ ] Profanity filter, TTL/expiry handling
- [x] OpenAPI-generated client (types from the API's own description; CI checks they're current)
- [x] Playwright suite (`e2e/`, the Compose `tests` service, a CI job that gates deploys; docs/08)
- [x] **k6 WebSocket load test against the real Lean box** (`load/bingo.js`): 10 games × 30 players, balls delivered in 34 ms (median), 0 failures, 242 MB of memory left (docs/10 §5b)

## Phase 3 — Full AWS design (portfolio)
- [ ] CDK stacks (network, data, app, web), GitHub OIDC pipeline; context flag **`profile=lean|full`**, `cdk synth` for both in CI
- [ ] Price the synthesized `full` stacks (AWS Pricing Calculator / Pricing API); mark docs/10 figures verified
- [ ] Deploy **Full** once on free credits: screenshots, dashboards, a demo recording; then `cdk destroy`
- [ ] Dashboards and alarms; teardown checklist (docs/10)
- [ ] Terms of Use and privacy pages (incl. host responsibility for custom content), linked from the footer; legal review before public launch

## Phase 4 — Next features (soon after launch)
- [ ] **Custom content, text only** (ADR 0009, FR-44): call faces and card faces, e.g. "2 + 5" called and "7" marked, definitions and words, word lists; a **set editor** in the lobby to add entries one by one (with a *Ball and square match* checkbox), add from another set file, edit and delete entries; clashes skipped and reported; **limits** (24–500 pairs, ball text ≤ 120, square text ≤ 20, ≤ 10 groups with names ≤ 20, file ≤ 64 KB); free-form card generator; upload page with a host-responsibility notice and acknowledgement; custom sets private to the host; one stored set per session, downloadable as a single file and re-uploaded to reuse; switch sets between rounds, redealing every card; optional groups saved in the file and a host "Group items" toggle, independent of card size, free space and patterns; card-size options and a "no free space" option as their own settings
- [ ] **Custom variants:** other ball ranges and card sizes, custom patterns
- [ ] **Waiting room** for late arrivals: people who arrive mid-round wait and join at the next lobby, instead of being refused (assumption A3 changes then)
- [ ] Spoken ball calls, themes, real ad provider integration
- [ ] Optional end-of-session summary export
- [ ] *Deferred:* images on balls or squares (storage and data-transfer cost, moderation; docs/10)

## Phase 5 — Serious Bingo (only if the site gets regular traffic)
- [ ] The host takes payment **outside the app** (no payments in-app), then **assigns card(s)** to players: one or several per player, as bought. Optionally by single-use QR claims a player scans. Cards expire at the end of the round. Players hold several cards on one screen (the model already allows a list of cards, docs/04). **Legal review first:** real-money games are regulated in many places.

## Portfolio notes
Pin a short README with: architecture diagram, a GIF of host + phone play, the ADR list, the test pyramid, and a "run it locally in one command" section. These docs, the ADRs and the CDK stacks are the parts most worth highlighting.
