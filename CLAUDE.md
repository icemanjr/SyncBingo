# SyncBingo

Phone-first, drop-in party Bingo web app. A host creates a game; players join by scanning a QR code or typing a game code, plus a host-set game password. This repo is also a **portfolio project** meant to show real experience with the stack below, so code quality, tests, docs and infrastructure-as-code matter as much as features.

## Status

**Live at https://syncbingo.com** (the Lean profile, ADR 0011). Roadmap Phases 0 to 2 are built, and Phase 3 is too except the Full profile's one-evening deploy and the legal review of the Terms and Privacy drafts. Phase 4 is under way, in the owner's order in `docs/09-roadmap.md` (themes and the ad provider are done; next the end-of-session summary report, spoken ball calls, then translations). AdSense is wired in but off until Google approves the site (ADR 0012, docs/06 §2).

The docs are the source of truth: `docs/README.md` (index and glossary), then `docs/01`–`10` and `docs/adr/`. If a decision changes, update the relevant doc and add or amend an ADR in the same pull request.

## Stack

- **Backend:** C# / .NET 10, ASP.NET Core minimal APIs and SignalR (WebSockets only).
- **Frontend:** React 19 + TypeScript (strict), Vite, React Router, Tailwind CSS v4, `@microsoft/signalr`, `openapi-fetch` with types generated from the API. No state library: each screen holds a snapshot in `useGame`.
- **State:** **Valkey**, the Redis-compatible store, only (ephemeral, TTL; ADR 0003, ADR 0011). No relational database, no accounts.
- **Local and test:** Docker Compose (`web` on 8080 behind nginx, `api` on 5080, `valkey`, and a `tests` profile running Playwright); Testcontainers (Valkey) for integration tests.
- **Cloud:** AWS with **AWS CDK in C#**, two profiles (ADR 0011, docs/07, docs/10):
  - **Lean**, the live site: one Lightsail instance running Compose (Caddy + API + Valkey), about $7 a month; DNS on Cloudflare. It updates itself nightly at 04:00 Mountain from images CI publishes (`deploy/update.sh`).
  - **Full**, the portfolio design: ECS Fargate (arm64), ALB, ElastiCache Valkey, S3 + CloudFront. Deployed once on free credits, then torn down (`infra/scripts/full-up.sh`, `full-down.sh`).
  - CI/CD is GitHub Actions (`.github/workflows/ci.yml`) with OIDC.
- **Tests:** xUnit v3 + Shouldly (+ FsCheck), WebApplicationFactory, Vitest + Testing Library, Playwright + axe-core, k6 (`load/bingo.js`).

## Repository map

```
src/SyncBingo.Domain/          pure C#, no I/O: the rules
  Sessions/Session.cs           the game: state machine, settings, calls, claims, wins, patterns (biggest file)
  Sessions/SessionSettings.cs   every host setting and its default; which are round-scoped / player-facing
  Sessions/SessionEvents.cs     the events commands return (and the API publishes)
  Participants/                 Participant (cards, marks, Undo, auto-fill), CardRules, names + NameFilter
  Rules/                        BingoVariant, CardLayout, WinPattern, WinSelection, StandardVariants, CardSize, CustomPattern
  Cards/                        Card, generators (by column, free-form)
  Content/                      custom sets: ContentSet, ContentSetFile (the JSON file), ContentVariant, ContentText
src/SyncBingo.Application/     use cases and ports
  Sessions/SessionCommandRunner the pipeline every shared-state command follows (lock, load, apply, save, schedule, publish)
  Sessions/HostCommands.cs      host commands, RoundHandler (countdown, clear cards, admitting the waiting room)
  Sessions/ JoinSession, CreateSession, JoinInfo, ContentHandler (custom sets)
  Players/                      a player's own commands (no session lock), host's player management, ParticipantUpdates (optimistic saves)
  Calling/DrawHandler.cs        every ball: Grab ball, Auto mode, the scheduler's tick, auto-fill, win detection
  Queries/Snapshot.cs           GET /snapshot: what each caller may see (players never get the answer key)
  Ports/Ports.cs                ISessionStore, IEventPublisher, ICallScheduler, notifications
src/SyncBingo.Infrastructure/  Redis store (Lua scripts, keys, content cache), scheduler, hashing, tokens, pairing store
src/SyncBingo.Api/             endpoints (Endpoints/*), contracts, auth (session tokens), rate limits, SignalR hubs and publisher
tests/                         Domain, Application (in-memory fakes), Api.IntegrationTests (Testcontainers), Infra (CDK)
web/src/                       the React app (layout in docs/02 §7): app/, api/, realtime/, storage/, features/{game,host,play,display,sets,landing,legal}
e2e/                           Playwright specs, run in Compose
infra/                         AWS CDK (C#): Lean and Full stacks
deploy/                        the live server's setup, update script and timer
load/                          k6 load test
docs/                          design documents and ADRs
```

## How a request flows

