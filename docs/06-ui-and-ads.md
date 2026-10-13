# 06 — UI, Ad Slots and Accessibility

## 1. Screens

| Screen | Route | Purpose |
|--------|-------|---------|
| Landing | `/` | **Host a game**, **Join a game**, greyed-out **Serious Bingo (coming soon)** |
| Host setup | `/host/new` | Choose game password and initial settings |
| Host console | `/host/:code` | **Call board by default** (FR-27), with QR + game code, last ball, draw/pause/round controls, claims and alerts, participant list, leaderboard. Rules and settings sit behind an **Options** button. **Open display** pops out the display view |
| Display | `/display/:code` | Read-only view for a projector or TV (FR-28): call board, last ball, win pattern, round, paused banner, win celebration, leaderboard when shown, and the join QR between rounds. No controls and **no ads** |
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
│ Round 3 · Normal Bingo · ▶ Running [Options]│
├─────────────────────────────────────────────┤
│  [ Grab ball ] [ Pause ]                    │
│  ── call board (FR-27) ──                   │
│               ┌──────┐                      │
│               │ N 37 │ last ball            │
│               └──────┘                      │
│    B       I       N       G       O        │
│  1 ■ 11  16 21 26  ...                      │
│  2 7 12  17 ■ 27                            │
│  ...                                        │
├─────────────────────────────────────────────┤
│ 🔔 Sam: BINGO ✓ qualifies                   │
│        [Inspect] [Award] [Dismiss] [Remove] │
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

### Call board (FR-27)

All balls of the variant, called ones marked with a fill **and** a tick (never colour alone). The **last ball** sits large and **top centre, above the letters**, as letter + number ("N 37"), so people at the back who misheard a call can check it; its cell on the board is emphasised too, and before the first call the slot shows "–". The five letters sit **side by side** across the board, and each letter's 15 numbers form a small block under it, read top to bottom, then left to right:

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

**Layout rule:** pick the arrangement that gives the larger cell in the space available: cell size = min(width ÷ columns, height ÷ rows). That is 15 columns × 5 rows for 3-per-letter and 10 × 8 for 2-per-letter, so a 16:9 projector gets 3 columns and a 4:3 or upright screen gets 2. The board is read-only, so on phones the cells may shrink below the 48 px tap-target size (nothing on it is tapped). For variants without B-I-N-G-O columns, the same rule picks a grid for the items (ADR 0009).

### Display view (FR-28)

Opened from the host console with **Open display**, in a new tab or on another device via its link or QR. It has its own read-only **display token** and its own connection, so popping it out never disconnects the host console. It shows only what the room should see:

```
┌──────────────────────────────────────────────────────────────┐
│  Round 3 · Normal Bingo [pattern]            ▶ Running       │
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

Connecting/reconnecting banner · "Bingo sent to host" confirmation · claims list on the host console (qualified yes/no, award / dismiss / remove) · wrong password · name taken · round in progress · paused overlay (does not cover the card) · win highlight · empty hopper · host disconnected · session ended/expired.
