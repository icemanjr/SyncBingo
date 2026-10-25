# 05 — API Contract

Base path `/api`. JSON everywhere. Errors use RFC 7807 `application/problem+json`. Auth is a bearer **session token** (host or participant). Commands are REST; the SignalR hub only pushes events.

## 1. Public endpoints

| Method & path | Body | Success | Errors |
|---------------|------|---------|--------|
| `POST /sessions` | `{ password, settings?, winCondition? }` | `201 { code, joinUrl, hostToken, snapshot }` | 400 malformed body · 422 password not 4–64 characters, or invalid settings |
| `GET /sessions/{code}/join-info` | – | `200 { acceptingJoins, round }` | 404 unknown code |
| `POST /sessions/{code}/join` | `{ password, name }` | `200 { participantId, token, snapshot }` | 401 wrong password · 429 too many wrong passwords from this address (§5) · 409 name taken · 409 "A round is in progress. You can join between rounds." (during a countdown too) · 409 "No unique cards available: all 72 possible cards are in use…" (a small grouped set, FR-39) · 404 |

`joinUrl` is `{origin}/join/{code}`, which is what the QR encodes. The password is never in the URL.

## 2. Participant endpoints (participant token)

The snapshot also accepts a host or display token. Every other endpoint here acts on the caller's own record, taken from the token: the routes carry no participant id, and a `cardId` is looked up only among the caller's own cards, so no request can reach another player's card (an unknown `cardId` is 422). A host or display token gets 403; a player the host removed gets 404.

