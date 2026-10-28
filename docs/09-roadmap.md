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
- [ ] Ad layout with placeholder slots (web: done); grayed-out Serious Bingo tile in the Full build only (`VITE_SERIOUS_BINGO_TEASER`)
- [x] Dark mode with a System / Light / Dark switch (FR-50)
- [x] Player-chosen mark shape, color and opacity (FR-51)
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
- [x] Accessibility pass: every screen scanned with axe-core (WCAG 2.1 AA, light and dark) on every PR; the card's grid fixed; reduced motion honored (docs/06 §3)
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
- [x] **Custom content, text only** (ADR 0009, FR-44; built 2026-10-19 to 24 in PRs from #74): call faces and card faces, e.g. "2 + 5" called and "7" marked, definitions and words, word lists; a **set editor** page, reached from the lobby, that edits a draft and sends it with one *Use this set*, to add entries one by one (with a *Ball and square match* checkbox), add from another set file, edit and delete entries; clashes skipped and reported; **limits** (8–500 pairs and enough for the card, possible cards shown, "No unique cards available" when all are held, ball text ≤ 120, square text ≤ 15, ≤ 5 groups with names ≤ 20, file ≤ 64 KB); free-form card generator; upload page with a host-responsibility notice and acknowledgement; custom sets private to the host; one stored set per session, downloadable as a single file and re-uploaded to reuse; switch sets between rounds, redealing every card; optional groups saved in the file, which are the card's columns (the editor's *Use groups*; amended 2026-10-22, replacing a separate *Group items* toggle), independent of free space and patterns; card-size options and a "no free space" option as their own settings
- [x] **Custom free space text** (built 2026-10-24): an optional `"free"` field in the set file (e.g. `"free": "★ Week 5"`) sets the text on the free space; it changes only what the square says, never whether there is a free space (that stays a game setting). Without it, the free space reads **FREE** (upper case), which also replaces today's ★ on cards in standard games; the small pattern diagrams keep ★. Checked like square text (printable, ≤ 15 characters)
- [x] **Custom variants** (built 2026-10-24): card sizes 3 × 3, 4 × 4 and 5 × 5 (FR-26a) and the host's own patterns, drawn in Options, added and renamed but never deleted (FR-21a). Other ball ranges need no feature of their own: a custom set of numbers gives any range (1–30 on a 3 × 3 card is 30-ball speed Bingo)
- [ ] *Deferred:* **90-ball Bingo** (3 × 9 tickets with blank squares, won by one line, two lines or a full house): blank squares and "any two lines" are new kinds of card and pattern, so it waits for demand
- [x] **Waiting room** for late arrivals (built 2026-10-25, FR-5): people who arrive mid-round wait without a card, setting up their marks meanwhile, and join automatically when the game is back in the lobby; the host sees them apart under Waiting (A3 amended)
*Order set by the owner, 2026-10-27: themes, the ad provider, the summary report, spoken ball calls, then translations.*
- [x] **Themes** (built 2026-10-27, FR-50a): color palettes (Classic, Ocean, Forest, Halloween, Winter), each in light and dark and contrast-checked; the host picks one for the game in Options, and a player may pick their own on their device. Colors only, nothing extra to download
- [x] **Real ad provider** (built 2026-10-27, ADR 0012): Google AdSense in the reserved banner slots, every request tagged as directed at children, Google's consent message for the EU, UK and Switzerland, and the Privacy page describing it. Ready but off: it goes live when the owner's AdSense account approves the site and the `AD_PROVIDER` repository variable is set (docs/06 §2)
- [ ] *If AdSense asks for more content* (owner, 2026-10-27): the landing page gains a short overview of what SyncBingo is and a **glossary** of how SyncBingo uses its terms, since some mean other things elsewhere. For example: a **game** runs from when the host creates it (and its password) until the host ends it; a **round** lasts until the cards are wiped clean of marks; the **host** runs the game and can't also play (A5), so a host who wants to play joins from another device. The glossary in `docs/README.md` is the starting point
- [ ] **End-of-session summary report** (optional export)
- [ ] **Spoken ball calls**
- [ ] **Translations** (owner, 2026-10-25): every piece of user-facing text in one file per language (e.g. `web/src/locales/en.json`), so a translator copies the file and translates the values; **Spanish** first, translated by the owner. Points to settle when it starts:
  - **Web text** through a small i18n library (e.g. react-i18next), with plurals ("1 win" / "2 wins") and inserted values ("Remove {name}?").
  - **Server messages** (refusals, notices such as "No unique cards available…") sent as a code plus values, so the web phrases them in the player's language and the text lives in one place.
  - **Choosing a language:** the browser's by default, with a switch in the footer; each person chooses their own, so one game can mix languages.
  - **The host's own patterns** may also carry a name in each supported language (e.g. `"es": "Signo más"`), optional and checked like the main name; each app shows the player's language, falling back to the main name. They are short and used in every game, numbers included, so the cost is tiny.
  - **Not translated:** set content and player names. A set in two languages would roughly double what every player downloads for each set, which doesn't fit the Lean profile; revisit only if the site moves to Full. A language class is already served by pairs: the ball in one language, the square its translation. The Terms and Privacy pages may need a reviewed translation.
  - **Spanish details:** opening ¿ and ¡, gendered agreement, and text about 20–30 % longer, which matters on phone-sized buttons.
- [ ] *Deferred:* images on balls or squares (storage and data-transfer cost, moderation; docs/10)

## Phase 5 — Serious Bingo (only if the site gets regular traffic)
- [ ] The host takes payment **outside the app** (no payments in-app), then **assigns card(s)** to players: one or several per player, as bought. Optionally by single-use QR claims a player scans. Cards expire at the end of the round. Players hold several cards on one screen (the model already allows a list of cards, docs/04). **Legal review first:** real-money games are regulated in many places.

## Portfolio notes
Pin a short README with: architecture diagram, a GIF of host + phone play, the ADR list, the test pyramid, and a "run it locally in one command" section. These docs, the ADRs and the CDK stacks are the parts most worth highlighting.
