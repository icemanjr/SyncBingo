# 06 — UI, Ad Slots and Accessibility

## 1. Screens

| Screen | Route | Purpose |
|--------|-------|---------|
| Landing | `/` | **Host a game**, **Join a game**, how it works in three steps, **What is SyncBingo?** (an overview, with links to How to play and the FAQ) and **Words SyncBingo uses** (the glossary: game, round, lobby, the host who doesn't play, and the rest); in the Full portfolio build only, a grayed-out **Serious Bingo (coming soon)** |
| Host setup | `/host/new` | Choose the game password; every other setting is in Options, in the lobby |
| Host console | `/host/:code` | **Lobby:** how to join (QR, game code, password note) beside the next-round card and the players. **During a round:** the calling card (last ball, Grab ball) and the **call board** (FR-27), with Bingo calls, players and the round's end beside them. **Options**, **Display**, the leaderboard, a player's card and ending the game open as dialogs |
| Custom set editor (host) | `/host/{code}/set` | Opened from the lobby's *Custom set* card (**Use a custom set** with numbers, **Edit set** with a set). A draft kept in this browser per game: set name; **Open a set file** (replaces the draft), **Add from a file** (merges, with the skip report), **Download this draft**; **Free space text** (blank: FREE; checked like a square and never the same as one); **Card size** (sent with the set; changing it fits the groups like Use groups); **Use groups** (one group per card column, fitted when turned on: extra groups' pairs become ungrouped, missing columns are added empty); **Add an entry** (with *Ball and square match*, one text box when ticked; a group); with Use groups, **Ungrouped pairs** first (with a warning and **Remove all**, confirmed, whenever it has pairs), then each group in column order with **Rename** (once, for all its entries), its short **Label** and **Move up/down**; without, one list of entries; each entry **Edit**, a group menu (including *Ungrouped*), **Delete**; a search box past 20 entries. At the end: the **possible cards** (a warning below 100: "Up to 72 players can each have their own card…"), what still stops the set being used, the host-responsibility acknowledgement, **Use this set** (lobby only) and **Back to numbers**. On phones each entry's text gets the full width, its controls a row below (FR-44, ADR 0009) |
| How to play | `/how-to-play` | A guide for players (joining, the card, marking, Undo, auto-fill, calling Bingo, the patterns drawn, a device that goes to sleep) and hosts (starting, Options, running a round, checking a Bingo, between rounds, the TV, custom sets, ending), then the glossary. Linked from the landing page and every footer |
| FAQ | `/faq` | Short answers: apps and accounts, cost, how many can play, playing apart (only the code and password are needed), the host not playing, joining late, a device that goes to sleep, who decides a Bingo, children, words instead of numbers, shorter games, how long games are kept, privacy, money, help |
| Terms, Privacy | `/terms`, `/privacy` | Plain-language **drafts**, marked "Draft, pending legal review": no accounts or cookies, the password only as a hash, everything deleted 5 minutes after a game ends (4 h idle, 12 h at most), IP addresses only briefly in memory for rate limits, no access logs, analytics or third parties. With real ads on (ADR 0012), Privacy adds an **Ads** section: Google AdSense, every request child-directed, what Google receives, and how to change consent Linked from a footer on every page except the TV |
| Display | `/display/:code` | Read-only view for a projector or TV (FR-28): call board, last ball, win pattern, round, win celebration, leaderboard when shown, and the join QR between rounds. No controls and **no ads** |
| Join | `/join/:code` (QR) or `/join` (manual) | Enter game code (if manual) and password |
| Name entry | same flow | Choose a unique name |
| Play | `/play/:code` | Card, latest ball (if shown), Bingo button (during a round), **Undo** button (always, during a round), own auto-fill switch (if the host made it available), win highlight |
| Leaderboard | overlay/route | Wins per participant; host can show to everyone |
| TV | `/tv` | A smart TV's own browser shows a short pairing code and a QR, then becomes the display once the host approves (ADR 0010). No ads |
| Pair | `/pair/:pairingCode` | What the TV's QR opens on the host's device: **Show game … on the TV** for each game this browser hosts |
| Not found | any other address | "Page not found", with **Back to the start** |

### Host console (sketch)

The design reference (Visual design, below) has the finished look. The layout:

```
Laptop, during a round (everything on one screen, ad rails either side)
┌───────────────────────────────────────────────────────────────┐
│ [Round 3] [Normal Bingo ✎]  6 players · late arrivals wait … │
│                                [Display] [Options] [⋯]        │
├──────────────────────────────────────┬────────────────────────┤
│ LAST BALL  Before: O74 G60 G55 N40   │ ╔ Sam pressed Bingo ═╗ │
│  (N 37)    [ Grab ball ] [Auto mode] │ ║ ✓ 1 line: Rows     ║ │
│                         57 balls left│ ║[Inspect][Award][Dis]║ │
├──────────────────────────────────────┤ ╚════════════════════╝ │
│ Call board              18 of 75     │ Players 6  Leaderboard │
│ (B) (I) (N) (G) (O)  tinted tiles,   │ (A) Ava        ▦  ⋯    │
│ 3 columns of 5 each, down then across│ (B) Ben        ▦  ⋯    │
│                                      │ [End round][Clear cards]│
└──────────────────────────────────────┴────────────────────────┘

Phone, during a round
┌───────────────────────┐
│ [Ad: top banner]      │
│ [Round 3] [Normal ✎]  │
│ [Display][Options][⋯] │
│ [Board][Bingo! 1][Players]  ← tabs: one view at a time
│  …the chosen view…    │
│ ┌───────────────────┐ │  ← the calling bar, pinned to the bottom
│ │(N 37) 57 balls left│ │     of the screen while the view scrolls,
│ │[  Grab ball  ][▶] │ │     and never over the ad below it
│ └───────────────────┘ │
│ [Ad: bottom banner]   │
└───────────────────────┘
```

- **Lobby:** a dark **Join Now!** panel, with "Scan or visit syncbingo.com", the QR code (with room around it for phone cameras) and the game code under it. The password note (FR-49) is edited in place; on the TV it shows exactly as typed, with no label, or not at all when empty. Beside it are the next-round card (patterns with **Change**, Manual or Auto, **Start Round N**, **Deal New Cards**; during the countdown, the seconds left and **Cancel**) and the players.
- **Each player row** has their card (▦) and a ⋯ menu: **Award a win** during a round, and **Remove**, which asks again.
- **In auto mode,** **Options** and the pattern button are not offered (FR-32); **Pause** replaces Grab ball and Auto mode. Displays are not rules, so **Display** stays available.
- **Dialogs:**
  - **Options** doesn't close on Escape or the backdrop, because it asks first about unapplied changes.
  - The others close with Escape, the backdrop or **Close**.
  - Focus moves into the dialog and back afterwards.

### Options and Apply (FR-32)

Options holds every rule: the settings and the win patterns. Changes there are a **draft** kept on the host's device until the host presses **Apply**:

- **Patterns** (FR-21) are checkboxes, and any selected pattern wins. **Normal Bingo** heads its group and switches **Rows**, **Columns** and **Diagonals** together; when only some are on it shows as partly checked. The shapes follow: Four Corners, the letters and Blackout. The last selected pattern can't be cleared ("at least one pattern"). A line above the list names the draft selection ("Any selected pattern wins: Normal Bingo or Four Corners").
- **Your patterns** (FR-21a) close the Patterns section: each of the host's own patterns with **Rename**, then **Draw a pattern** (a name and a grid like the card in use, the free space shown as ★). **Add this pattern** asks "Add “Plus”? You can rename it later, but not delete it." before adding it at once. Drawing waits until a new card size is applied; past 5 patterns the button gives way to a note.

- Changed fields are highlighted, and the button counts them: **Apply 3 changes** (disabled with no changes).
- **Apply** sends every change in one request (`PUT /rules`); the server applies them in one step and every screen gets them together.
- After Apply the button shows **Applied ✓** and stays disabled for about 1.5 s, so a double-click can't send the batch twice. There is no server-side lockout: nothing can be half-applied.
- Settings that shape a whole round (card size: *5 × 5*, *4 × 4* or *3 × 3*; free space: *Always marked*, *Earned* or *None*; redraw policy; multiple wins) sit in a separate **Between rounds** section. A new card size, or choosing *None* or leaving it, redeals every card, as the help text says. The pattern list follows the card size in the draft (no letters on a smaller card; patterns it lacks are dropped, falling back to Normal Bingo). With a 4 × 4 card the free space is locked ("A 4 × 4 card has no center square"); with a grouped set the card size is locked with a link to the **set editor**. During a round it is locked, with the note "Change these in the lobby", so everything Apply sends mid-round takes effect at once.
- Closing Options with unapplied changes asks **Discard changes?**
- Options is **not available in auto mode**: the host presses **Pause** first, so no ball is called while new rules are announced. During the round countdown Options can be opened but **Apply** is disabled ("Cancel the countdown to change the rules").