| Method & path | Body | Notes |
|---------------|------|-------|
| `GET /sessions/{code}/snapshot` | – | The caller's view: `{ game, you, host }`. **`game`** (everyone): code, state, round and upcoming round, countdown end, pattern, the variant's **patterns as data** `{ id, name, masks[] }` (bit *i* = cell *i*; FR-48), the card shape `{ rows, columns, freeCell }`, **`freeText`** (what the free space says: a set's own text, or "FREE"), **`cardSizes`** (each size the host can choose, `{ size, rows, columns, patterns }`, so Options lists the right patterns for a size; FR-26a), the call board as groups of balls, the calls (`null` for a player unless *Show ball*, *Highlight matching square* or *Show call board* is on), balls remaining, `nextBallFrom` (FR-47), the settings players notice, the leaderboard (`null` for players and the display until the host shows it) and `leaderboardVisible`, `version` and `seq`, and **`content`** with a custom set (ADR 0009; `null` for numbers): `{ name, groupItems, board, squares }` (`groupItems`: the set has groups, so it is dealt by group), where `board[n − 1]` is item n's ball text and `squares` every square's text in alphabetical order (to size text for Max). **Never the answer key:** no ball is paired with its square. Calls (and `POST /balls`) carry their own `call` and `square`. **`you`** (a player): own id, name, auto-fill switch and cards `{ cardId, cells, marks, faces }`, never anyone else's. With a custom set, `faces` is each square's text and `cells` only placeholders (cell position + 1, the free cell 0), never item numbers, which with the board's ball text would pair clues with squares; the app knows a square was called by matching its text to called balls' squares. The host's card inspection gets the real items and faces. `CardReplaced` notices carry placeholders too. **`host`** (the host): every setting, the players `{ participantId, name, joinedAt }`, open claims this round and the round summaries. Create and join return the same snapshot. Used on load, reconnect and gap detection. |
| `POST /sessions/{code}/marks` | `{ cardId, cell, marked }` | Rejects (422) marking an uncalled number when *Free marking* is off, and an unmark that *Unmarking* does not allow (off, or a called square under *Mismarks only*). Returns updated marks. |
| `POST /sessions/{code}/cards/{cardId}/undo` | – | Undo (FR-25): resets the card to its marks when the last ball was called. Always allowed during a round. Returns updated marks. |
| `POST /sessions/{code}/claims` | `{ cardId }` | Bingo button. `202 { claimedAt }`: the claim always goes to the host (FR-37a); the player is not told whether it qualifies. |
| `PUT /sessions/{code}/me/auto-fill` | `{ on }` | Switch auto-fill for the caller's own card; 409 unless the host made it available. |
| `DELETE /sessions/{code}/me` | – | Leave the game (FR-53): 204; pending claims dismissed, token revoked, name and card released. Everyone gets `ParticipantLeft { name }`. |
| `POST /sessions/{code}/cards/redraw` | `{ cardId }` | Only when policy allows and state is BetweenRounds. |

## 3. Host endpoints (host token)

| Method & path | Body | Notes |
|---------------|------|-------|
| `PUT /sessions/{code}/rules` | `{ settings?, winCondition? }` | **Apply** in Options: the settings sent (only those fields change) and the pattern, in one step. `200 { settings, winCondition }`. 409 in auto mode ("Pause before changing the rules.") and during the countdown; 422 for a lobby-only setting (free space, redraw policy, multiple wins) during a round, with nothing applied. Changing the card size (FR-26a), or the free space to or from *None*, changes the card itself, so every player is dealt a new card (`CardReplaced`) and the last round's calls are cleared; 422 if the custom set in use doesn't fill that card, if the chosen pattern doesn't exist on that card ("A 3 × 3 card has no Letter B"), or for a grouped set's card size (changed in the set editor). |
| `POST /sessions/{code}/rounds/countdown` | `{ startInAuto: bool }` | *Start Round N*: begins the 10 s countdown (FR-45); 409 unless in the lobby. |
| `DELETE /sessions/{code}/rounds/countdown` | – | Cancel the countdown; back to the lobby. |
| `POST /sessions/{code}/cards/clear` | `{ redrawCards: bool }` | *Clear cards*: ends any running round, clears marks and calls, optional redraw, back to the lobby (FR-34); 409 during a countdown. |
| `PUT /sessions/{code}/content?cardSize=` | the set file itself (JSON, ADR 0009) | *Use this set* (FR-44): the editor's whole draft or an uploaded file; a set with groups is dealt by group, one per card column. The optional `cardSize` (`FiveByFive`, `FourByFour`, `ThreeByThree`) changes the card in the same step (FR-26a). Checked in full (limits, unique sides, fits the card), then used: clears the last round's calls and deals every player a new card (`CardReplaced`), and raises `ContentChanged`. Lobby only (409 otherwise); 422 with what to fix; 413 above 128 KB. 204 |
| `DELETE /sessions/{code}/content` | — | Back to numbers: the standard balls and a new card for everyone. Lobby only. 204 |
| `GET /sessions/{code}/content` | — | Downloads the set in use as its file (`application/json`, named after the set): the host any time; a **player** only when the host shares it and only between rounds (403 otherwise, saying why). 409 when the game uses numbers (not 404, which clients read as "the game is over") |
| `PUT /sessions/{code}/content/sharing` | `{ playersMayDownload: bool }` | *Players can download this set* (ADR 0009): off by default and off again when the set changes; any state but Ended. Raises `ContentSharingChanged` to everyone; the snapshot's `content.playersMayDownload` says it. 204 |
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
| `POST /sessions/{code}/display-links` | – | `201 { displayId, displayUrl }`: a read-only display link (FR-28), `{origin}/display/{code}#t={token}`. The token is in the URL fragment, never the query, so it never reaches a server log. At most 10 displays per game (409 beyond). |
| `DELETE /sessions/{code}/display-links` | – | Revoke every display link (paired TVs stay); each open display gets `DisplayDisconnected` and its token stops working. |
| `PUT /sessions/{code}/join-note` | `{ note }` | The note shown with the join QR (FR-49), up to 80 characters; empty or `null` clears it. Any state but ended, even auto mode. |
| `PUT /sessions/{code}/board-side` | `{ side: "Ball" \| "Square" }` | *Call board shows* (FR-29): which face of a custom set's items every call board shows; any state but Ended; raises `BoardSideChanged` to everyone. The snapshot's `content.board` then holds that face and `content.boardSide` says which. 204 |
| `POST /sessions/{code}/display-pairings` | `{ pairingCode }` | **Connect a TV** (ADR 0010): approve the code shown on a TV, typed in any case, with or without the hyphen. `204`; the TV becomes a display of kind `Tv` and receives its token. `404` unknown, used or expired code; `409` when the game already has 10 displays (the code stays usable). Rate-limited to 10 per minute per host token. |
| `GET /sessions/{code}/displays` · `DELETE …/displays/{id}` | – | List the displays (`[{ displayId, kind: Link \| Tv, createdAt }]`); disconnect one (its token stops working and it gets `DisplayDisconnected`). |

**TV pairing (anonymous, ADR 0010):** `POST /tv/pairings` → `201 { pairingId, pairingCode, expiresAt }`, where the code is shown as `K7M-2QX` and lives 10 minutes, and `pairingId` is a 64-character secret only that TV knows. Rate-limited to 30 per IP per hour. The TV then connects to `/hubs/pairing?pairingId=…` (WebSockets, anonymous, server to client only), which can deliver only that pairing's `Paired { sessionCode, displayToken }`, or `PairingExpired` for an unknown or expired pairing. A result approved before the TV connected is handed over on connect.

Future (Serious Bingo): `POST /sessions/{code}/cards/generate` → `{ claimId, claimUrl }`; `POST /claims/{claimId}/redeem`.

## 4. SignalR hub

Endpoint: `/hubs/game?code={code}&access_token={token}` (a browser can't set headers on a WebSocket; other clients may send `Authorization: Bearer` instead). WebSockets only, with the Redis (Valkey) backplane. The hub is **server to client only**: clients can call nothing on it. On connect the server adds the caller to the group for its role, `s:{code}:players`, `s:{code}:host` or `s:{code}:display`, and a player also to their own group, `s:{code}:p:{participantId}`, for private notices. A token for another game, a made-up token or a revoked one cannot connect.

Two kinds of message (ADR 0002, amended):

- **`event { type, seq, data }`**: a session event. `seq` counts within the receiver's own stream (players, host or display), so it never skips; a gap means a missed event, and the client refetches the snapshot, whose `game.seq` is the stream position to continue from. The host's and the display's snapshots also carry `game.joinNote` (FR-49).
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
| `WinClaimed` | host | `{ claim: { id, participantId, name, cardId, winConditionId, claimedAt, matches, alreadyAwarded, awardable, status, alreadyWon, completed } }`. `completed` lists the selected patterns the card completes (`["rows", "corners"]`) |
| `ClaimDismissed` | host | `{ claimId }` |
| `WinDetected` | host | `{ participantId, name, cardId, winConditionId, matches, completed }` |
| `WinAwarded` | all | host: `{ win }` in full; players and the display: `{ name, winCondition, wins, announced }`. `announced` is a pattern id, or null; clients show "Sam had a Bingo!" when it is null, else that pattern's `announcedAs` ("Sam had Four Corners!") |
| `ParticipantRemoved` | all | host: `{ participantId, name }`; players and the display: `{ name }`, shown as "Sam was removed from the game" |
| `ParticipantLeft` | all | host: `{ participantId, name }`; players and the display: `{ name }`, shown as "Sam left the game" (FR-53) |
| `LeaderboardVisibilityChanged` | all | `{ visible }` (wins also change the leaderboard; clients update it from `WinAwarded` or the snapshot) |
| `SessionEnded` | all | – |
| `JoinNoteChanged` | host and display | `{ note }` (FR-49; `null` when cleared) |

| Notice | To | Payload |
|--------|----|---------|
| `ParticipantJoined` | host | `{ participantId, name }` |
| `WrongPasswordsAlert` | host | `{ wrongInLastMinute, lockedFor }`: many wrong passwords (the shared password may be wrong), sent at the warning level (10) and again if joining from one address gets locked (`lockedFor`, e.g. `"00:10:00"`) |
| `AutoFillApplied` | that player | `{ cells: [{ cardId, cell }] }` |
| `CardReplaced` | that player | `{ cardId, cells }` |
| `DisplayDisconnected` | that display | – (the host disconnected it; the page stops and returns to its start screen) |

## 5. Conventions

- `Idempotency`: draw responses include `sequence`; clients ignore events with a `seq` they have already applied in their stream.
- JSON numbers are strict: `7`, never `"7"` (so the OpenAPI types are plain integers for the typed web client).
- Validation failures: 400 (shape) or 422 (rule violation, e.g. marking an uncalled number with *Free marking* off); state violations: 409.
- Domain commands return a `Result` rather than throwing for expected refusals; each `DomainErrorCode` maps to one status: `Unauthorized` → 401, `Forbidden` → 403, `NotFound` → 404, `WrongState` → 409, `HopperEmpty` → 409, `SessionFull` → 409, `Conflict` → 409 (a name already taken, or a player's change that kept losing a race with other writes: "please try again"), `NotAllowed` → 422, `Invalid` → 422. The `message` is safe to show to players.
- **Rate limits** (config section `RateLimits`; counted per API instance), answered with 429, `Retry-After` where known, and a problem with `code: "TooManyRequests"`:
  - **Wrong game passwords:** reaching 60 within a minute, per game and IP address, **locks joining that game from that address for 10 minutes** (the right password too, or guessing could continue; `Retry-After` says how long). Only *wrong* passwords count, and the limit is high, because a whole event (up to 100 people) or class can share one WiFi address and children mistype; going past it means a bot or something broken. The host is alerted at 10 wrong passwords within a minute and again on a lockout (`WrongPasswordsAlert`, §4), since the password shared with the room may not be the game's.
  - **Creating games:** 30 per IP address per hour.
  - **Marks, Undo and Bingo presses:** per player token, bursts of 10, refilling at 5 a second.
  - **TV pairings:** 30 started per IP address per hour; 10 approvals per minute per host token.
  - **Custom set changes** (use a set, or back to numbers): 30 per host token per hour, since each one redeals every card.
  - Behind a reverse proxy (Caddy, CloudFront) the client address must come from forwarded headers, configured with the trusted proxies (roadmap Phase 1b); until then every caller shares the proxy's address.
- **`Idempotency-Key`** on `POST /wins`: an award repeated with the same key (up to 100 characters) changes nothing, so a double-clicked award for a shouted Bingo counts once. The game remembers its last 100 keys.
- **OpenAPI:** the API describes itself at `GET /openapi/v1.json` (OpenAPI 3.1), from which the web app's typed client is generated.
- Authentication: a missing, unknown or revoked token, or a token for a different game, is 401; a valid token without the right role is 403 (the application layer decides roles). A game whose lock stays taken longer than 5 s answers 503 "The game is busy. Please try again."
- Problem bodies carry `detail` (the message to show) and `code` (the `DomainErrorCode` name) for clients to branch on.
- Commands answer `204` unless they return data: marks and Undo `{ marks }` (the 25-bit mask), *Grab ball* and *Auto mode* `{ number, letter, sequence }`, *Start Round N* `{ round, endsAt }`, auto-fill `{ on }`, redraw `{ cardId, cells }`, rules `{ settings, winCondition }`.
**Win patterns (FR-21, ADR 0004 amended).** `winCondition` is a **selection key**: pattern ids joined by `+` in the variant's order, e.g. `rows+columns+diagonals+corners`. Requests may send ids in any order, and a preset id stands for its patterns (`normal`). Responses and events always carry the canonical key. The snapshot's `game.patterns` are `{ id, name, masks, isLine, announcedAs }`, and `game.presets` are `{ id, name, patternIds }` (Normal Bingo = rows, columns, diagonals), so clients name a selection ("Normal Bingo or Four Corners") from data.
- Settings on the wire: `autoCallIntervalSeconds`, `showBallToPlayers`, `highlightMatches`, `showCallBoard`, `autoFillAvailable`, `winHighlight`, `winDetection`, `redrawPolicy`, `freeMarking`, `requireMarksForClaims`, `unmarking`, `freeSpace`, `showRoundNumbers`, `multipleWins`. Enums are sent as names (`"MismarksOnly"`). In `PUT /rules` every settings field is optional and only those sent change, applied under the session lock to the current settings. `winCondition` is one of `normal`, `corners`, `B`, `I`, `N`, `G`, `O`, `blackout`; left out, the pattern stays.

