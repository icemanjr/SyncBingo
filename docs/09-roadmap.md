# 09 — Roadmap

## Phase 0 — Foundations
- [ ] Repo, solution layout, Docker Compose (`web`, `api`, `redis`), CI skeleton
- [ ] Domain: variant model, card generation, bitmask patterns, state machine (with tests)

## Phase 1 — Core game (MVP)
- [ ] Create session, game code, QR, password; join by QR and by code
- [ ] Name uniqueness, session tokens, reconnect + snapshot
- [ ] Manual and auto calling: rounds start manual ("Grab ball"); "Auto mode" draws at once then on a timer; "Pause" returns to manual
- [ ] Marking with server validation; auto-fill; Bingo button
- [ ] Win conditions: Normal, Four Corners, B/I/N/G/O, Blackout; change mid-round
- [ ] Win detection modes; host awards wins; win highlight
- [ ] Rounds: end, clear cards, redraw policy; joins between rounds only
- [ ] Leaderboard and session stats; host inspect card; kick
- [ ] Call board as the host's default view (Options button for rules), shareable with participants; pop-out read-only display view for projectors (no ads); smart TV / projector pairing with a short code (ADR 0010)
- [ ] Ad layout with placeholder slots; greyed-out Serious Bingo tile

## Phase 2 — Polish and hardening
- [ ] Accessibility pass, reduced motion, screen-reader labels
- [ ] Rate limiting, profanity filter, TTL/expiry handling
- [ ] Playwright suite, k6 load test, OpenAPI-generated client

## Phase 3 — AWS deployment
- [ ] CDK stacks (network, data, app, web), GitHub OIDC pipeline
- [ ] Dashboards and alarms; cost-conscious demo configuration; teardown docs
- [ ] Terms of Use and privacy pages (incl. host responsibility for custom content), linked from the footer; legal review before public launch

## Phase 4 — Future expansion
- [ ] **Serious Bingo:** host-generated card claims (single-use QR), host-added cards, multiple cards per participant, cards expire at round end (no payments in-app; legal review first)
- [ ] Custom variants (other ball ranges, card sizes), custom patterns
- [ ] **Custom content** (ADR 0009): call faces and card faces, e.g. "2 + 5" called and "7" marked, definitions and words, shapes, word lists, uploaded images; free-form card generator; upload page with a host-responsibility notice and acknowledgement; custom sets private to the host
- [ ] Waiting room for late arrivals, spoken ball calls, themes, real ad provider integration
- [ ] Optional end-of-session summary export

## Portfolio notes
Pin a short README with: architecture diagram, a GIF of host + phone play, the ADR list, the test pyramid, and a "run it locally in one command" section. These docs, the ADRs and the CDK stacks are the parts most worth highlighting.
