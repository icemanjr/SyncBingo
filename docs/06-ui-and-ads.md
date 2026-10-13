# 06 — UI, Ad Slots and Accessibility

## 1. Screens

| Screen | Route | Purpose |
|--------|-------|---------|
| Landing | `/` | **Host a game**, **Join a game**, greyed-out **Serious Bingo (coming soon)** |
| Host setup | `/host/new` | Choose game password and initial settings |
| Host console | `/host/:code` | **Call board by default** (FR-27), with QR + game code, last ball, draw/pause/round controls, claims and alerts, participant list, leaderboard. Rules and settings sit behind an **Options** button. **Open display** pops out the display view |
| Display | `/display/:code` | Read-only view for a projector or TV (FR-28): call board, last ball, win pattern, round, win celebration, leaderboard when shown, and the join QR between rounds. No controls and **no ads** |
| Join | `/join/:code` (QR) or `/join` (manual) | Enter game code (if manual) and password |
| Name entry | same flow | Choose a unique name |
| Play | `/play/:code` | Card, latest ball (if shown), Bingo button (if enabled), **Undo** button (always, during a round), own auto-fill switch (if the host made it available), win highlight |
| Leaderboard | overlay/route | Wins per participant; host can show to everyone |

### Host console (sketch)

```
┌─────────────────────────────────────────────┐
│ [Ad: top banner]                            │
├─────────────────────────────────────────────┤
│ 9ve4-shp3-k7mw-a2qx  [QR]   [Open display ⧉]│
│ Round 3 · Normal Bingo · ▶ Auto    [Options]│
├─────────────────────────────────────────────┤
│  Auto: [ Pause ]  Manual: [Grab][Auto mode] │
│  ── call board (FR-27) ──                   │
│               ┌──────┐                      │
│               │ N 37 │ last ball            │
│               └──────┘                      │
│    B       I       N       G       O        │
│  1 ■ 11  16 21 26  ...                      │
│  2 7 12  17 ■ 27                            │
│  ...                                        │
├─────────────────────────────────────────────┤
│ 🔔 Sam: BINGO ✓ 2 lines · 2 awardable       │
│  [Inspect] [Award 1] [Award 2] [Dismiss] [⋯]│
│ Players (14) ▸   [ Clear cards ] [Leaderbd] │
├─────────────────────────────────────────────┤
│ [Ad: bottom banner]                         │
└─────────────────────────────────────────────┘
```

### Participant play screen (mobile sketch)

```
┌───────────────────────┐
│ [Ad: top banner]      │
├───────────────────────┤
│  Normal Bingo   (N 37)│  ← ball corner (if enabled)
│  B  I  N  G  O        │
│ 07 22 38 51 66        │
│ 12 19 41 49 70        │
│ 03 27 ★  58 62        │
│ 15 30 33 47 75        │
│ 09 16 44 60 68        │
│      [ BINGO! ]       │  ← if enabled
├───────────────────────┤
│ [Ad: bottom banner]   │
└───────────────────────┘
```

### Lobby and round countdown (FR-45, FR-46)

Between rounds the host console's main control is the round button, with the start-mode toggle beside it:

```
┌─────────────────────────────────────────────┐
│ Players (14) ▸                [Options]     │
│      [ Start Round 4 ]   Manual ◉ ○ Auto    │
└─────────────────────────────────────────────┘
```

With *Show round numbers* off the button reads **Start New Round**. Pressing it starts a 10-second countdown on every screen, shown where the last ball normally sits:

```
┌──────────┐
│ Round    │
│ starts in│
│    7     │
└──────────┘      host only: [ Cancel ]
```

Clients count down locally to the server's `endsAt`, so there is no message per second. During the countdown, the join page shows "A round is in progress. You can join between rounds." At zero the slot shows "–" until the first ball (manual), or the first ball itself (auto).

### Claim alerts on the host console

