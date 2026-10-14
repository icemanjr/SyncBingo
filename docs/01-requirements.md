# 01 — Requirements

## 1. Product summary

Nobody brings bingo cards or balls to a family party, but everyone has a phone. The host creates a session, everyone joins, and the app deals cards, calls numbers and tracks wins. Options exist to make it forgiving for small, easily distracted children (auto-fill, win celebration, ball shown on screen) and to turn all of that off for adults who want to pay attention ("serious" play).

## 2. Principles

- **Phone-first**, big tap targets, readable at a glance.
- **No accounts, no personal data.** Everything lives only for the duration of the session.
- **Host is in control** of pace, pausing, rules and who is recorded as a winner.
- **Server-authoritative.** Clients never decide what was called, what is marked, or who won.
- **Rules are data.** The standard 75-ball game is one configuration of a variant model, so other variants can be added later.

## 3. Functional requirements

### 3.1 Landing and joining

| ID | Requirement |
|----|-------------|
| FR-1 | Landing page offers **Host** and **Join**, plus a greyed-out **"Serious Bingo — coming soon"** tile (non-functional, signals planned expansion). |
| FR-2 | A host creates a session by choosing a **game password** (4–64 characters, no complexity rules: it only stops someone who mistypes a code from joining the wrong game, and hosts will often use the event's name). Each game has **exactly one host**: the host token is issued once, at creation, and there are no co-hosts. The system generates a random **game code** (16 characters, shown as four hyphenated groups of four) and a **QR code** that opens the join page with the code pre-filled. |
| FR-3 | Participants join by scanning the QR code **or** typing the game code. Both paths require the game password. The password is never embedded in the QR code. |
| FR-4 | After the password check, the participant enters a **name**. Names are unique within the session, compared case-insensitively. A duplicate name is rejected and the participant must choose another. |
| FR-5 | Participants can join **only between rounds**. A join attempt during a round's countdown or the round itself is refused with **"A round is in progress. You can join between rounds."** |
| FR-6 | On join the server issues a **session token**; the client stores it and uses it to rejoin transparently after a sleep, refresh or connection drop. |
| FR-7 | The host can **remove (kick)** a participant. |

### 3.2 Host settings (changeable between rounds; most also mid-round)

The host changes settings and the win pattern in **Options**, as a draft, and presses **Apply** to change them all at once. Options is available only in the lobby and in manual mode: never in auto mode or during the round countdown, so no ball is called and no round starts while the host announces new rules.

| ID | Setting | Options |
|----|---------|---------|
| FR-10 | **Calling: manual and auto** | Every round starts in **manual** mode: the host presses **Grab ball** for each ball and calls it aloud. Pressing **Auto mode** draws a ball **immediately**, then the app draws one every *auto-call interval* (a setting, 3–120 s). In auto mode the host's only calling button is **Pause**, which returns to manual mode; to speed up or slow down, the host pauses, changes the interval, and presses Auto mode again. When auto mode calls the **last ball**, it returns to manual mode by itself: nothing is left to call. |
| FR-11 | **Grab ball** | Host button that draws immediately; shown in manual mode only (FR-10). |
| FR-12 | **Show ball to participants** | On/off. When on, the latest ball appears in the corner of each participant's screen. |
| FR-13 | **Highlight matching square** | On/off. When on, a called number is highlighted on cards that contain it. |
| FR-14 | **Show call board** | On/off. Participants can open the call board (FR-27) on their own screen. |
| FR-15 | ~~Host also participates~~ | **Removed** (2026-10-12): a card plus the game controls would clutter the host console. A host who wants to play joins from another device or tab as a regular participant. |
| FR-16 | **Auto-fill available** | On/off. When on, each participant may switch auto-fill on **for their own card**; it is never applied to everyone. For a participant who has it on, a missed number is filled in once the *next* number is called, so they get one full call interval to mark it themselves. The last ball of a round is therefore never auto-filled. |
| FR-17 | **Bingo button** | On/off. Lets a participant claim a win (FR-37a). When off, players shout "Bingo!" and the host checks the card (FR-38). |
| FR-18 | **Win highlight** | On/off. Celebratory display on the winner's screen (and optionally everyone's) so young children notice a win. |
| FR-19 | **Win detection** | *Off* (serious mode: players must recognise their own win), *Alert host* (host is told, by name, whose card has a winning pattern of **called** numbers, whether or not the player has noticed or marked it), *Alert host and pause* (the same, and the game pauses until the host resumes). Detection only alerts; a win still needs a claim and the host's award. |
| FR-20 | **Card redraw policy** | *Host controls* redraws, or *participants may pick a new card* between rounds. |
| FR-21 | **Win condition** | Normal Bingo (any row, column or diagonal), Four Corners, letters **B**, **I**, **N**, **G**, **O** (the letter drawn on the card), Blackout. |
| FR-22 | **Free marking** | On (default) / off. *On:* participants may mark any square, including numbers not yet called ("mismarks"); careless marking can cost a player the game, which is part of the challenge for older players. *Off* (for young children): only called numbers can be marked. |
| FR-23 | **Require marks for claims** | Off (default) / on. *Off:* a claim is valid when every cell of the pattern on the card has been called. *On:* those cells must also be marked by the player. |
| FR-24 | **Unmarking** | *Any* (default) / *Mismarks only* / *Off*, independent of *Free marking*. *Any:* participants may unmark any square they marked. *Mismarks only:* they may unmark squares whose number has **not** been called, so a mismark can be fixed but a correct mark cannot be removed. *Off:* marks are final (except through Undo, FR-25). The free cell follows *Free space* (FR-26). |
| FR-25 | **Undo** (always available) | During a round, a participant can press **Undo** to reset their card to how it was when the **last ball was called** (before the first ball: as dealt, with only the free cell marked). It reverts every mark and unmark made since that ball, whatever the *Unmarking* setting, so an accidental tap can always be fixed until the next ball is called. Auto-fill happens as part of a draw, so it is not undone. Pressing Undo again changes nothing. |
| FR-43 | **Multiple wins** | Changeable between rounds only. *Once per round:* a card that has won is done until the next *Clear cards*. *Once per pattern* (default): one win per card per pattern; changing the pattern allows another. *Every declared win:* the host awards what the player declares, e.g. 2 for a "double Bingo" (one ball completing a row and a column) or 1 for "I got column B!", and later lines in the same round, including one the player only noticed after more balls, can be claimed and awarded too. Claims and detection alerts show how many lines the card completes and how many wins it already got for this pattern; claims also show how many more wins are **awardable** under this setting, and "No awardable matches" when there are none (e.g. a press after a double Bingo was paid, with no third line). The host still decides. |
| FR-44 | **Change the content set between rounds** (Phase 4, ADR 0009). A set is a dictionary of ball/square pairs, optionally with named groups that are saved in the set file; card size, free space and patterns are separate settings, so a new set never makes a pattern unusable. The host chooses whether to use the groups (**Group items**: each card column draws from one group, and the board shows the groups), as long as they fit the current card. A set needs at least 24 pairs (exactly 24 requires the free space), and enough for the current card, otherwise the upload is refused with what to change. In the lobby the host can upload a different set file, which replaces the session's single stored set and redeals every card. The host can download the current set as one file to reuse later; nothing else is stored. |
| FR-26 | **Free space** | *Always marked* (default) / *Earned*. Changeable between rounds only. *Always marked:* the free centre starts marked, counts toward every pattern that includes it, and can never be unmarked (A8). *Earned* (a common house rule): the free centre starts **unmarked** and is earned, for each player separately, by the first called ball that is **not** on their card; it then stays earned for the round. Until it is earned it does not count toward any pattern (so no line through the centre and no Blackout). Once earned it behaves like a called square: the player marks it, auto-fill (if on) fills it one call later, and *Unmarking* and *Undo* apply as normal. |

