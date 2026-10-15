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

## Amendment (2026-10-15): several patterns at once
The host may select **several patterns** for a round, any of which wins (FR-21), e.g. Normal Bingo or Four Corners.
- **Normal Bingo splits** into three patterns: **Rows**, **Columns** and **Diagonals** (5, 5 and 2 masks). It becomes a **preset**, a named group of patterns the variant also carries as data, so clients name a selection without hard-coding ids.
- **The round's win condition is a selection key:** pattern ids joined by `+` in the variant's order (`rows+columns+diagonals+corners`). It goes everywhere one pattern id went (events, claims, wins, alerts), so nothing else changes shape.
  - The "once" modes count **once per selection**, and choosing a new selection allows another win, as changing the pattern did.
  - A preset id stands for its patterns (`normal` = `rows+columns+diagonals`), which also reads sessions saved before this change.
- **Each pattern says whether it is a line** (`isLine`) **and how a win on it is announced** (`announcedAs`, e.g. "the letter B"). A win is announced as "a Bingo" unless a shape is the **only** thing complete on the card ("Ava had Four Corners!"). This way an announcement never names a pattern the player didn't declare.
