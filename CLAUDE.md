# SyncBingo

Phone-first, drop-in party Bingo web app. A host creates a session; participants join by scanning a QR code or typing a game code, plus a host-set game password. This repo is also a **portfolio project** meant to show real experience with the stack below, so code quality, tests, docs and infrastructure-as-code matter as much as features.

## Status

**Design is complete; no code exists yet.** Start by reading `docs/README.md`, then the numbered documents it links. The docs are the source of truth. If you change a decision, update the relevant doc and add or amend an ADR in `docs/adr/`.

## Stack

- Backend: C# / .NET 10, ASP.NET Core minimal APIs + SignalR
- Frontend: React 19 + TypeScript (strict), Vite, React Router, Zustand, Tailwind, `@microsoft/signalr`
- State: Redis-compatible store only (ephemeral, TTL), moving to **Valkey** (ADR 0011). No relational database, no accounts.
- Local/test: Docker Compose (`web`, `api`, `redis`), Testcontainers for integration tests
- Cloud: AWS provisioned with **AWS CDK in C#**, two profiles (ADR 0011, docs/10): **Lean**, the live site at syncbingo.com (registered and DNS on Cloudflare) on one small instance (Compose: Caddy + API + Valkey, target ≤ $7/month); **Full**, the portfolio design (ECS Fargate arm64, ALB, ElastiCache Valkey, S3 + CloudFront, ACM; DNS on Cloudflare), deployed once on free credits then torn down. CI/CD with GitHub Actions + OIDC
- Tests: xUnit (+ FsCheck), WebApplicationFactory, Vitest, Playwright, k6

## Planned layout

```
src/SyncBingo.Domain/           pure C#, no I/O
src/SyncBingo.Application/      commands, handlers, ports
src/SyncBingo.Infrastructure/   Redis store, scheduler, hashing
src/SyncBingo.Api/              endpoints, SignalR hub, auth
tests/                          Domain, Application, Api.IntegrationTests
web/                            React + TypeScript
infra/                          AWS CDK (C#)
docs/                           design documents and ADRs
docker-compose.yml
```

## Core concepts (full glossary in `docs/README.md`)

- **Session** = the whole game, all rounds. **Round** = while cards carry marks. *Clear cards* ends a round and returns to the lobby; *Start Round N* runs a 10 s countdown (joining closes) and then starts the next round, in manual mode unless the host toggled Auto. Multiple winners and win-condition changes (e.g. Normal -> Blackout) happen *within* a round.
- Standard 75-ball Bingo; win conditions: Normal (row/column/diagonal), Four Corners, letter shapes B/I/N/G/O, Blackout.
- Joining is allowed **only between rounds**. Names are unique per session (case-insensitive). No persistent accounts.
- Game code: 16 chars, shown as four hyphenated groups of four; the QR carries the code, never the password.
- Participants get a session token so they can recover after a sleeping phone or dropped connection.

## Key architectural rules (see `docs/adr/`)

1. **Server-authoritative** state. Clients send intents; the server validates them and judges every claim against the balls actually called. Independent host settings: *Free marking* (players may mark uncalled numbers, "mismarks"; default on) and *Unmarking* (any / mismarks only / off; default any). *Undo* back to the card's state at the last call is always available.
2. **REST for commands, SignalR for events only**, WebSockets only, Redis backplane. Events carry a sequence number; the snapshot endpoint is the source of truth.
3. **Rules are data.** Cards hold a 25-bit mark mask; win patterns are lists of bitmasks; a win is `(marks & mask) == mask`. Variants (ball range, card size, patterns) are configuration.
4. **Redis with TTL**, behind `ISessionStore`. Shared-state mutations take a short per-session lock; marks update only the participant's own hash field.
5. **Auto-call scheduling** uses a Redis sorted set with an atomic Lua claim and lease, so exactly one instance draws per tick.
6. **Host credits wins.** A win needs a claim (Bingo button or a shout) and the host's *Award win*. Detection only alerts the host and uses called numbers, not marks; every Bingo press reaches the host, valid or not. Auto-fill is opt-in per participant when the host makes it available. The host does not play; a host who wants to play joins from another device.
7. Secrets and tokens are stored hashed; wrong game passwords, game creation and marking are rate-limited; a participant only ever receives their own card.

## Ads and UI constraints

Banner ads only: top/bottom on mobile, left/right on desktop, in fixed-size reserved slots (placeholders for now). Never pop-ups or anything that can cover the play area. Mobile-first, 48 px minimum tap targets, WCAG AA, reduced-motion support, no flashing. The Full portfolio build shows a greyed-out, non-functional "Serious Bingo - coming soon" tile on the landing page; the live Lean site does not (build-time switch `VITE_SERIOUS_BINGO_TEASER`).

## Confirmed assumptions (`docs/01-requirements.md` section 7)

A1 wins credited by host. A2 game-code alphabet omits look-alike characters. A3 mid-round join attempts are refused (no waiting room). A4 a round starts after a 10 s countdown, in manual mode unless toggled to auto (amended). A5 superseded: the host does not play (FR-15 removed). A6 Grab ball only in manual mode; Pause = back to manual (amended). A7 defaults: 100 participants, 4 h idle expiry, 12 h hard lifetime cap. A8 free centre cell always marked by default; the host may instead make it *Earned* by the first called ball not on the player's card (FR-26).

## Out of scope (for now)

Persistent accounts, payments or any handling of money, chat, native apps. "Serious Bingo" (host-generated card claims, multiple cards per participant, cards expiring each round) is a future phase; keep `Participant.Cards` a list so it needs no model change.

## Suggested first tasks (Phase 0 in `docs/09-roadmap.md`)

1. Create the solution/project layout, `.editorconfig`, nullable + warnings-as-errors, and the Docker Compose skeleton.
2. Implement the Domain layer with tests first: `BingoVariant`, card generation, bitmask pattern evaluation (incl. all 12 Normal lines and the letter shapes in `docs/03`), game-code generator, session/round state machine, auto-fill rule.
3. Add CI skeleton (build, tests, `cdk synth` later).

## Working agreements

- Plan before large changes; keep commits small and conventional.
- Write tests alongside domain and application code; don't skip failing tests.
- Ask before deviating from a documented decision; if the decision is changing, update the docs and ADRs in the same change.
- Keep the README portfolio-ready (architecture diagram, run-locally section, test pyramid, ADR list).
