# 08 — Testing Strategy

## 1. Pyramid

| Level | Tooling | Focus |
|-------|---------|-------|
| **Domain unit** | xUnit, Shouldly, FsCheck | Card generation, pattern evaluation, state machine, auto-fill rule, name normalisation, game-code generator |
| **Application** | xUnit + fakes for ports | Command handlers: authorisation, state preconditions, event emission |
| **Integration (API)** | `WebApplicationFactory` + Testcontainers (Redis) | REST endpoints, auth, rate limits, Redis layout, scheduler claiming, concurrent joins |
| **Realtime integration** | SignalR .NET client in tests | Event delivery, audience rules (host-only vs everyone), reconnect + snapshot resync |
| **Frontend unit** | Vitest + Testing Library | Card component, AdSlot layout, hooks, store reducers |
| **End-to-end** | Playwright (multiple browser contexts) | Host + 3 participants through full flows, mobile viewport emulation |
| **Load** | k6 (WebSocket) | 100 participants per session, many sessions, draw fan-out latency |

## 2. Key scenarios

**Domain / property tests**
- Every generated card has numbers in the correct column ranges, no duplicates, centre free.
- Every pattern evaluates correctly for hand-built mark sets, including all 12 Normal Bingo lines and each letter shape.
- Auto-fill marks only the *previous* ball, only on cards containing it, and never marks a ball that was not called.
- Marks cannot be set for uncalled numbers.
- Game codes are 16 characters, from the allowed alphabet, with no collisions in a large sample.

**Integration**
- Join: wrong password (401), duplicate name (409, case-insensitive), join mid-round (409), rate limiting (429).
- Two simultaneous joins with the same name → exactly one succeeds.
- Token recovery: disconnect, reconnect, same card and marks.
- Pause freezes auto-calls; resume continues; manual draw works while paused.
- Scheduler: with two API instances, each tick draws exactly once; a killed instance's lease expires and draws resume.
- Participants never receive other participants' cards; host-only events never reach participants.

**End-to-end (Playwright)**
1. Host creates a game, a participant joins via the join URL, and another via manual code entry.
2. Host draws balls; participants mark; a winner is detected; host awards; leaderboard updates.
3. Host switches Normal → Blackout mid-round, then clears cards; a late joiner is admitted only between rounds.
4. Participant phone "sleeps" (page reload) and recovers.
5. Layout: ad slots reserve space and the card never shifts when an ad loads.

## 3. Quality gates in CI

Build warnings as errors · nullable reference types on · TypeScript `strict` · ESLint/Prettier · coverage reported (domain and application layers target ≥ 90 %) · integration and e2e suites green before merge.
