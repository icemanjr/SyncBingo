# SyncBingo — Design Documents

> A phone-first, drop-in party Bingo web app for family gatherings, parties and groups of adults. One person hosts, everyone else joins from their own phone by scanning a QR code or typing a game code.

**Stack:** C# / .NET 10 (API + SignalR) · React 19 + TypeScript (SPA) · Valkey, the Redis-compatible store (ephemeral state) · Docker Compose for local and test · AWS, provisioned with AWS CDK in C#, in two profiles (ADR 0011): **Lean**, the live site on one small instance, and **Full** (ECS Fargate, ElastiCache, S3 and CloudFront), the portfolio design.

**Status:** Live at https://syncbingo.com. Phases 0 to 3 are built, and Phase 4 is under way (docs/09). These documents are the source of truth: a change of decision updates them, and an ADR, in the same pull request.

## Document index

| # | Document | Purpose |
|---|----------|---------|
| 01 | [Requirements](01-requirements.md) | Functional and non-functional requirements, scope, assumptions |
| 02 | [Architecture](02-architecture.md) | Components, layering, real-time design, concurrency |
| 03 | [Game state machine](03-game-state-machine.md) | Session/round lifecycle, allowed commands, win handling |
| 04 | [Data model](04-data-model.md) | Domain model, card/pattern bitmasks, Redis layout, security |
| 05 | [API contract](05-api-contract.md) | REST endpoints and SignalR events |
| 06 | [UI and ad slots](06-ui-and-ads.md) | Screens, layout, ad-slot rules, accessibility |
| 07 | [Infrastructure](07-infrastructure.md) | Docker local setup, AWS CDK stacks, CI/CD |
| 08 | [Testing strategy](08-testing-strategy.md) | Unit, integration, end-to-end, load |
| 09 | [Roadmap](09-roadmap.md) | MVP phases, the next features (Phase 4) and "Serious Bingo" (Phase 5) |
| 10 | [Cost and hosting](10-cost-and-hosting.md) | Lean / Middle / Full tiers, cost drivers, free credits, budgets, teardown |
| — | [ADRs](adr/) | Short records of the key architectural decisions |

## Glossary

| Term | Meaning |
|------|---------|
| **Session** (a.k.a. game) | Everything that happens from the moment a host creates a game until it ends: all rounds, all participants. |
| **Round** | A span during which cards carry marks. **Start Round N** runs a 10-second countdown, then the round starts with a full hopper; **End round** or **Clear cards** ends it and returns to the lobby. Multiple winners and win-condition changes (e.g. Normal Bingo → Blackout) can happen inside one round. |
| **Lobby** | Between rounds (`BetweenRounds`): cards are dealt, the set and the round-scoped settings can change, and late arrivals get their cards. |
| **Hopper** | The pool of balls not yet called: 75 on a 5 × 5 card, 60 on 4 × 4, 45 on 3 × 3, or a custom set's items. |
| **Call / ball** | A ball drawn from the hopper and announced: a number, or with a custom set its ball text (e.g. "2 + 5"), matched by a square (e.g. "7"). |
| **Win condition** | The patterns being played; any selected one wins (FR-21): Normal Bingo (rows, columns, diagonals), Four Corners, the letters B I N G O (5 × 5 only), Blackout, or the host's own drawn patterns (FR-21a). |
| **Custom set** | The host's own balls and squares (FR-44, ADR 0009), built in the set editor and kept as a file; optionally in groups, one per card column. |
| **Waiting room** | Where someone who joins during a countdown or round waits, without a card, until the game is back in the lobby (FR-5). |
| **Display** | A read-only screen for the room (FR-28): a display link in another tab, or a smart TV paired with a short code (ADR 0010). No ads. |
| **Host** | The person running the session. The host doesn't play (A5); a host who wants to play joins from another device. |
| **Participant** | A player in the session, identified only by a name that is unique within the session. |
| **Game code** | 16-character random code (four groups of four, hyphenated) carried by the QR code. |
| **Game password** | Chosen by the host; required to join, whether by QR or by typing the code. |
| **Session token** | Opaque secret issued at join so a participant can recover after a sleeping phone or dropped connection. |