### 3.2a Call board and display view

| ID | Requirement |
|----|-------------|
| FR-27 | The **call board** shows every ball of the variant, with called balls marked (by shape or icon as well as colour). The **last ball** is shown large, **top centre above the letters** (e.g. "N 37"), so people at the back who misheard a call can check it, and its cell is emphasised on the board. It is the host console's **default view**; rules and settings sit behind an **Options** button. The board is driven entirely by data from the server, as **groups of items** (for standard Bingo the five letters B-I-N-G-O, 15 balls each; for a custom game without columns, a single unlabelled group), so it never assumes 75 balls or five columns. Groups sit side by side and the layout picks the number of rows that gives the largest cells on the screen in use; for standard Bingo that means 3 columns of 5 or 2 columns of 8 + 7 per letter (docs/06). |
| FR-28 | The host can open a **display view**, read-only and without ads, in a separate tab or on another device (projector, TV), using a display link. It shows the call board, the last ball, the win pattern, the round, win celebrations, the leaderboard when the host shows it, and the join QR between rounds; never settings, claims, alerts or controls. Opening it does not disconnect the host console; the host can revoke display links. A **smart TV or projector** can connect on its own, without a cable to the host's device, by **pairing**: its browser opens a short address that shows a short code, and the host enters that code (or scans the TV's QR) under **Connect a TV** (ADR 0010). |
| FR-29 | **Call board shows: ball side (default) / square side.** With custom content (ADR 0009), the host chooses whether the board's cells show each item's **call face** (what is called, e.g. a picture or a definition) or its **card face** (what players mark, e.g. the character's name or the word). The default is **always the ball side**, whatever the content (even large pictures on the ball side and plain numbers on the square side); the board never switches by itself, only the host does. The square side suits content whose ball is bigger than its square. The large last-ball slot always shows the ball side, since that is the call people need to check. Applies to every call board (host, participants, display) and can change at any time. Hidden for standard Bingo, where both sides are the same number. |

### 3.3 Running a game

| ID | Requirement |
|----|-------------|
| FR-30 | The host can **pause** auto mode at any time (e.g. a winner walks to the front for a prize), which simply returns to manual mode: no more automatic calls, participants keep marking, and no "paused" banner is shown to anyone. **Auto mode** resumes, drawing the next ball at once. |
| FR-31 | The game **never stops automatically** just because someone may have won, unless the host enabled *Alert host and pause*. |
| FR-32 | The host can **change the win condition mid-round** (e.g. Normal → Blackout) and continue calling with the same balls and marks. The pattern is changed in Options with the other rules, in manual mode only (pause auto mode first), so the host can announce it ("Now we're playing Blackout!") before the next ball. |
| FR-33 | After a win the host can: continue for more winners, change the win condition, or **Clear cards** to start a new round. |
| FR-34 | **Clear cards** ends the round (if one is running), clears all marks, returns all balls to the hopper, optionally redraws every card, and returns to the lobby. It does not start the next round: late arrivals can join, then the host presses *Start Round N* (FR-45). |
| FR-35 | The host can **end a round early** (*End round*): calling stops and the cards and calls stay on screen. Between rounds, joining and (per FR-20) card redraw are available. |
| FR-36 | Participants mark squares themselves, following *Free marking* (FR-22) , *Unmarking* (FR-24) and *Undo* (FR-25). The server stores exactly what each participant marked, and always judges wins against the balls actually called. |
| FR-37 | Ties: the first to claim may be recorded as the winner, but the host decides who is credited (see assumption A1). |
| FR-37a | **Every Bingo press reaches the host**, valid or not, like a shout the caller must hear. The host sees the name, the time, and whether the card currently qualifies (FR-23). The player only sees that their Bingo was sent to the host. The host can award the win, dismiss the claim, or remove (FR-7) a player who keeps calling false Bingos. |
| FR-38 | The host can **inspect a participant's card** to verify a claimed win. |
| FR-39 | **No two cards in play are identical.** Within a session, no two cards held at the same time have the same number in every cell. This covers cards dealt on join, host redraws at round start, participant redraws and (future) extra cards. A card that is replaced, or whose holder is removed, stops counting. |
| FR-45 | **Start Round N** begins a fixed **10-second countdown**, shown on every screen (host, participants, display) in the last-ball slot, labelled "Round starts in". Joining and redraws close the moment it starts; the host can **Cancel** at any time during it. At zero the round starts. A **Manual / Auto** toggle beside the button (Manual by default) decides how it starts: Auto draws the first ball at zero and keeps calling. There is no countdown-length setting and no "start now" button. |
| FR-46 | **Show round numbers** (on by default). Rounds are the term users see. On: "Start Round 3", "Round 3". Off: "Start New Round", "This round". |

### 3.4 Statistics

| ID | Requirement |
|----|-------------|
| FR-40 | The session tracks, for the current session only: rounds played, win condition(s) used in each round, who won each round, total winners. |
| FR-41 | A **leaderboard** (wins per participant) is available to the host between rounds and at the end, so the host can announce the biggest winners. The host may display it to everyone. |
| FR-42 | Nothing persists after the session ends or expires. |

### 3.5 Advertising

| ID | Requirement |
|----|-------------|
| FR-50 | Every page reserves fixed-size **banner slots**: top and bottom on mobile; left and right on desktop. **Exception:** the display view (FR-28) shows no ads, because it is shown to the whole room. |
| FR-51 | No pop-ups, interstitials, overlays or anything that can cover the play area. |
| FR-52 | Ad rendering sits behind a provider abstraction; the portfolio build shows placeholders. |

## 4. Non-functional requirements

| ID | Requirement |
|----|-------------|
| NFR-1 | **Latency:** a called ball reaches all connected participants within ~500 ms under normal conditions. |
| NFR-2 | **Capacity:** design target of 100 participants per session; many concurrent sessions. |
| NFR-3 | **Resilience:** a participant who loses connection recovers their card, marks and name automatically. |
| NFR-4 | **Privacy:** no accounts, no personal data; passwords and tokens stored hashed; data expires via TTL. |
| NFR-5 | **Security:** server-side validation of all commands, join rate limiting, no cross-participant card leakage. |
| NFR-6 | **Accessibility:** WCAG 2.1 AA targets, ≥48 px tap targets, state never conveyed by colour alone, reduced-motion support, no flashing content. |
| NFR-7 | **Portability:** runs locally with one `docker compose up`; deploys to AWS via CDK. |
| NFR-8 | **Extensibility:** ball range, card size and win patterns are data evaluated by generic code, not hard-coded logic; loading them from configuration comes with custom variants. |

## 5. Out of scope (initially)

Persistent accounts; payments or any handling of money; chat; custom card images/themes; native apps.

## 6. Future: "Serious Bingo"

The app handles **no money**. The host collects payment, then generates a card (new single-use QR that a participant scans to add that card to themselves) or adds a card to a named participant. Cards expire at the end of each round. The MVP data model already allows a participant to hold more than one card (see 04). Note that real-money games are regulated in many jurisdictions; this should be reviewed before any such feature ships.

## 7. Confirmed assumptions

Confirmed by the owner on 2026-10-12.

| # | Assumption |
|---|-----------|
| A1 | **Wins are credited by the host.** Detection and claims raise alerts; the host confirms who is credited, which feeds the leaderboard. |
| A2 | The game code alphabet omits look-alike characters (0/o, 1/i/l) for easier typing; length stays 16 (4×4). |
| A3 | A join attempt during a round is refused with a message (no waiting room in the MVP). |
| A4 | ~~Starting a round begins in *Running*.~~ *Amended 2026-10-12:* every round starts in **manual** mode; nothing is called until the host presses Grab ball or Auto mode (FR-10). *Amended 2026-10-13:* rounds start after a 10-second countdown (FR-45), in manual mode unless the host set the toggle to Auto. |
| A5 | ~~A host who is also a participant appears on the leaderboard.~~ Superseded by the removal of FR-15: a host who plays joins as a regular participant and appears on the leaderboard like anyone else. |
| A6 | "Grab a new ball" while paused is an explicit host action and is allowed. *Amended 2026-10-12:* pausing returns to manual mode, where Grab ball is the normal way to draw; Grab ball is not offered in auto mode. |
| A7 | Default max participants per session is 100; default session idle expiry is 4 hours, with a hard cap of 12 hours on total session lifetime. |
| A8 | The free centre space is always marked and counts toward every pattern that includes it. *Amended 2026-10-12:* this is the default; the host can choose the *Earned* free space instead (FR-26). |
