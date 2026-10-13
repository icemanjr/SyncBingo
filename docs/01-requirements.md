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
| FR-2 | A host creates a session by choosing a **game password**. The system generates a random **game code** (16 characters, shown as four hyphenated groups of four) and a **QR code** that opens the join page with the code pre-filled. |
| FR-3 | Participants join by scanning the QR code **or** typing the game code. Both paths require the game password. The password is never embedded in the QR code. |
| FR-4 | After the password check, the participant enters a **name**. Names are unique within the session, compared case-insensitively. A duplicate name is rejected and the participant must choose another. |
| FR-5 | Participants can join **only between rounds**. A join attempt during a round shows a friendly "round in progress" message. |
| FR-6 | On join the server issues a **session token**; the client stores it and uses it to rejoin transparently after a sleep, refresh or connection drop. |
| FR-7 | The host can **remove (kick)** a participant. |

### 3.2 Host settings (changeable between rounds; most also mid-round)

| ID | Setting | Options |
|----|---------|---------|
| FR-10 | **Caller mode** | *Auto*: the app draws on a configurable interval. *Manual*: the host draws and calls the number aloud; the new ball appears on the host's screen. |
| FR-11 | **Grab a new ball** | Host button that draws immediately in either mode, including while paused. |
| FR-12 | **Show ball to participants** | On/off. When on, the latest ball appears in the corner of each participant's screen. |
| FR-13 | **Highlight matching square** | On/off. When on, a called number is highlighted on cards that contain it. |
| FR-14 | **Show recent calls** | On/off. Board of called numbers visible to participants. |
| FR-15 | ~~Host also participates~~ | **Removed** (2026-10-12): a card plus the game controls would clutter the host console. A host who wants to play joins from another device or tab as a regular participant. |
| FR-16 | **Auto-fill available** | On/off. When on, each participant may switch auto-fill on **for their own card**; it is never applied to everyone. For a participant who has it on, a missed number is filled in once the *next* number is called, so they get one full call interval to mark it themselves. The last ball of a round is therefore never auto-filled. |
| FR-17 | **Bingo button** | On/off. Lets a participant claim a win (FR-37a). When off, players shout "Bingo!" and the host checks the card (FR-38). |
| FR-18 | **Win highlight** | On/off. Celebratory display on the winner's screen (and optionally everyone's) so young children notice a win. |
| FR-19 | **Win detection** | *Off* (serious mode: players must recognise their own win), *Alert host* (host is told, by name, whose card has a winning pattern of **called** numbers, whether or not the player has noticed or marked it), *Alert host and pause* (the same, and the game pauses until the host resumes). Detection only alerts; a win still needs a claim and the host's award. |
| FR-20 | **Card redraw policy** | *Host controls* redraws, or *participants may pick a new card* between rounds. |
| FR-22 | **Free marking** | On (default) / off. *On:* participants may mark any square, including numbers not yet called ("mismarks"); careless marking can cost a player the game, which is part of the challenge for older players. *Off* (for young children): only called numbers can be marked. |
| FR-24 | **Allow unmarking** | On (default) / off, independent of *Free marking*. *On:* participants may unmark a square they marked, e.g. after a mis-tap. *Off:* a mark stays once made. The free cell can never be unmarked. |
| FR-23 | **Require marks for claims** | Off (default) / on. *Off:* a claim is valid when every cell of the pattern on the card has been called. *On:* those cells must also be marked by the player. |
| FR-21 | **Win condition** | Normal Bingo (any row, column or diagonal), Four Corners, letters **B**, **I**, **N**, **G**, **O** (the letter drawn on the card), Blackout. |

### 3.3 Running a game

| ID | Requirement |
|----|-------------|
| FR-30 | The host can **pause and resume** at any time (e.g. a winner walks to the front for a prize). While paused the auto-call timer freezes and participants may still mark numbers already called. |
| FR-31 | The game **never stops automatically** just because someone may have won, unless the host enabled *Alert host and pause*. |
| FR-32 | The host can **change the win condition mid-round** (e.g. Normal → Blackout) and continue calling with the same balls and marks. |
| FR-33 | After a win the host can: continue for more winners, change the win condition, or **Clear cards** to start a new round. |
| FR-34 | **Clear cards** ends the round, clears all marks, returns all balls to the hopper, and optionally redraws every card. This starts a new round. |
| FR-35 | The host can **end a round early**. Between rounds, joining and (per FR-20) card redraw are available. |
| FR-36 | Participants mark squares themselves, following *Free marking* (FR-22) and *Allow unmarking* (FR-24). The server stores exactly what each participant marked, and always judges wins against the balls actually called. |
| FR-37 | Ties: the first to claim may be recorded as the winner, but the host decides who is credited (see assumption A1). |
| FR-37a | **Every Bingo press reaches the host**, valid or not, like a shout the caller must hear. The host sees the name, the time, and whether the card currently qualifies (FR-23). The player only sees that their Bingo was sent to the host. The host can award the win, dismiss the claim, or remove (FR-7) a player who keeps calling false Bingos. |
| FR-38 | The host can **inspect a participant's card** to verify a claimed win. |
| FR-39 | **No two cards in play are identical.** Within a session, no two cards held at the same time have the same number in every cell. This covers cards dealt on join, host redraws at round start, participant redraws and (future) extra cards. A card that is replaced, or whose holder is removed, stops counting. |

### 3.4 Statistics

| ID | Requirement |
|----|-------------|
| FR-40 | The session tracks, for the current session only: rounds played, win condition(s) used in each round, who won each round, total winners. |
| FR-41 | A **leaderboard** (wins per participant) is available to the host between rounds and at the end, so the host can announce the biggest winners. The host may display it to everyone. |
| FR-42 | Nothing persists after the session ends or expires. |

### 3.5 Advertising

| ID | Requirement |
|----|-------------|
| FR-50 | Every page reserves fixed-size **banner slots**: top and bottom on mobile; left and right on desktop. |
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
| A4 | Starting a round begins in *Running*; in Auto mode the first ball arrives after one interval, or immediately via "Grab a new ball". |
| A5 | ~~A host who is also a participant appears on the leaderboard.~~ Superseded by the removal of FR-15: a host who plays joins as a regular participant and appears on the leaderboard like anyone else. |
| A6 | "Grab a new ball" while paused is an explicit host action and is allowed. |
| A7 | Default max participants per session is 100; default session idle expiry is 4 hours, with a hard cap of 12 hours on total session lifetime. |
| A8 | The free centre space is always marked and counts toward every pattern that includes it. |