### Participant play screen (mobile sketch)

```
┌───────────────────────┐
│ [Ad: top banner]      │
├───────────────────────┤
│ ROUND 1 · HOW TO WIN  │
│ Normal Bingo ⓘ  (N 37)│  ← tap: patterns and rules (FR-48); the last ball (if shown)
│ ┌───────────────────┐ │
│ │ B  I  N  G  O     │ │  ← letter colors (Visual design)
│ │ 07 22 38 51 66    │ │
│ │ 12 19 41 49 70    │ │
│ │ 03 27 FREE 58 62  │ │  ← the free space says FREE, or a set's own text
│ │ 15 30 33 47 75    │ │
│ │ 09 16 44 60 68    │ │
│ └───────────────────┘ │
│ [      BINGO!      ]  │  ← during a round
│ [Board][Undo][Marks][More]  ← More: Leave game
├───────────────────────┤
│ [Ad: bottom banner]   │
└───────────────────────┘
```

- **Card tools** sit in one row of icon buttons with short labels: **Call board** (when shown), **Undo** (during a round), **My marks** (FR-51) and **More**, which holds **Leave game** (FR-53).
- **Auto-fill and New card,** when the host allows them, sit just below.
- **Join page:** the form is on one card. Arriving by QR shows **Game found from the QR code** with the code and a **Change** button instead of an empty code field. Under the name: "Everyone sees this name when you win." The button reads **Get my card**.
- **Landing page:** a dark hero ("Bingo for a room full of phones.") with **Join a game** (amber, most visitors) and **Host a game**, then three "how it works" steps.

### Visual design (2026-10-15)

The agreed reference is the design canvas made on 2026-10-15 (host lobby and round on a laptop, host and player on a phone, landing, join, TV display). The refreshed screens follow it; the layouts land screen by screen (roadmap Phase 2).

- **Look:** "party, but tidy". Deep plum ink, a violet accent and an amber last ball, on a faint lilac page. Dark mode is a deep plum page with lighter violet accents.
- **Type:** **Figtree** for reading, **Bricolage Grotesque** for headings, numbers and big buttons. Both are self-hosted (`@fontsource-variable`, SIL Open Font License), so no request goes to a font service. Browsers fetch only the Latin files: 61 KB (Bricolage 41 KB, Figtree 20 KB) on top of the 140 KB first visit (docs/10 §5a).
- **B-I-N-G-O colors:** B blue, I red, N gray, G green, O amber-brown, as on real bingo balls. They color group headers and called balls, with white text that is AA on each. The letter is always written too, so color is never the only cue.
- **Contrast:** every text and background pair is checked by script, in both themes: 4.5:1 for text, 3:1 for borders.
- **Tokens by meaning:** `page`, `surface`, `ink`, `accent`, `notice`, `ball`, `letter-*`, `group` in `web/src/index.css`, each with a light and a dark value. Components never name a color directly.
- **Buttons:** one solid **primary** action per screen (Grab ball, Start Round, Join). Tinted **secondary** for the rest, **outline** for a second strong action (Auto mode), **quiet** text buttons for minor ones. All keep the 48 px tap target.
- **Icons:** drawn (inline SVG), never text symbols or emoji, which some phones' fonts can't show.

### Theme (FR-50)

A three-way switch in every page's header: **System** (the default: follows the device, including when the device switches at sunset), **Light** and **Dark**. Remembered on the device; nothing is sent to the server. Colors are defined by meaning (page, surface, ink, muted ink, accent, danger…) with a light and a dark value each, so every screen supports both, and body and muted text meet 4.5:1 in both. The join QR stays black on white in either theme so phones can scan it. The display view has no header and follows the screen's own setting.

### Color themes (FR-50a)

Five palettes, defined once in `web/src/app/palettes.ts` and applied with `data-palette` on `<html>`, so the light/dark switch keeps working: **Classic** (index.css), **Ocean**, **Forest**, **Halloween** and **Winter**. They change the page, surfaces, accent, last ball and group colors, never the text colors or the B-I-N-G-O letters. A unit test computes every text/background pair of every palette in light and dark (4.5:1 for text, 3:1 for the accent as a border), and an axe scan runs on a player's round screen in each. The host picks the game's theme in Options (*Look*, any time, no notice); players see it unless they chose their own under **My marks** → **Colors**. The host console and the display always show the game's theme; pages outside a game stay Classic.