- **Commands are REST; events are SignalR** (ADR 0002). A client sends an intent (`POST /api/sessions/{code}/balls`). The endpoint resolves the caller from the bearer token (`SessionTokenAuthentication`) and calls a handler, which runs through `SessionCommandRunner`:
  1. check the caller (host only, unless the command says otherwise);
  2. take the per-session lock;
  3. load the session;
  4. apply the domain method, which returns events or a `DomainError`;
  5. save;
  6. keep the auto-call schedule in step;
  7. send private notifications (auto-fill, a new card), then publish the events.
- **Events** are numbered per audience (players, host, display); `Session.AudienceOf` decides who gets each. A client applies them with `applyEvent` (`web/src/features/game/gameEvents.ts`) and refetches the snapshot after a gap, a reconnect, or an event it can't absorb. **The snapshot is the source of truth.**
- **Refusals** are `DomainError`s, mapped to RFC 7807 problems with a `code` (`Api/Http/Problems.cs`). Their messages are written to be shown to people as they are.
- **A player's marks, Undo and auto-fill switch** take no session lock: they save the participant with optimistic concurrency (`ParticipantUpdates`, a Lua compare-and-set).
- **Auto mode and countdowns** are a Redis sorted set; every API instance polls it and claims due games atomically (`RedisCallScheduler`, `CallSchedulerService`).

## Core concepts (full glossary in `docs/README.md`)

- **Game (session)** = from creation until the host ends it (or it expires: 4 h idle, 12 h at most, 5 minutes after ending). **Round** = while cards carry marks. **Start Round N** runs a 10 s countdown, then the round starts in manual mode (Grab ball) unless the host chose Auto. **End round** or **Clear cards** returns to the **lobby** (`BetweenRounds`). Wins and pattern changes happen *within* a round.
- **Joining** is open at any time until the game ends. During a countdown or round, joiners enter the **waiting room** (FR-5, A3 amended): they get a name and their settings but no card. They are admitted, in arrival order, by every command that returns the game to the lobby (`RoundHandler.AdmitWaitingAsync`). Names are unique per game (case-insensitive) and filtered for offensive words.
- **Rules are data:**
  - **Card sizes:** 5 × 5 (75 balls), 4 × 4 (60, no free space) and 3 × 3 (45); 15 balls per column on every size (FR-26a).
  - **Patterns:** a selection of patterns, any of which wins: Normal Bingo (rows + columns + diagonals), Four Corners, the letters B I N G O (5 × 5 only), Blackout, plus up to 5 patterns the host draws (FR-21a), which can be renamed but never deleted.
  - **Free space:** always marked, Earned, or None (FR-26).
- **Custom sets** (FR-44, ADR 0009): pairs of ball text and square text, built in the set editor (`/host/:code/set`) and kept by the host as a JSON file.
  - With **Use groups**, there is one group per card column, dealt like B-I-N-G-O; without it, items go anywhere on the card.
  - A small set can make fewer different cards than there are players; the next joiner is told "No unique cards available" (FR-39).
- **Displays** (FR-28): read-only screens for the room, opened from a display link or a TV paired with a short code at `/tv` (ADR 0010). They show no ads.
- **Themes** (FR-50a): the host's color palette for the game; each player can override it on their own device. Light and dark mode are separate, per device.

## Key architectural rules (see `docs/adr/`)

1. **Server-authoritative** state. Clients send intents; the server validates them and judges every claim against the balls actually called. *Free marking* (mismarks allowed; default on) and *Unmarking* (any / mismarks only / off) are independent host settings; *Undo* back to the last call always works.
2. **REST for commands, SignalR for events only**, WebSockets only, Redis backplane. Events carry a per-audience sequence number; the snapshot endpoint is the source of truth.
3. **Rules are data.** A card's marks are a bitmask (up to 32 cells); patterns are lists of masks; a win is `(marks & mask) == mask`. Card size, ball range and patterns are configuration (`BingoVariant`). A custom set becomes a variant whose balls are the set's items (`ContentVariant`).
4. **Valkey/Redis with TTL**, behind `ISessionStore`. Shared-state changes take a short per-session lock; every check-and-write is a Lua script; a session's keys all expire together.
5. **Auto-call scheduling** uses a sorted set with an atomic Lua claim and lease, so exactly one instance draws per tick.
6. **The host credits wins** (ADR 0008). A win needs a claim (the Bingo button or a shout) and the host's *Award*. Detection only alerts the host, from called numbers rather than marks. Every Bingo press reaches the host, valid or not. Auto-fill is opt-in per player when the host offers it. **The host doesn't play** (A5); a host who wants to play joins from another device.
7. **Secrets and the answer key.** Tokens and the game password are stored hashed. Wrong passwords, game creation, marking, pairing and set changes are rate-limited. A player only ever receives their own card. In a custom-set game a player's card holds **placeholders, never item numbers**, which with the board's ball text would pair every clue with its word. Every card a player gets goes through `CardView.CellsFor`.
8. **Rules change only when quiet.** Options is a draft applied with one **Apply**, in the lobby or manual mode, never in auto mode or during the countdown. Mid-round changes show a "Rules updated" notice and a 3 s pause before the next ball (FR-47). Round-scoped settings (card size, free space, redraws, multiple wins) change only in the lobby.

