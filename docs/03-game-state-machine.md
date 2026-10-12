# 03 — Game State Machine

## 1. Lifecycle

```mermaid
stateDiagram-v2
    [*] --> BetweenRounds : host creates session
    BetweenRounds --> InRound : StartRound (clear cards, refill hopper)
    state InRound {
        [*] --> Running
        Running --> Paused : Pause
        Paused --> Running : Resume
    }
    InRound --> BetweenRounds : EndRound
    BetweenRounds --> Ended : EndSession
    InRound --> Ended : EndSession
    Ended --> [*]
```

- **BetweenRounds** is also the initial lobby (zero rounds played). Cards keep the previous round's marks until the next round starts.
- **InRound** lasts while the cards carry marks. *Clear cards* from inside a round is shorthand for `EndRound` + `StartRound`.
- **Ended** is reached by the host ending the session or by TTL expiry. All data is then discarded.

## 2. Commands by state

| Command | BetweenRounds | Running | Paused | Notes |
|---------|:---:|:---:|:---:|-------|
| Join (password + name) | ✔ | ✖ | ✖ | FR-5 |
| Start round / Clear cards | ✔ | ✔ (ends current) | ✔ (ends current) | Refills hopper, clears marks, optional redraw |
| End round | – | ✔ | ✔ | Freezes cards, shows leaderboard |
| Pause / Resume | – | ✔ / – | – / ✔ | Pause cancels the auto-call schedule |
| Draw ball ("Grab a new ball") | – | ✔ | ✔ | Explicit host action, allowed while paused (A6) |
| Auto draw (scheduler) | – | ✔ | ✖ | Only in Auto mode |
| Mark / unmark square | ✖ | ✔ | ✔ | Only for called numbers |
| Claim Bingo | ✖ | ✔ | ✔ | Only if Bingo button enabled |
| Change win condition | – | ✔ | ✔ | Same ball draw and marks continue |
| Change settings | ✔ | ✔ | ✔ | Some apply from the next round (e.g. redraw policy) |
| Redraw card (participant) | ✔ if allowed | ✖ | ✖ | Policy FR-20 |
| Redraw cards (host) | ✔ | – | – | At round start |
| Award win | – | ✔ | ✔ | Credits a participant; feeds leaderboard |
| Kick participant | ✔ | ✔ | ✔ | |
| View leaderboard | ✔ | ✔ | ✔ | Host decides if shown to all |

## 3. Ball drawing

1. Hopper = variant's ball set (1–75) minus balls already called this round.
2. Draw uses an injected random source (CSPRNG in production, seeded in tests).
3. Each draw increments `sequence` and records `{ball, letter, calledAt}`.
4. If **auto-fill** is on, every participant card containing the *previous* ball that is not yet marked gets it marked, **before** the new ball is announced. A missed number therefore remains fillable by the child for one full call interval.
5. If **win detection** is on, the win condition is evaluated against all cards after fills (see §4).
6. When the hopper is empty the host is told; no further draws until a new round.

## 4. Win handling

Marks are kept per card as a 25-bit mask; a pattern is a 25-bit mask; a card satisfies a pattern when `(marks & pattern) == pattern`. For Normal Bingo, any of the 12 line masks satisfies.

```mermaid
flowchart TD
  M["Mark, auto-fill or draw"] --> D{"Win detection enabled?"}
  D -- "No" --> C
  D -- "Alert" --> W["Raise WinDetected to host<br/>(name, card, win condition)"]
  D -- "Alert + pause" --> W2["Pause + raise WinDetected"]
  C["Bingo button pressed?"] --> V{"Server validates card<br/>against current win condition"}
  V -- "Valid" --> K["Raise WinClaimed to host<br/>(name, timestamp)"]
  V -- "Invalid" --> X["Reject; tell player 'not yet'"]
  W --> A["Host confirms: Award win"]
  W2 --> A
  K --> A
  A --> L["Record in round + leaderboard;<br/>show win highlight if enabled"]
```

Rules:

- A card can be flagged **at most once per win condition per round** (no repeat alerts).
- Claim order is recorded with a server timestamp so the host can see who was first.
- The host can **award a win** to one or several participants, including when detection and the Bingo button are both off (serious mode: a player shouts, the host verifies via *inspect card*, then awards).
- A participant may win more than once per round, once for each win condition the host plays.
- Awarding is idempotent for the same participant + win condition + round.

## 5. Win patterns (5×5, row-major, centre = free)

Normal Bingo: any of 5 rows, 5 columns, 2 diagonals. Four Corners: cells 0, 4, 20, 24. Blackout: all 25.

Letter shapes (`X` = required cell):

```
 B        I        N        G        O
XXXX.    XXXXX    X...X    XXXXX    XXXXX
X...X    ..X..    XX..X    X....    X...X
XXXX.    ..X..    X.X.X    X.XXX    X...X
X...X    ..X..    X..XX    X...X    X...X
XXXX.    XXXXX    X...X    XXXXX    XXXXX
```

These are data, not logic: each shape is written as a picture like the ones above (`StandardVariants.Classic75`) and every pattern is checked by the same evaluator, so shapes can be tuned or new patterns added without new code. Loading variants and patterns from configuration files is deferred until custom variants are built (roadmap Phase 4).
