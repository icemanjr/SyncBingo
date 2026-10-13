# 09 — Roadmap

## Phase 0 — Foundations
- [ ] Repo, solution layout, Docker Compose (`web`, `api`, `redis`), CI skeleton
- [ ] Domain: variant model, card generation, bitmask patterns, state machine (with tests)

## Phase 1 — Core game (MVP)
- [ ] Create session, game code, QR, password; join by QR and by code
- [ ] Name uniqueness, session tokens, reconnect + snapshot
- [ ] Manual and auto calling, "Grab a new ball", pause/resume
- [ ] Marking with server validation; auto-fill; Bingo button
- [ ] Win conditions: Normal, Four Corners, B/I/N/G/O, Blackout; change mid-round
- [ ] Win detection modes; host awards wins; win highlight
- [ ] Rounds: end, clear cards, redraw policy; joins between rounds only
- [ ] Leaderboard and session stats; host inspect card; kick
- [ ] Ad layout with placeholder slots; greyed-out Serious Bingo tile

## Phase 2 — Polish and hardening
- [ ] Accessibility pass, reduced motion, screen-reader labels
- [ ] Rate limiting, profanity filter, TTL/expiry handling
- [ ] Playwright suite, k6 load test, OpenAPI-generated client

## Phase 3 — AWS deployment
- [ ] CDK stacks (network, data, app, web), GitHub OIDC pipeline
- [ ] Dashboards and alarms; cost-conscious demo configuration; teardown docs

## Phase 4 — Future expansion
- [ ] **Serious Bingo:** host-generated card claims (single-use QR), host-added cards, multiple cards per participant, cards expire at round end (no payments in-app; legal review first)
- [ ] Custom variants (other ball ranges, card sizes), custom patterns
- [ ] **Custom content** (ADR 0009): call faces and card faces, e.g. "2 + 5" called and "7" marked, definitions and words, shapes, word lists, uploaded images; free-form card generator
- [ ] Waiting room for late arrivals, spoken ball calls, themes, real ad provider integration
- [ ] Optional end-of-session summary export

## Portfolio notes
Pin a short README with: architecture diagram, a GIF of host + phone play, the ADR list, the test pyramid, and a "run it locally in one command" section. These docs, the ADRs and the CDK stacks are the parts most worth highlighting.
