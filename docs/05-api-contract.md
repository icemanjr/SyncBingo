# 05 — API Contract

Base path `/api`. JSON everywhere. Errors use RFC 7807 `application/problem+json`. Auth is a bearer **session token** (host or participant). Commands are REST; the SignalR hub only pushes events.

## 1. Public endpoints

| Method & path | Body | Success | Errors |
|---------------|------|---------|--------|
| `POST /sessions` | `{ password, settings?, winCondition? }` | `201 { code, joinUrl, hostToken, snapshot }` | 400 malformed body · 422 password not 4–64 characters, or invalid settings |
| `GET /sessions/{code}/join-info` | – | `200 { acceptingJoins, round }` | 404 unknown code |
| `POST /sessions/{code}/join` | `{ password, name }` | `200 { participantId, token, snapshot }` | 401 wrong password · 429 too many wrong passwords from this address (§5) · 409 name taken · 409 "A round is in progress. You can join between rounds." (during a countdown too) · 404 |

`joinUrl` is `{origin}/join/{code}`, which is what the QR encodes. The password is never in the URL.

## 2. Participant endpoints (participant token)

The snapshot also accepts a host or display token. Every other endpoint here acts on the caller's own record, taken from the token: the routes carry no participant id, and a `cardId` is looked up only among the caller's own cards, so no request can reach another player's card (an unknown `cardId` is 422). A host or display token gets 403; a player the host removed gets 404.

| Method & path | Body | Notes |
|---------------|------|-------|
| `GET /sessions/{code}/snapshot` | – | The caller's view: `{ game, you, host }`. **`game`** (everyone): code, state, round and upcoming round, countdown end, pattern, the variant's **patterns as data** `{ id, name, masks[] }` (bit *i* = cell *i*; FR-48), the card shape `{ rows, columns, freeCell }`, the call board as groups of balls, the calls (`null` for a player unless *Show ball*, *Highlight matching square* or *Show call board* is on), balls remaining, `nextBallFrom` (FR-47), the settings players notice, the leaderboard (`null` for players and the display until the host shows it), `version` and `seq`. **`you`** (a player): own id, name, auto-fill switch and cards `{ cardId, cells, marks }`, never anyone else's. **`host`** (the host): every setting, the players `{ participantId, name, joinedAt }`, open claims this round and the round summaries. Create and join return the same snapshot. Used on load, reconnect and gap detection. |
| `POST /sessions/{code}/marks` | `{ cardId, cell, marked }` | Rejects (422) marking an uncalled number when *Free marking* is off, and an unmark that *Unmarking* does not allow (off, or a called square under *Mismarks only*). Returns updated marks. |
| `POST /sessions/{code}/cards/{cardId}/undo` | – | Undo (FR-25): resets the card to its marks when the last ball was called. Always allowed during a round. Returns updated marks. |
| `POST /sessions/{code}/claims` | `{ cardId }` | Bingo button. `202 { claimedAt }`: the claim always goes to the host (FR-37a); the player is not told whether it qualifies. |
| `PUT /sessions/{code}/me/auto-fill` | `{ on }` | Switch auto-fill for the caller's own card; 409 unless the host made it available. |
| `POST /sessions/{code}/cards/redraw` | `{ cardId }` | Only when policy allows and state is BetweenRounds. |

## 3. Host endpoints (host token)

