# ADR 0004 — Variants and win patterns as data (bitmasks)

**Status:** Accepted

## Context
Standard 75-ball Bingo is the first game, but the host should eventually be able to customise ball sets, card sizes and patterns.

## Decision
A `BingoVariant` describes ball range, column ranges, card dimensions, free cell and win patterns. Patterns are lists of cell bitmasks; a card wins when `(marks & mask) == mask` for any mask. Letter shapes, corners, blackout and Normal Bingo (12 lines) are data: the built-in variant draws shapes as ASCII pictures and generates the lines from the card layout. Loading variants from configuration files waits until custom variants are needed (roadmap Phase 4).

## Consequences
- One evaluator for every pattern; a new pattern is a new picture, not new logic.
- Card state is a compact bitmask, cheap to store and compare.
- Variants beyond 5×5 or 75 balls need validation work, but not a redesign.
