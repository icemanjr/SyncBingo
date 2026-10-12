# 06 — UI, Ad Slots and Accessibility

## 1. Screens

| Screen | Route | Purpose |
|--------|-------|---------|
| Landing | `/` | **Host a game**, **Join a game**, greyed-out **Serious Bingo (coming soon)** |
| Host setup | `/host/new` | Choose game password and initial settings |
| Host console | `/host/:code` | QR + game code, participant list, settings, draw/pause/round controls, win alerts, leaderboard |
| Join | `/join/:code` (QR) or `/join` (manual) | Enter game code (if manual) and password |
| Name entry | same flow | Choose a unique name |
| Play | `/play/:code` | Card, latest ball (if shown), Bingo button (if enabled), win highlight |
| Leaderboard | overlay/route | Wins per participant; host can show to everyone |

### Host console (sketch)

```
┌─────────────────────────────────────────────┐
│ [Ad: top banner]                            │
├─────────────────────────────────────────────┤
│ Game code  9ve1-sle3-234k-am32     [QR]     │
│ Round 3 · Normal Bingo · ▶ Running          │
├─────────────────────────────────────────────┤
│  LAST BALL                  [ Grab ball ]   │
│   N 37        recent: 12 55 8 61 30         │
│  [ Pause ] [ Win condition ▾ ] [ End round ]│
├─────────────────────────────────────────────┤
│ 🔔 Sam has a winning card  [Inspect][Award] │
│ Players (14): Ava, Ben, Sam, ...  [Kick]    │
│ [ Clear cards ]   [ Leaderboard ]  [Settings]│
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

## 2. Ad slots

**Rules**

- Banners only. No pop-ups, interstitials, expanding or sticky-overlay formats, and nothing that can cover the play area.
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
- No real-money features are planned in the app (see 01 §6); the "Serious Bingo" tile is a non-interactive teaser.

## 3. Accessibility and kid-friendliness

- Tap targets ≥ 48 px; the card scales to the viewport.
- Marked / called / winning states use shape or icon as well as colour.
- Win celebration respects `prefers-reduced-motion` and avoids flashing (no more than three flashes per second).
- Text contrast meets WCAG AA; large, simple typography for the current ball.
- Screen-reader labels on every cell, e.g. "B 7, called, marked".
- The host can optionally speak the ball aloud with the browser's speech synthesis (nice-to-have).

## 4. States to design

Connecting/reconnecting banner · wrong password · name taken · round in progress · paused overlay (does not cover the card) · win highlight · empty hopper · host disconnected · session ended/expired.
