# 08 — Testing Strategy

## 1. Pyramid

| Level | Tooling | Focus |
|-------|---------|-------|
| **Domain unit** | xUnit, Shouldly, FsCheck | Card generation, pattern evaluation, state machine, auto-fill rule, name normalisation, game-code generator |
| **Application** | xUnit + fakes for ports | Command handlers: authorisation (who may run each command, and on whose cards), state preconditions, event emission, lost-update races (the fake store refuses stale saves) |
| **Integration (API)** | `WebApplicationFactory` + Testcontainers (Redis) | REST endpoints, auth, rate limits, Redis layout, scheduler claiming, concurrent joins |
| **Realtime integration** | SignalR .NET client in tests | Event delivery, audience rules (host-only vs everyone), reconnect + snapshot resync |
| **Frontend unit** | Vitest + Testing Library | Event-stream sequencing and resync, the API client, token storage (incl. blocked storage), AdSlot layout, pages, store reducers |
| **End-to-end** | Playwright (multiple browser contexts) | Host + 3 participants through full flows, mobile viewport emulation |
| **Load** | k6 (WebSocket), `load/bingo.js` | Many games of real WebSocket players marking their cards; draw fan-out latency. Run against the live box off-hours (results: docs/10 §5b) |

## 2. Key scenarios

**Domain / property tests**
- Every generated card has numbers in the correct column ranges, no duplicates, centre free; columns are shuffled and every number is equally likely in its column.
- The generator never deals a card that is already in play, deals every distinct card of a tiny variant before failing, and fails clearly when none are left.
- Every pattern evaluates correctly for hand-built mark sets, including all 12 Normal Bingo lines and each letter shape.
- Auto-fill marks only the *previous* ball, only on cards containing it, and never marks a ball that was not called.
- *Free marking* (on/off) and *Unmarking* (off / mismarks only / any) work independently: every one of the six combinations behaves as specified; *Mismarks only* never removes a mark on a called square.
- *Earned* free space: the centre is not counted, markable (with *Free marking* off) or auto-filled until a called ball is missing from the card; a card whose numbers are all called first earns it only on its first miss; Blackout and centre lines need it earned.
- Undo restores the card to its marks at the last call whatever the unmarking setting, keeps auto-fill marks, before the first ball returns the card as dealt, and is a no-op when nothing changed since the last ball (FsCheck: any sequence of marks, unmarks and undos between two draws ends in a state reachable by the rules).
- Auto-fill applies only to participants who switched it on, and only while the host makes it available.
- Win detection uses called numbers, not marks; claims qualify on called numbers, plus marks when *Require marks* is on.
- *Multiple wins*: a double Bingo reports 2 lines; once per round, once per pattern and every declared win award as specified; a second line noticed later can be claimed and awarded; awarding the same claim twice counts once; a press with nothing left to award shows 0 awardable in every mode.
- Game codes are 16 characters, from the allowed alphabet, with no collisions in a large sample.

**Integration**
- Join: wrong password (401), duplicate name (409, case-insensitive), join mid-round (409), too many wrong passwords (429, locked 10 minutes) while 30 correct joins, or 50 typos then a correct join, from one address all succeed; game creation and marking limits (429); an award repeated with one `Idempotency-Key` counts once.
- Two simultaneous joins with the same name → exactly one succeeds.
- Concurrent joins and redraws never leave two identical cards in play; a redrawn or kicked player's card is released.
- Token recovery: disconnect, reconnect, same card and marks.
- *Start Round N* counts down 10 s, closes joining and redraws, can be cancelled, and starts the round at zero (in auto mode with its first ball if toggled); *Clear cards* returns to the lobby.
- Rounds start in manual mode; Auto mode draws at once and then on the timer; Pause stops auto-calls; Grab ball works only in manual mode.
- Scheduler: with two API instances, each tick draws exactly once; a killed instance's lease expires and draws resume.
- Participants never receive other participants' cards; host-only events never reach participants.
- Realtime: each stream's `seq` is unbroken (players don't see claims, so their numbers don't skip); the snapshot's `seq` is where the caller's stream continues; wins and removals reach everyone by name, without ids; private notices reach only their player; only a token for that game can connect, by header or `access_token` query.
- Authorisation: a participant token gets 403 on every host endpoint; host and display tokens get 403 on player commands; a player can only reach their own cards; a removed player's token stops working.
- Participant writes: a mark and a draw's auto-fill that overlap both survive (no lost update), against real Redis.
- Redis store: sessions and participants round-trip; of many concurrent name reservations, card claims or saves from one participant version exactly one wins; every key carries the idle expiry; a session past 12 hours reads as gone; the lock admits one holder at a time, times out, frees itself after a crash and is never released by a former holder; removing a player revokes only their token.
- Displays: a display link's token is in the URL fragment, never the query; a display sees public state and the join note but no claims, and can call no command; a disconnected display is told and its token stops working; at most 10 displays; players never get the join note.
- TV pairing: a code works once (even with hosts racing) and expires after 10 minutes; only a host can approve it, and a full game refuses without using up the code; the TV receives a read-only display token, also when it connects after the approval; an unknown pairing gets `PairingExpired`; pairing creation and approval are rate-limited.

**End-to-end (Playwright)**
1. Host creates a game, a participant joins via the join URL, and another via manual code entry.
2. Host draws balls; participants mark; a winner is detected; host awards; leaderboard updates.
3. Host switches Normal → Blackout mid-round, then clears cards; a late joiner is admitted only between rounds.
4. Participant phone "sleeps" (page reload) and recovers.
5. Layout: ad slots reserve space and the card never shifts when an ad loads.
6. Accessibility: every screen, light and dark, has no WCAG 2.1 A/AA violations by axe-core, and reduced motion switches animation off (`accessibility.spec.ts`).

These live in `e2e/tests/` (Playwright, one game per test, run in parallel): `joining.spec.ts` (1, the between-rounds half of 3, and 4), `round.spec.ts` (2 with a real line found by calling balls, a false Bingo that needs confirming, the pattern change from 3, and leaving the game), `display.spec.ts` (the TV view follows the calls) and `layout.spec.ts` (5, on a phone and on a laptop). They find controls by role and accessible name, never by layout or CSS, so a redesign doesn't break them. CI runs them on every PR against the stack built from that commit, and images are published only after they pass. Writing them found two real problems: nginx kept a stale address for the API after it restarted (fixed, docs/07), and a timing assumption in a test about when a refused join is answered.

## 3. Quality gates in CI

Build warnings as errors · nullable reference types on · TypeScript `strict` · ESLint/Prettier · coverage reported (domain and application layers target ≥ 90 %) · integration and e2e suites green before merge.