## Ads and UI constraints

- **Ads:**
  - Banners only: top and bottom on phones, left and right on desktops, in fixed-size reserved slots (`app/AdSlot.tsx`). None on the display or TV.
  - Placeholders in development and the Full build. The live site uses Google AdSense once approved (ADR 0012); the build switches are `VITE_AD_PROVIDER` and the `VITE_ADSENSE_*` ids in `web/.env`.
  - Every ad request is child-directed.
  - Google's consent message for the EU, UK and Switzerland is the one allowed exception to "never pop-ups or anything that can cover the play area". Keep Auto ads off in the AdSense account.
- **UI:**
  - Mobile-first, 48 px minimum tap targets, WCAG AA in light and dark and every theme (axe scans in e2e), reduced-motion support, no flashing.
  - The Full build shows a grayed-out "Serious Bingo – coming soon" tile on the landing page; the live site does not (`VITE_SERIOUS_BINGO_TEASER`).
- **Wording:** never call it "Kahoot-style"; say "phone-first, drop-in party Bingo".

## Running and checking things

| What | Command |
|------|---------|
| Build and test the backend | `dotnet build -warnaserror` and `dotnet test` (needs Docker/Podman for the Valkey Testcontainer) |
| Web checks | in `web/`: `npm run lint`, `npm run typecheck`, `npm test`, `npx prettier --check .` |
| **After any API-visible change** | in `web/`: `npm run api:generate`, which rebuilds `src/api/openapi.json` and `schema.d.ts`; commit them |
| Run the stack locally | `docker compose up --build -d`, then http://localhost:8080 |
| End-to-end tests | `docker compose --profile tests run --rm tests` (after `up --build -d`) |
| Web dev server | `npm run dev` in `web/` (proxies `/api` and `/hubs` to the API on 5080) |
| Load test | `k6 run -e BASE=http://localhost:8080 load/bingo.js` |
| CDK synth | in `infra/`: `npx aws-cdk synth -c profile=lean`, or `-c profile=full -c stage=dev` (CI checks both) |
| Deploy the live site now | `ssh ubuntu@18.223.223.138 'sudo /opt/syncbingo/update.sh'` (otherwise nightly), after CI's Publish images |

## Debugging tips

- **Start from the snapshot.** `GET /api/sessions/{code}/snapshot` with the caller's token shows exactly what that screen should show. If a screen disagrees with it, the bug is in the web app's event handling (`applyEvent`, `useGame`). If the snapshot is wrong, look in the domain or `Queries/Snapshot.cs`.
- **Redis keys** are `bingo:s:{code}:meta|participants|names|tokens|token-owners|cards|displays|lock`, plus `bingo:schedule` and the pairing keys (docs/04 §4). Locally: `docker compose exec valkey valkey-cli`.
- **Live logs:** the API logs to stdout in Compose. On the server, `sudo docker compose -f /opt/syncbingo/docker-compose.lean.yml logs api`. No access logs are kept (privacy).
- **"No unique cards available"** means a small custom set has run out of different cards (`BingoVariant.PossibleCards`). The set editor shows the count while the host edits.
- **A command that "does nothing"** has probably returned no events: domain methods return `[]` for a no-op, and the runner then neither saves nor publishes.
- **A web test can't find text**: user-facing text is asserted literally in Vitest and Playwright specs. Change both when rewording.

## Confirmed assumptions (`docs/01-requirements.md` §7)

- **A1** The host credits wins.
- **A2** The game-code alphabet omits look-alike characters.
- **A3** Amended: late arrivals wait in the waiting room rather than being refused.
- **A4** A round starts after a 10 s countdown, in manual mode unless toggled to Auto.
- **A5** The host does not play.
- **A6** Grab ball only in manual mode; Pause means back to manual.
- **A7** Defaults: 100 players, 4 h idle expiry, 12 h hard cap.
- **A8** The free space is always marked by default; the host may choose Earned or None (FR-26).

## Out of scope (for now)

Persistent accounts, payments or any handling of money, chat, native apps. "Serious Bingo" (the host takes payment outside the app and assigns cards to players, several per player; cards expire each round) is roadmap Phase 5, only if the site gets regular traffic. Keep `Participant.Cards` a list so it needs no model change. 90-ball Bingo is deferred. Custom sets are not translated (it would double what every player downloads).

## Working agreements

- **Branches and PRs:**
  - Never push to `master`. Every change gets a branch and a PR; the owner reviews and squash-merges.
  - Stacked PRs: after the parent is squash-merged, `git rebase --onto origin/master <parent> <child>`.
  - Delete merged local branches at the end of a phase, not after each merge. Ask before deleting anything on GitHub.
- **Before larger changes,** plan; keep commits small and conventional (`feat:`, `fix:`, `refactor:`, `docs:`).
- **Tests:** write them alongside domain and application code; don't skip failing tests.
- **Decisions:** ask before deviating from a documented one. If a decision is changing, update the docs and ADRs in the same change.
- **README:** keep it portfolio-ready (architecture diagram, run-locally section, test pyramid, ADR list).