Every Bingo press appears as an alert with the player's name, the time, the lines completed and the wins still **awardable** (FR-37a, FR-43). Its buttons follow *Multiple wins*:

| Situation | Buttons |
|-----------|---------|
| *Once per pattern* or *once per round*, awardable 1 | **Inspect**, **Award**, **Dismiss**, ⋯ (**Remove player**) |
| *Every declared win*, awardable *n* ≥ 1 | **Inspect**, **Award 1** … **Award n** (one button per count, so the host taps the number the player declared; a stepper above 4), **Dismiss**, ⋯ |
| Awardable 0 ("No awardable matches") | **Inspect**, **Dismiss**, **Remove player**; *Award anyway* stays under ⋯ because the host decides (ADR 0008) |

Every button keeps the 48 px tap target. An award sends the claim's id, so a double tap counts once.

### Call board (FR-27)

All balls of the variant, called ones marked with a fill **and** a tick (never colour alone). The **last ball** sits large and **top centre, above the letters**, as letter + number ("N 37"), so people at the back who misheard a call can check it; its cell on the board is emphasised too, and before the first call the slot shows "–". The board knows nothing about B-I-N-G-O. It draws whatever **groups of items** the server sends (see *Board data* below). For standard Bingo those are the five letters, which sit **side by side** across the board, and each letter's 15 numbers form a small block under it, read top to bottom, then left to right:

```
                 ┌──────┐                              ┌──────┐
                 │ N 37 │  last ball                   │ N 37 │
                 └──────┘                              └──────┘
3 columns of 5 (wide)                   2 columns, 8 + 7 (narrower)
   B          I        …   O              B       I      …   O
 1  6 11   16 21 26     61 66 71        1  9   16 24      61 69
 2  7 12   17 22 27     62 67 72        2 10   17 25      62 70
 3  8 13   18 23 28     63 68 73        3 11   18 26      63 71
 4  9 14   19 24 29     64 69 74        …      …          …
 5 10 15   20 25 30     65 70 75        8      23         68
```

**Board data.** The snapshot carries the board as ordered groups; the client derives everything else (item count, column count, whether columns are locked) from them:

```json
"board": {
  "cellShape": "square",
  "groups": [
    { "label": "B", "items": [ { "id": 1, "callFace": "1", "cardFace": "1" }, …, { "id": 15, "callFace": "15", "cardFace": "15" } ] },
    { "label": "I", "items": [ … ] }
  ]
}
```

- Standard Bingo: five labelled groups, one per card column.
- A custom game with no columns (e.g. 40 items): one group with no label.
- Content sets (ADR 0009): each item has a `callFace` (what is called: "2 + 5", a definition, later an image) and a `cardFace` (what is marked: "7", the word, the name). The board's cells show the side the host chose under **Call board shows** (FR-29). It always starts on the ball side, whatever the content, and only changes when the host switches it; while the large last-ball slot always shows the call face. `cellShape` follows the side shown: `square` for numbers, larger square cells for pictures, `wide` for words and definitions, so the layout rule gives each kind of content room.