| Method & path | Body | Notes |
|---------------|------|-------|
| `PUT /sessions/{code}/rules` | `{ settings?, winCondition? }` | **Apply** in Options: the settings sent (only those fields change) and the pattern, in one step. `200 { settings, winCondition }`. 409 in auto mode ("Pause before changing the rules.") and during the countdown; 422 for a lobby-only setting (free space, redraw policy, multiple wins) during a round, with nothing applied. |
| `POST /sessions/{code}/rounds/countdown` | `{ startInAuto: bool }` | *Start Round N*: begins the 10 s countdown (FR-45); 409 unless in the lobby. |
| `DELETE /sessions/{code}/rounds/countdown` | – | Cancel the countdown; back to the lobby. |
| `POST /sessions/{code}/cards/clear` | `{ redrawCards: bool }` | *Clear cards*: ends any running round, clears marks and calls, optional redraw, back to the lobby (FR-34); 409 during a countdown. |
| `POST /sessions/{code}/rounds/current/end` | – | End round early; cards and calls stay visible. |
| `POST /sessions/{code}/auto` | – | Auto mode: draws a ball at once and starts the timer (409 unless in manual mode). |
| `POST /sessions/{code}/pause` | – | Back to manual mode (409 unless in auto mode). |
| `POST /sessions/{code}/balls` | – | "Grab ball" (manual mode only; 409 in auto mode). Returns the ball and `sequence`. |
| `POST /sessions/{code}/wins` | `{ participantId, cardId?, wins?, claimId? }` | Award a win for the current win condition, as *Multiple wins* allows (FR-43). `wins` > 1 only with *every declared win*. Passing the `claimId` makes repeats harmless; awards without a claim (a shouted Bingo) send an `Idempotency-Key` header so a double click counts once (§5). |
| `POST /sessions/{code}/claims/{claimId}/dismiss` | – | Clear a claim without awarding it. |
| `GET /sessions/{code}/participants/{id}/card` | – | Inspect a card to verify a claim (FR-38): `{ participantId, name, autoFillOn, cards[] }` with marks. |
| `DELETE /sessions/{code}/participants/{id}` | – | Kick. |
| `GET /sessions/{code}/leaderboard` | – | `{ entries, rounds }`: wins per participant and the round summaries. |
| `POST /sessions/{code}/leaderboard/show` | `{ visible }` | Show or hide the leaderboard on participants' screens. |
| `POST /sessions/{code}/end` | – | End the session; data discarded. |
| `POST /sessions/{code}/display-links` | – | `201 { displayUrl }`: a read-only display link (FR-28), token in the URL fragment. |
| `DELETE /sessions/{code}/display-links` | – | Revoke every display link; open display views disconnect. |
| `POST /sessions/{code}/display-pairings` | `{ pairingCode }` | **Connect a TV** (ADR 0010): approve the code shown on a TV. `204`; the TV receives its display token. `404` unknown or expired code. Rate-limited per host. |
| `GET /sessions/{code}/displays` · `DELETE …/displays/{id}` | – | List connected displays (paired TVs and display links); disconnect one. |

**TV pairing (anonymous, ADR 0010):** `POST /tv/pairings` → `201 { pairingId, pairingCode, expiresAt }` (rate-limited per IP). The TV then connects to `/hubs/pairing?pairingId=…`, which can only deliver `Paired { sessionCode, displayToken }` (or `PairingExpired`) for that pairing, and then closes.

Future (Serious Bingo): `POST /sessions/{code}/cards/generate` → `{ claimId, claimUrl }`; `POST /claims/{claimId}/redeem`.

## 4. SignalR hub

