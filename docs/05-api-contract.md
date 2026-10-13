# 05 — API Contract

Base path `/api`. JSON everywhere. Errors use RFC 7807 `application/problem+json`. Auth is a bearer **session token** (host or participant). Commands are REST; the SignalR hub only pushes events.

## 1. Public endpoints

| Method & path | Body | Success | Errors |
|---------------|------|---------|--------|
| `POST /sessions` | `{ password, settings? }` | `201 { code, joinUrl, hostToken, snapshot }` | 400 invalid password/settings |
| `GET /sessions/{code}/join-info` | – | `200 { acceptingJoins, round }` | 404 unknown code |
| `POST /sessions/{code}/join` | `{ password, name }` | `200 { participantId, token, snapshot }` | 401 wrong password · 409 name taken · 409 round in progress · 404 · 429 rate-limited |

`joinUrl` is `{origin}/join/{code}`, which is what the QR encodes. The password is never in the URL.

## 2. Participant endpoints (participant or host token)

| Method & path | Body | Notes |
|---------------|------|-------|
| `GET /sessions/{code}/snapshot` | – | Full state for the caller (the call board as groups of items, docs/06; own cards, settings visible to them, called balls if the participant may see them; see `BallCalled` below). Used on load, reconnect and gap detection. |
| `POST /sessions/{code}/marks` | `{ cardId, cell, marked }` | Rejects (422) marking an uncalled number when *Free marking* is off, and an unmark that *Unmarking* does not allow (off, or a called square under *Mismarks only*). Returns updated marks. |
| `POST /sessions/{code}/cards/{cardId}/undo` | – | Undo (FR-25): resets the card to its marks when the last ball was called. Always allowed during a round. Returns updated marks. |
| `POST /sessions/{code}/claims` | `{ cardId }` | Bingo button. `202 { claimedAt }`: the claim always goes to the host (FR-37a); the player is not told whether it qualifies. |
| `PUT /sessions/{code}/me/auto-fill` | `{ on }` | Switch auto-fill for the caller's own card; 409 unless the host made it available. |
| `POST /sessions/{code}/cards/redraw` | `{ cardId }` | Only when policy allows and state is BetweenRounds. |

## 3. Host endpoints (host token)

| Method & path | Body | Notes |
|---------------|------|-------|
| `PUT /sessions/{code}/settings` | partial settings | Validates per state (§ 03). |
| `POST /sessions/{code}/rounds` | `{ redrawCards: bool }` | Start next round / Clear cards. |
| `POST /sessions/{code}/rounds/current/end` | – | End round early. |
| `POST /sessions/{code}/auto` | – | Auto mode: draws a ball at once and starts the timer (409 unless in manual mode). |
| `POST /sessions/{code}/pause` | – | Back to manual mode (409 unless in auto mode). |
| `POST /sessions/{code}/balls` | – | "Grab ball" (manual mode only; 409 in auto mode). Returns the ball and `sequence`. |
| `PUT /sessions/{code}/win-condition` | `{ condition }` | `normal`, `corners`, `B`, `I`, `N`, `G`, `O`, `blackout`. |
| `POST /sessions/{code}/wins` | `{ participantId, cardId? }` | Award win for the current win condition (idempotent). |
| `POST /sessions/{code}/claims/{claimId}/dismiss` | – | Clear a claim without awarding it. |
| `GET /sessions/{code}/participants/{id}/card` | – | Inspect a card to verify a claim. |
| `DELETE /sessions/{code}/participants/{id}` | – | Kick. |
| `GET /sessions/{code}/leaderboard` | – | Wins per participant + round summaries. |
| `POST /sessions/{code}/leaderboard/show` | `{ visible }` | Show or hide the leaderboard on participants' screens. |
| `POST /sessions/{code}/end` | – | End the session; data discarded. |
| `POST /sessions/{code}/display-links` | – | `201 { displayUrl }`: a read-only display link (FR-28), token in the URL fragment. |
| `DELETE /sessions/{code}/display-links` | – | Revoke every display link; open display views disconnect. |
| `POST /sessions/{code}/display-pairings` | `{ pairingCode }` | **Connect a TV** (ADR 0010): approve the code shown on a TV. `204`; the TV receives its display token. `404` unknown or expired code. Rate-limited per host. |
| `GET /sessions/{code}/displays` · `DELETE …/displays/{id}` | – | List connected displays (paired TVs and display links); disconnect one. |

**TV pairing (anonymous, ADR 0010):** `POST /tv/pairings` → `201 { pairingId, pairingCode, expiresAt }` (rate-limited per IP). The TV then connects to `/hubs/pairing?pairingId=…`, which can only deliver `Paired { sessionCode, displayToken }` (or `PairingExpired`) for that pairing, and then closes.

Future (Serious Bingo): `POST /sessions/{code}/cards/generate` → `{ claimId, claimUrl }`; `POST /claims/{claimId}/redeem`.

## 4. SignalR hub

Endpoint: `/hubs/game?access_token={token}`. WebSockets only. On connect the server adds the caller to the group for its role: `s:{code}:players`, `s:{code}:host` or `s:{code}:display`. In the table below, "all" means all three groups. A display token may call only the snapshot endpoint, which returns public state (board, last ball, win condition, round, state; the leaderboard when shown).

Every event includes `seq` (session event counter). Events are hints; the snapshot endpoint is authoritative.

| Event | Audience | Payload |
|-------|----------|---------|
| `SessionStateChanged` | all | `{ state, round, version }` |
| `SettingsChanged` | all | settings visible to participants |
| `WinConditionChanged` | all | `{ condition }` |
| `RoundStarted` | all | `{ round, cards[] for self }` (own cards only) |
| `RoundEnded` | all | `{ round, winners[] }` |
| `BallCalled` | host and display always; participants if *Show ball*, *Highlight matching square* or *Show recent calls* is on | `{ number, letter, sequence }` |
| `AutoFillApplied` | affected participant | `{ cardId, cells[] }` |
| `AutoCallingStarted` / `AutoCallingPaused` | host | – (the room sees no difference; there is no paused banner) |
| `ParticipantJoined` / `ParticipantLeft` / `ParticipantKicked` | host (kicked: that participant) | `{ id, name }` |
| `WinDetected` | host | `{ participantId, name, cardId, condition }` |
| `WinClaimed` | host | `{ claimId, participantId, name, cardId, condition, claimedAt, qualified }` |
| `WinAwarded` | all if *Win highlight* is on, otherwise host and winner | `{ participantId, name, condition }` |
| `LeaderboardUpdated` | host; all when shown | `{ entries[] }` |
| `CardReplaced` | that participant | `{ card }` |
| `SessionEnded` | all | – |

## 5. Conventions

- `Idempotency`: draw responses include `sequence`; clients ignore events with a `seq` they have already applied.
- Validation failures: 400 (shape) or 422 (rule violation, e.g. marking an uncalled number with *Free marking* off); state violations: 409.
- Domain commands return a `Result` rather than throwing for expected refusals; each `DomainErrorCode` maps to one status: `WrongState` → 409, `HopperEmpty` → 409, `SessionFull` → 409, `NotAllowed` → 422, `Invalid` → 422. The `message` is safe to show to players.
- Rate limits: join endpoint per code + IP; marks per participant.
- OpenAPI is generated from the API project and used to produce the typed TypeScript client.