**Layout rule.** For each candidate number of rows *r* (1 to the largest group's size): every group needs ⌈items ÷ *r*⌉ sub-columns; total columns = the sum, plus a small gap between groups; cell size = min(width ÷ columns, height ÷ *r*), adjusted for `cellShape`. Use the *r* with the largest cell. For standard Bingo this gives *r* = 5 (3 columns of 5 per letter, 15 × 5) on a 16:9 projector and *r* = 8 (8 + 7, 10 × 8) on 4:3 or upright screens; a single group of 40 on a projector comes out at about 4 rows of 10. The board is read-only, so on phones the cells may shrink below the 48 px tap-target size (nothing on it is tapped).

### Display view (FR-28)

Opened from the host console with **Open display**, in a new tab or on another device via its link or QR. It has its own read-only **display token** and its own connection, so popping it out never disconnects the host console. It shows only what the room should see:

```
┌──────────────────────────────────────────────────────────────┐
│  Round 3 · Normal Bingo [pattern]                            │
│                         ┌────────┐                           │
│                         │  N 37  │  last ball                │
│                         └────────┘                           │
│        B          I          N          G          O         │
│      1  6 11   16 21 26   31 36 41   46 51 56   61 66 71     │
│      2  7 12   17 22 27   32 ■ 42    47 52 57   62 67 72     │
│      …                                                       │
│                                                              │
│   Between rounds: [QR] Join at syncbingo… code 9ve4-shp3-…   │
└──────────────────────────────────────────────────────────────┘
```

Never shown: settings, claims, alerts, the participant list, inspect and award controls, or any ads.

**Connecting a TV or projector directly** (ADR 0010). The screen's own browser opens `/tv`, which shows:

```
┌────────────────────────────────────────────┐
│              SyncBingo display             │
│                                            │
│         Enter this code on the host:       │
│                 K7M-2QX                    │
│                                            │
│        [QR]   or scan with the host phone  │
│              Code expires in 9:41          │
└────────────────────────────────────────────┘
```

The host opens **Options → Connect a TV** and types the code (or scans the QR on the phone signed in as host). The TV switches to the display view at once; Options also lists connected displays, each with **Disconnect**. The `/tv` and display pages stay lightweight (small bundle, no heavy animation) because smart TV browsers are often old and slow.

## 2. Ad slots

**Rules**

- Banners only, and none on the display view (FR-28). No pop-ups, interstitials, expanding or sticky-overlay formats, and nothing that can cover the play area.
- Slots are **fixed-size containers reserved at layout time**, so an ad loading late (or not at all) never shifts the card or buttons.
- Mobile (< 1024 px): one banner at the top and one at the bottom (e.g. 320×50 or 320×100).
- Desktop (≥ 1024 px): one banner on each side (e.g. 160×600), with the game centred.
- The play area is sized so the full card and Bingo button fit on a small phone between the two reserved banners without scrolling.

**Implementation**

```
<AdLayout>            // CSS grid: top | left | main | right | bottom, slots by breakpoint
  <AdSlot position="top" />
  <AdSlot position="left" />
  <main>…game…</main>
  <AdSlot position="right" />
  <AdSlot position="bottom" />
</AdLayout>
```

`AdSlot` takes a `provider` (`placeholder` in dev/portfolio builds; a real network later), enforces its fixed dimensions, and renders nothing interactive over the page. Ads are lazy and never block game rendering.

**Launch considerations (not blockers for the portfolio build)**

- The audience includes young children. Ad networks have child-directed rules and the US has COPPA; contextual (non-personalised) ads, a clear privacy page and no data collection should be part of any launch plan.
- A **Terms of Use** page (linked from every page footer) states, among the usual terms, that hosts are responsible for any custom content they upload or display in their games (ADR 0009). The future custom-content **upload page** repeats this next to the upload control, and the host must tick an acknowledgement before uploading.
- No real-money features are planned in the app (see 01 §6); the "Serious Bingo" tile is a non-interactive teaser.

## 3. Accessibility and kid-friendliness

- Tap targets ≥ 48 px; the card scales to the viewport.
- Marked / called / winning states use shape or icon as well as colour.
- Win celebration respects `prefers-reduced-motion` and avoids flashing (no more than three flashes per second).
- Text contrast meets WCAG AA; large, simple typography for the current ball.
- Screen-reader labels on every cell, e.g. "B 7, called, marked".
- The host can optionally speak the ball aloud with the browser's speech synthesis (nice-to-have).

## 4. States to design

Connecting/reconnecting banner · round countdown in the last-ball slot · "Bingo sent to host" confirmation · claims list on the host console (qualified yes/no, award / dismiss / remove) · wrong password · name taken · round in progress · win highlight · empty hopper · host disconnected · session ended/expired.