Endpoint: `/hubs/game?code={code}&access_token={token}` (a browser can't set headers on a WebSocket; other clients may send `Authorization: Bearer` instead). WebSockets only, with the Redis (Valkey) backplane. The hub is **server to client only**: clients can call nothing on it. On connect the server adds the caller to the group for its role, `s:{code}:players`, `s:{code}:host` or `s:{code}:display`, and a player also to their own group, `s:{code}:p:{participantId}`, for private notices. A token for another game, a made-up token or a revoked one cannot connect.

Two kinds of message (ADR 0002, amended):

- **`event { type, seq, data }`**: a session event. `seq` counts within the receiver's own stream (players, host or display), so it never skips; a gap means a missed event, and the client refetches the snapshot, whose `game.seq` is the stream position to continue from.
- **`notice { type, data }`**: a private message to one player or the host, unnumbered and safe to apply twice.

In the table, "all" means all three streams.

| Event | Audience | Payload |
|-------|----------|---------|
| `SettingsChanged` | all | host: every setting (as in `PUT /rules`); players and the display: only the settings they notice |
| `WinConditionChanged` | all | `{ winConditionId }` |
| `RulesUpdated` | all | `{ changes: [{ rule, value }], nextBallFrom }`: the "Rules updated" notice (FR-47). `rule` is a settings wire name or `winCondition`; clients word it ("Pattern changed to Blackout"). Raised only mid-round, and only for changes players notice. *Grab ball* and *Auto mode* answer 409 until `nextBallFrom` |
| `RoundCountdownStarted` | all | `{ round, endsAt, startInAuto, winConditionId }`: clients count down locally to `endsAt` in the last-ball slot and show the round-start notice with the pattern (FR-47) |
| `RoundCountdownCancelled` | all | `{ round }` |
| `RoundStarted` | all | `{ round }` |
| `CardsCleared` | all | `{ redraw }` (players with new cards also get the `CardReplaced` notice) |
| `RoundEnded` | all | `{ round }` |
| `BallCalled` | host and display always; players if *Show ball*, *Highlight matching square* or *Show call board* is on | `{ call: { sequence, number, letter, calledAt, source }, previousNumber }` |
| `AutoCallingStarted` / `AutoCallingPaused` | host | – (the room sees no difference; there is no paused banner) |
| `WinClaimed` | host | `{ claim: { id, participantId, name, cardId, winConditionId, claimedAt, matches, alreadyAwarded, awardable, status } }` |
| `ClaimDismissed` | host | `{ claimId }` |
| `WinDetected` | host | `{ participantId, name, cardId, winConditionId, matches }` |
| `WinAwarded` | all | host: `{ win }` in full; players and the display: `{ name, winCondition, wins }`, shown as "Sam had Bingo!" |
| `ParticipantRemoved` | all | host: `{ participantId, name }`; players and the display: `{ name }`, shown as "Sam was removed from the game" |
| `LeaderboardVisibilityChanged` | all | `{ visible }` (wins also change the leaderboard; clients update it from `WinAwarded` or the snapshot) |
| `SessionEnded` | all | – |

| Notice | To | Payload |
|--------|----|---------|
| `ParticipantJoined` | host | `{ participantId, name }` |
| `AutoFillApplied` | that player | `{ cells: [{ cardId, cell }] }` |
| `CardReplaced` | that player | `{ cardId, cells }` |

## 5. Conventions

- `Idempotency`: draw responses include `sequence`; clients ignore events with a `seq` they have already applied in their stream.
- Validation failures: 400 (shape) or 422 (rule violation, e.g. marking an uncalled number with *Free marking* off); state violations: 409.
- Domain commands return a `Result` rather than throwing for expected refusals; each `DomainErrorCode` maps to one status: `Unauthorized` → 401, `Forbidden` → 403, `NotFound` → 404, `WrongState` → 409, `HopperEmpty` → 409, `SessionFull` → 409, `Conflict` → 409 (a name already taken, or a player's change that kept losing a race with other writes: "please try again"), `NotAllowed` → 422, `Invalid` → 422. The `message` is safe to show to players.
- **Rate limits** (config section `RateLimits`; counted per API instance), answered with 429, `Retry-After` where known, and a problem with `code: "TooManyRequests"`:
  - **Wrong game passwords:** reaching 60 within a minute, per game and IP address, **locks joining that game from that address for 10 minutes** (the right password too, or guessing could continue; `Retry-After` says how long). Only *wrong* passwords count, and the limit is high, because a whole event (up to 100 people) or class can share one WiFi address and children mistype; going past it means a bot or something broken.
  - **Creating games:** 30 per IP address per hour.
  - **Marks, Undo and Bingo presses:** per player token, bursts of 10, refilling at 5 a second.
  - Behind a reverse proxy (Caddy, CloudFront) the client address must come from forwarded headers, configured with the trusted proxies (roadmap Phase 1b); until then every caller shares the proxy's address.
- **`Idempotency-Key`** on `POST /wins`: an award repeated with the same key (up to 100 characters) changes nothing, so a double-clicked award for a shouted Bingo counts once. The game remembers its last 100 keys.
- **OpenAPI:** the API describes itself at `GET /openapi/v1.json` (OpenAPI 3.1), from which the web app's typed client is generated.
- Authentication: a missing, unknown or revoked token, or a token for a different game, is 401; a valid token without the right role is 403 (the application layer decides roles). A game whose lock stays taken longer than 5 s answers 503 "The game is busy. Please try again."
- Problem bodies carry `detail` (the message to show) and `code` (the `DomainErrorCode` name) for clients to branch on.
- Commands answer `204` unless they return data: marks and Undo `{ marks }` (the 25-bit mask), *Grab ball* and *Auto mode* `{ number, letter, sequence }`, *Start Round N* `{ round, endsAt }`, auto-fill `{ on }`, redraw `{ cardId, cells }`, rules `{ settings, winCondition }`.
- Settings on the wire: `autoCallIntervalSeconds`, `showBallToPlayers`, `highlightMatches`, `showCallBoard`, `autoFillAvailable`, `winHighlight`, `winDetection`, `redrawPolicy`, `freeMarking`, `requireMarksForClaims`, `unmarking`, `freeSpace`, `showRoundNumbers`, `multipleWins`. Enums are sent as names (`"MismarksOnly"`). In `PUT /rules` every settings field is optional and only those sent change, applied under the session lock to the current settings. `winCondition` is one of `normal`, `corners`, `B`, `I`, `N`, `G`, `O`, `blackout`; left out, the pattern stays.

