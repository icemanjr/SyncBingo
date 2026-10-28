# ADR 0008 — Host credits wins

**Status:** Accepted

## Context
Ties are sometimes decided by who shouted first, serious games leave recognizing a win to the player, and the host must be able to pause for a prize. The leaderboard needs a definitive winner list.

## Decision
Detection and Bingo-button claims raise alerts (with names and timestamps). Every claim reaches the host, valid or not, with a flag saying whether the card qualifies, as a shout must reach the caller; the host awards, dismisses, or removes a player who keeps calling false Bingos. Only the host's **Award win** action records a win and feeds the leaderboard. The host can inspect a card to verify, and can award with no alert and no claim at all (a shouted Bingo with win detection off). *Amended 2026-10-14:* the Bingo button is always available during a round; there is no setting to turn it off.

## Consequences
- The host remains in control and ties are resolved by a human.
- One extra tap for the host per win; mitigated by a one-tap "Award" on the alert.
- A future automatic-award option is simple to add if wanted.
