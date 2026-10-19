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
- [x] Accessibility pass: every screen scanned with axe-core (WCAG 2.1 AA, light and dark) on every PR; the card's grid fixed; reduced motion honoured (docs/06 §3)
- [x] Rate limiting (wrong passwords, game creation, marks), `Idempotency-Key` on awards, OpenAPI document
- [x] Profanity filter for player names (FR-4); ended games expire after 5 minutes, and an expired game reads as ended on every screen (docs/04 §4)
- [x] OpenAPI-generated client (types from the API's own description; CI checks they're current)
- [x] Playwright suite (`e2e/`, the Compose `tests` service, a CI job that gates deploys; docs/08)
- [x] **k6 WebSocket load test against the real Lean box** (`load/bingo.js`): 10 games × 30 players, balls delivered in 34 ms (median), 0 failures, 242 MB of memory left (docs/10 §5b)

## Phase 3 — Full AWS design (portfolio)
- [x] CDK stacks (network, data, app, web), GitHub OIDC pipeline; context flag **`profile=lean|full`**, `cdk synth` for both in CI
- [x] Price the synthesized `full` stacks (AWS Pricing Calculator / Pricing API); mark docs/10 figures verified
- [ ] Deploy **Full** for an evening on free credits (`infra/scripts/full-up.sh`): end-to-end and k6 runs against it, a failover with one task stopped, dashboard and console captures; then `full-down.sh` the same day (docs/10 §3)
- [x] Dashboards and alarms; teardown checklist (docs/10)
- [x] Terms of Use and Privacy pages, plain-language **drafts** of what the app actually does (`/terms`, `/privacy`), linked from every page's footer; the operator's name and the addresses `legal@` and `support@syncbingo.com` come from build settings (`web/.env`)
- [ ] Legal review of those drafts before relying on them

## Phase 4 — Next features (soon after launch)
- [ ] **Custom content, text only** (ADR 0009, FR-44): call faces and card faces, e.g. "2 + 5" called and "7" marked, definitions and words, word lists; a **set editor** page, reached from the lobby, that edits a draft and sends it with one *Use this set*, to add entries one by one (with a *Ball and square match* checkbox), add from another set file, edit and delete entries; clashes skipped and reported; **limits** (24–500 pairs, ball text ≤ 120, square text ≤ 15, ≤ 10 groups with names ≤ 20, file ≤ 64 KB); free-form card generator; upload page with a host-responsibility notice and acknowledgement; custom sets private to the host; one stored set per session, downloadable as a single file and re-uploaded to reuse; switch sets between rounds, redealing every card; optional groups saved in the file and a host "Group items" toggle, independent of card size, free space and patterns; card-size options and a "no free space" option as their own settings
- [ ] **Custom free space text:** an optional `"free"` field in the set file (e.g. `"free": "★ Week 5"`) sets the text on the free space; it changes only what the square says, never whether there is a free space (that stays a game setting). Without it, the free space reads **FREE** (upper case), which also replaces today's ★ on cards in standard games; the small pattern diagrams keep ★. Checked like square text (printable, ≤ 15 characters)
- [ ] **Custom variants:** other ball ranges and card sizes, custom patterns
- [ ] **Waiting room** for late arrivals: people who arrive mid-round wait and join at the next lobby, instead of being refused (assumption A3 changes then)
- [ ] Spoken ball calls, themes, real ad provider integration
- [ ] Optional end-of-session summary export
- [ ] *Deferred:* images on balls or squares (storage and data-transfer cost, moderation; docs/10)

## Phase 5 — Serious Bingo (only if the site gets regular traffic)
- [ ] The host takes payment **outside the app** (no payments in-app), then **assigns card(s)** to players: one or several per player, as bought. Optionally by single-use QR claims a player scans. Cards expire at the end of the round. Players hold several cards on one screen (the model already allows a list of cards, docs/04). **Legal review first:** real-money games are regulated in many places.

## Portfolio notes
Pin a short README with: architecture diagram, a GIF of host + phone play, the ADR list, the test pyramid, and a "run it locally in one command" section. These docs, the ADRs and the CDK stacks are the parts most worth highlighting.