### Mark style (FR-51)

On the play screen, **⚙ My marks** opens a small panel with a live preview cell:

- **Shape:** ● circle (default, like a dauber), ★ star, ♥ heart, ■ filled square, or ▢ tint (the whole cell's background).
- **Color:** a palette of 6 colors, each checked so a marked number stays readable in both themes, plus a high-contrast option.
- **Opacity:** a slider from 20% to 80% (45% by default), shown live on the preview cell.
- **Text size:** Small, **Medium** (the default), Large, or Max, for whatever is in the squares (numbers now; words or short phrases of up to 15 characters with custom sets).
  - Text **wraps at spaces only**, never inside a word, as many lines as the square's height allows.
  - A square whose text doesn't fit **shrinks on its own**, never below its size's minimum: **6 / 10 / 14 / 18 pt** for Small / Medium / Large / Max (lowered by 2 pt on 2026-10-19: on a 390 px phone a square is 56 px wide, and the old minimums cut off most words of 7 letters or more).
  - Past the minimum, the end is **cut off with "…"**. The full text shows on **hover** (a mouse) or a **long press** (touch, half a second), which never marks the square; a normal tap marks. Screen readers always hear the full text.
  - **Max** is calculated: one size for every square, the largest at which the set's *hardest* face fits whole (one long word can be harder than a short phrase), measured in the card's font and recalculated when the square resizes. Every card in the game uses the same Max.
  - The fitting rules are pure functions (`web/src/features/play/fitText.ts`) with their own tests.

The mark sits *behind* the number and the number stays bold on top, so a marked square never hides what is on it, even at 80%. **Layering is a property of the square's content, not fixed:** text squares draw the mark behind; when image squares arrive (ADR 0009, deferred) the mark is drawn over the picture as a translucent overlay, since a picture would hide a mark behind it. Marked cells also say so to screen readers ("17, marked"). The choice is remembered on the device and never sent to the server.

### Patterns and current rules (FR-48)

Tapping the pattern name opens this panel, in the lobby, during the countdown or during a round. It has two tabs, and opens on **Patterns**:

```
┌──────────────────────────┐   ┌──────────────────────────┐
│[Patterns] Current rules [X]│ │ Patterns [Current rules][X]│
│ Normal Bingo             │   │ • Mark only called numbers│
│ Any row, column or diag. │   │ • Unmark only squares not │
│ ■■■■■  ■....  ■....      │   │   called yet              │
│ .....  ■....  .■...      │   │ • Free space: earned      │
│ .....  ■....  ..★..      │   │ • Undo: always, back to   │
│ .....  ■....  ...■.      │   │   the last ball           │
│ .....  ■....  ....■      │   │ • Auto-fill: on for you   │
│                          │   │ • One win per pattern     │
└──────────────────────────┘   └──────────────────────────┘
```

- **Patterns:** one grid per selected pattern, under "Any one of these wins:" when there are several. Line patterns show one example line ("Any of the 5 rows"). Grids are drawn from the shapes the server sends (docs/05 snapshot), never hard-coded, so other card sizes and patterns need no client change. Cells in the pattern are filled **and** outlined, not shown by color alone; each grid has a text alternative ("Top row", "Column B", "Diagonal").
- **Current rules:** every rule in force that applies to players, in plain words, including the player's own auto-fill switch; host-only settings are left out.
- Tabs are real tabs for assistive technology (`role="tablist"`), each at least 48 px tall. The panel updates in place if the host applies new rules while it is open, and it never covers the BINGO button.

### Rules notices (FR-47)

A panel over the top of the card or board, on participants' screens, the display view and the host console:

```
┌───────────────────────────────┐
│ Rules updated              [X]│
│ • Pattern changed to Blackout │
│ • Squares can only be marked  │
│   once their number is called │
└───────────────────────────────┘
```

- **Rules updated** appears when the host applies changes mid-round, listing each change in plain words; a pattern change shows the new pattern's grid (as in FR-48), and tapping the notice opens the *Patterns and current rules* panel. The round-start version, shown during the countdown, reads **This round: Four Corners** with the pattern's shape drawn as a small 5×5 grid.
- It closes with **X** (48 px target) or by itself when the next ball is called. It never hides the BINGO button, and it is announced to screen readers (`aria-live="polite"`). It has no animation under reduced motion and never flashes.
- For 3 seconds after a *Rules updated* notice the host's **Grab ball** button counts down ("Grab ball in 3…") and **Auto mode** is disabled, so everyone has time to read before the next ball.

### Lobby and round countdown (FR-45, FR-46)

Between rounds the host console's main control is the round button, with the start-mode toggle beside it:

```
┌─────────────────────────────────────────────┐
│ Players (14) ▸                [Options]     │
│      [ Start Round 4 ]   Manual ◉ ○ Auto    │
│ [ Clear cards ]  [ Deal New Cards ]         │
└─────────────────────────────────────────────┘
```

**Deal New Cards** gives every player a fresh card (FR-34). It appears only in the lobby, and after a tap it reads **Dealt ✓** for about 1.5 s, so a double tap can't deal twice.

With *Show round numbers* off the button reads **Start New Round**. Pressing it starts a 10-second countdown on every screen, shown where the last ball normally sits:

```
┌──────────┐
│ Round    │
│ starts in│
│    7     │
└──────────┘      host only: [ Cancel ]
```

Clients count down locally to the server's `endsAt`, so there is no message per second. During the countdown and the round, the join page shows "A round is in progress. Join now and you'll be added as soon as it's done." and joining enters the waiting room (FR-5): the play screen shows "Round currently being played. You'll be added as soon as it's done." with **My marks**, the auto-fill switch and the call board as usual, but no card or Bingo button. The host's Players list shows them under **Waiting**, counted with the players, with "New players cannot get a card during a round. Return to the lobby to allow them to join (End round, Clear cards or Cancel)." and **Remove**. The console's status line reads "late arrivals wait until the end of the round" instead of "joining open", and the join panel says "Anyone who arrives during a round waits, and gets a card as soon as it ends." At zero the slot shows "–" until the first ball (manual), or the first ball itself (auto).

### Claim alerts on the host console

Every Bingo press appears as an alert with the player's name, the time, the lines completed and the wins still **awardable** (FR-37a, FR-43). Its buttons follow *Multiple wins*:

| Situation | Buttons |
|-----------|---------|
| *Once per pattern* or *once per round*, awardable 1 | **Inspect**, **Award**, **Dismiss**, ⋯ (**Remove player**) |
| *Every declared win*, awardable *n* ≥ 1 | **Inspect**, **Award 1** … **Award n** (one button per count, so the host taps the number the player declared; a stepper above 4), **Dismiss**, ⋯ |
| Not on called numbers ("✗ not on called numbers"), card hasn't won | **Inspect**, **Award**, **Dismiss**. Award asks first: *"Award a Bingo that isn't on the called numbers?"* **Award anyway** / **Cancel**, because the host decides (ADR 0008) but may have tapped by mistake |
| Already won, in the *once* modes ("already won, nothing more to award") | **Inspect**, **Dismiss**. No Award button: an award would change nothing (docs/03) |

Every button keeps the 48 px tap target. An award sends the claim's id, so a double tap counts once.

### Wrong-password alert (host only)

When many people type a wrong game password (10 within a minute), the host console shows an alert, because the password shared with the room is often not the one the game was created with: **"10 wrong passwords in the last minute. Check the password you shared."** If joining from one address gets locked (docs/05 §5), the alert says so: **"Joining is locked for 10 minutes from one network after 60 wrong passwords."** Players see nothing; each alert shows once, not on every typo.

### Call board (FR-27)

All balls of the variant, called ones marked with a fill **and** a tick (never color alone). The **last ball** sits large and **top center, above the letters**, as letter + number ("N 37"), so people at the back who misheard a call can check it; its cell on the board is emphasized too, and before the first call the slot shows "–". The board knows nothing about B-I-N-G-O. It draws whatever **groups of items** the server sends (see *Board data* below). For standard Bingo those are the five letters, which sit **side by side** across the board, and each letter's 15 numbers form a small block under it, read top to bottom, then left to right:

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

- Standard Bingo: five labeled groups, one per card column.
- A custom game with no columns (e.g. 40 items): one group with no label.
- Content sets (ADR 0009): each item has a `callFace` (what is called: "2 + 5", a definition, later an image) and a `cardFace` (what is marked: "7", the word, the name). The board's cells show the side the host chose under **Call board shows** (FR-29). It always starts on the ball side, whatever the content, and only changes when the host switches it; while the large last-ball slot always shows the call face. `cellShape` follows the side shown: `square` for numbers, larger square cells for pictures, `wide` for words and definitions, so the layout rule gives each kind of content room.

**As built for text sets (2026-10-19, PR 4 of custom content):** the board shows each group, in order and under its name, as a grid of wide tiles with the ball text cut off with "…" (full text on hover and for screen readers); a set dealt free-form is one block. The **last ball** becomes a rounded panel in the ball color, its group's name over the call text, whose size steps down with length so a 120-character definition fits. Cards show the square text with tighter gaps (about 20% larger squares), group names above the columns cut off when long, and the host's card inspector and recent calls show text too. The FR-29 **Call board shows** choice is in the host's **Displays** menu (ball side by default): with definitions on the ball side the board's tiles say little ("An organism that m…"), so the host can switch every board to the square side.

**Layout rule.** For each candidate number of rows *r* (1 to the largest group's size): every group needs ⌈items ÷ *r*⌉ sub-columns; total columns = the sum, plus a small gap between groups; cell size = min(width ÷ columns, height ÷ *r*), adjusted for `cellShape`. Use the *r* with the largest cell. For standard Bingo this gives *r* = 5 (3 columns of 5 per letter, 15 × 5) on a 16:9 projector and *r* = 8 (8 + 7, 10 × 8) on 4:3 or upright screens; a single group of 40 on a projector comes out at about 4 rows of 10. The board is read-only, so on phones the cells may shrink below the 48 px tap-target size (nothing on it is tapped).

### Display view (FR-28)

Opened from the host console's **Display** button, in a new tab or on another device via its link or QR. It has its own read-only **display token** and its own connection, so popping it out never disconnects the host console. It shows only what the room should see:

```
During a round (16:9; left column 300 px, the board fills the rest, from the top)
┌──────────────────────────────────────────────────────────────┐
│ [Round 3]          ┌──────────────────────────────────────┐  │
│   LAST BALL        │  (B)    (I)    (N)    (G)    (O)      │  │
│    ( N 37 )        │  1 6 11 16 21 26 31 ■ 41 46 51 56 ... │  │
│ Normal Bingo       │  … tinted tiles, down then across     │  │
│ ▦ ▦ ▦ pattern grids└──────────────────────────────────────┘  │
│ Leaderboard (if    [        Ava had a Bingo!            ]    │  ← alerts under the board:
│  the host shows it)[ Rules updated: … ]                      │     the latest win, rules notices
└──────────────────────────────────────────────────────────────┘

The board never moves or shrinks for an alert, so its balls never crowd on smaller TVs.

Between rounds: the board's place shows the Join Now! panel: the QR, the game code under it,
and the password note exactly as the host typed it ("Password: harvest2026"), or nothing if empty.
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

**Display** opens a dialog: **Open in a new tab**, **Copy display link**, **Connect a TV** (its help names this site's own `/tv` address), **Password note**: a text box (up to 80 characters) for the password or a hint, shown under the QR on every display and on the host console between rounds (FR-49), then (with a custom set) **Call board shows** and the list of displays, each with **Disconnect**. Blank shows nothing.

The host opens **Display → Connect a TV** and types the code (or scans the QR on the phone signed in as host). The TV switches to the display view at once; the same menu lists connected displays, each with **Disconnect**. It lives outside Options so it works in auto mode too. The `/tv` and display pages stay lightweight (small bundle, no heavy animation) because smart TV browsers are often old and slow.

## 2. Ad slots

**Rules**

- Banners only, and only on pages with content (FR-50 amended, ADR 0012): the landing page, How to play, the FAQ, the play screen and the host console. None on forms, legal pages, pairing, not found or the display view (FR-28), and none on a game screen while it shows a dead end (`useNoAds`). No pop-ups, interstitials, expanding or sticky-overlay formats, and nothing that can cover the play area.
- Slots are **fixed-size containers reserved at layout time**, so an ad loading late (or not at all) never shifts the card or buttons.
- Mobile (< 1024 px): one banner at the top and one at the bottom (e.g. 320×50 or 320×100).
- Desktop (≥ 1024 px): one banner on each side (e.g. 160×600), with the game centerd.
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

`AdSlot` enforces its fixed dimensions (`overflow-hidden`, so nothing an ad draws leaves its box) and renders nothing interactive over the page. Ads are lazy and never block game rendering. The provider comes from the build (`web/src/app/ads.ts`): **placeholders** in development and the Full portfolio build, **Google AdSense** on the live site (ADR 0012):

- AdSense's script loads once, when the first slot appears, so never on the TV display. Only the visible pair asks for an ad: top and bottom on a phone, the sides on a desktop. Each unit is asked once and never refreshed.
- Every unit carries `data-tag-for-age-treatment="1"` (child treatment: no personalized ads, no remarketing), added by `AdSlot` itself.
- An unfilled slot stays empty at its reserved size.
- Visitors in the EU, the UK and Switzerland see Google's consent message on their first visit (the one exception to FR-51). The footer's **Privacy and cookie settings** opens it again; it appears only when ads are on.
- **Turning it on** (the owner's steps):
  1. Create the AdSense account and add syncbingo.com.
  2. Put the publisher id in `web/.env` (`VITE_ADSENSE_CLIENT`). The next deploy carries the verification tag and `/ads.txt`; until then Caddy answers `/ads.txt` with 404.
  3. In AdSense, set up Privacy & messaging → European regulations (Google's consent message), and keep **Auto ads off**.
  4. Once the site is approved, create two display ad units, 320 × 50 and 160 × 600, and put their ids in `web/.env`.
  5. Set the repository variable `AD_PROVIDER` to `adsense`. The live build then shows ads and the Privacy page describes them.

**Launch considerations (not blockers for the portfolio build)**

- The audience includes young children. Ad networks have child-directed rules and the US has COPPA. Hence every request is child-directed and the Privacy page describes the ads (ADR 0012).
- A **Terms of Use** page (linked from every page footer) states, among the usual terms, that hosts are responsible for any custom content they upload or display in their games (ADR 0009). The future custom-content **upload page** repeats this next to the upload control, and the host must tick an acknowledgement before uploading.
- No real-money features are planned in the app (see 01 §6); the "Serious Bingo" tile is a non-interactive teaser.

## 2a. Wording

- American spelling (owner, 2026-10-28): color, center, canceled, -ize. British English comes later as its own translation.
- **Error messages don't use contractions** (owner, 2026-10-28): every refusal from the server and every error the web app shows reads "cannot", "is not", "does not", which feels more formal for an error. Everything else (help, notices, rules, buttons) may use them.
- **Say "device"** (owner, 2026-10-28): text never assumes what someone is using. "These choices stay on your device", not "on your phone"; a phone, tablet or laptop may be named only as an example ("from any device, such as a laptop"). The landing headline, "Bingo for a room full of phones", describes the room, not the reader.
- Server messages are written to be shown to people as they are (docs/05 §5); the game is a "game" to people, never a "session".
- Never call SyncBingo "Kahoot-style"; it is phone-first, drop-in party Bingo.

## 3. Accessibility and kid-friendliness

- Tap targets ≥ 48 px; the card scales to the viewport.
- Marked / called / winning states use shape or icon as well as color.
- Win celebration respects `prefers-reduced-motion` and avoids flashing (no more than three flashes per second).
- Text contrast meets WCAG AA; large, simple typography for the current ball.
- Screen-reader labels on every cell, e.g. "B 7, called, marked".
- The host can optionally speak the ball aloud with the browser's speech synthesis (nice-to-have).

- **Checked automatically (2026-10-16):** `e2e/tests/accessibility.spec.ts` plays a real game and scans every screen with axe-core against WCAG 2.1 A and AA, in light and in dark, on every PR: landing, create, join, the host's lobby, Options and round (laptop and phone), the player's card and rules panel, and the TV.
- **The first scan found one problem:** the player's card wasn't a valid grid. Its squares now sit in rows and announce a mark as "selected". Everything else passed.
- **Reduced motion:** with the device's reduced-motion setting on, animations and transitions are switched off everywhere, by one global rule, and the BINGO button doesn't move when pressed.

## 4. States to design

Connecting/reconnecting banner · round countdown in the last-ball slot · "Bingo sent to host" confirmation · claims list on the host console (qualified yes/no, award / dismiss / remove) · wrong password · name taken · round in progress · win highlight · empty hopper · host disconnected · session ended/expired.
