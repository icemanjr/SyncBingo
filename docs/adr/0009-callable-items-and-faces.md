# ADR 0009 — Callable items with call and card faces

**Status:** Proposed (to be accepted when custom content is built, roadmap Phase 4)

## Context
Standard Bingo calls a number and players mark the same number. Hosts such as teachers want other content: call "2 + 5" and mark "7"; read a definition and mark the word; use shapes, word lists or uploaded images. The game rules (dealing unique cards, marking only what has been called, bitmask win patterns, auto-fill) should not change for each kind of content.

## Decision
- **A ball is an item id.** Items are numbered 1…N, exactly as balls are today. Cards hold item ids, marks are bits per cell, and every rule in the domain works on ids. Card fingerprints (FR-39) are made of ids too.
- **Content is presentation.** A content set maps each item id to a **call face** (what is announced or shown when the item is drawn) and a **card face** (what is printed on the card and marked). A face is text or, later, an image reference. Standard Bingo is the content set where both faces are the number, plus the B-I-N-G-O column labels.
- **Dealing is pluggable.** Cards are dealt through `ICardGenerator`:
  - `ColumnCardGenerator` (built): each column draws from its own range, as in 75-ball Bingo.
  - A future free-form generator: any item in any cell, for content without columns.
  - Uniqueness (`GenerateUnique`) works with any generator.

## Consequences
- New content types need a content set and possibly a generator, but no changes to marking, winning or the state machine.
- Domain, API and UI code must not assume that the ball and the card show the same thing, or that columns are always B-I-N-G-O; they display faces and labels from the content set.
- **Open questions** for when this is built:
  - Card faces should be unique within a content set (if "2 + 5" and "10 − 3" both answer "7", a card could show two identical "7" cells). Lean: require unique card faces.
  - Uploaded images need storage (S3), size and type limits, and moderation, because children are expected users.
