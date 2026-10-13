# ADR 0008 — Host credits wins

**Status:** Accepted

## Context
Ties are sometimes decided by who shouted first, serious games leave recognising a win to the player, and the host must be able to pause for a prize. The leaderboard needs a definitive winner list.

## Decision
Detection and Bingo-button claims raise alerts (with names and timestamps). Every claim reaches the host, valid or not, with a flag saying whether the card qualifies, as a shout must reach the caller; the host awards, dismisses, or removes a player who keeps calling false Bingos. Only the host's **Award win** action records a win and feeds the leaderboard. The host can inspect a card to verify, and can award without any alert (win detection and Bingo button both off).

## Consequences
- The host remains in control and ties are resolved by a human.
- One extra tap for the host per win; mitigated by a one-tap "Award" on the alert.
- A future automatic-award option is simple to add if wanted.
