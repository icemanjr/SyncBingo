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

## Sets are files the host keeps; changing sets between rounds
Decided by the owner (2026-10-13):
- **A set is a dictionary of pairs**: each ball (call face) with its square (card face). Card size, the free space and the win patterns are session settings, **independent of the set**, so changing the set never makes a pattern unusable.
- **A set may also carry groups** (owner, 2026-10-13): named lists of its pairs, e.g. "×1", "×2", "×3" for times tables. Grouping takes effort, so it is saved in the set file and restored on upload, and can be edited in the set editor. Whether to *use* the groups is a host setting, **Group items** (on / off):
  - *on*: each card column draws from one group, as B-I-N-G-O does, and the call board shows the groups side by side under their labels;
  - *off*: items are dealt anywhere (free-form) and the board is one block.
  Grouping needs one group per card column, each with enough pairs for its column (5 on a 5 × 5 card, 4 for the column holding the free space). If the set's groups do not fit the current card, the toggle is disabled with the reason, and the set still works ungrouped.
- **A set needs at least 24 pairs**, enough for a 5 × 5 card with a free space. With exactly 24, the free space must be used; a card without a free space (a future card option, alongside card sizes) needs at least 25. A set too small for the current card is refused on upload with what to change (a smaller card, or the free space on); the set is never altered to fit.
- **At most one custom set is stored per session**: the one in use. Nothing is kept beyond the session, in line with ADR 0003, so there is no library of sets to manage in ephemeral storage.
- **A set is a single file the host keeps.** After building a set (balls and squares) the host downloads it as one file and re-uploads it whenever it is wanted. A teacher prepares *Times tables 1–3*, *Times tables 4–6* and *Week 5 vocabulary* ahead of time and uploads whichever is next.
- **The set can change between rounds** (in the lobby, never during a countdown or a round), by uploading another file. It replaces the stored set, and:
  - every card is redealt, because cards hold item ids from the old set; all old fingerprints are released (FR-39);
  - the card size, free space and win condition stay as they are;
  - the call board rebuilds from the new set's pairs;
  - the leaderboard keeps counting across the whole session, and each round's summary records which set it used.

## Text only for now; building and growing a set
Decided by the owner (2026-10-14):
- **Faces are text.** Images on balls or squares are **deferred**: storage, data transfer and moderation would raise the running cost of a personal project (docs/10). Item ids and faces stay as designed, so images can be added later without a model change.
- **The set editor** (lobby only, since any change redeals every card): add entries one by one, with a **Ball and square match** checkbox that uses one text for both sides; **add from file**, merging another set file into the current one (a teacher uploads *Week 1 vocabulary*, adds week 2's new words, and downloads the cumulative list); **edit** and **delete** entries; or **upload** a file that replaces the set. Groups in a merged file combine with existing groups of the same name.
- **Each side is unique within a set**, so a call can only ever match one square and a card never shows the same square twice. Added entries (typed or merged) that duplicate an existing pair, or reuse one side with a different partner, are **skipped and reported** to the host, never overwritten; the host can re-add them with different wording.
- **Readable squares** (FR-51): square text wraps at spaces and shrinks to a per-size minimum, then is cut off with "…" (full text on hover or long press). The set editor warns about entries that would be cut off on a phone, but never blocks them.
- **Limits**, checked by the server before anything changes (a cost and abuse guard): 24–500 pairs, ball text ≤ 120 characters, square text ≤ 20 (a matched entry is therefore ≤ 20), ≤ 10 groups with names ≤ 20 characters, set file ≤ 64 KB.
- **The set file** is versioned JSON: `format`, a name, the pairs (`ball`, and `square` only when it differs, so a one-sided entry means both sides match) and optional groups.

## Responsibility for content
Decided by the owner (2026-10-12):
- **SyncBingo is responsible for** the ads it shows and any **built-in content sets** it ships (pre-created card sets). Those are reviewed as suitable for children.
- **Hosts are responsible for their own content.** A host's custom set (its words) is usable **only in that host's games**: it is never listed, shared or shown to other hosts. Whoever uploads it is responsible for it being appropriate for their audience.
- This is stated in the site's **Terms of Use** and on the **upload page** itself, which the host must acknowledge before uploading.

## Consequences
- New content types need a content set and possibly a generator, but no changes to marking, winning or the state machine.
- Domain, API and UI code must not assume that the ball and the card show the same thing, or that columns are always B-I-N-G-O; they display faces and labels from the content set. The call board can show either side (FR-29): the ball side by default, or the square side when the balls are large, such as pictures or definitions.
- **Open questions** for when this is built:
  - ~~Card faces should be unique within a content set.~~ Decided: both faces are unique (see above).
  - *(When images return)* Uploaded images need storage (S3) and size and type limits.
  - *(When images return)* Marks on image squares are drawn over the picture as a translucent overlay, not behind it as on text squares (FR-51); the card component already treats layering as per-square. Moderation is the host's responsibility (see above). A disclaimer does not remove legal duties for clearly illegal material or copyright takedown requests, so uploads should stay private to the host, expire with the session where possible, and there should be a contact for reports. Get legal review before launch.
  - ~~Reuse across sessions without accounts.~~ Decided: sets are files the host downloads and re-uploads (see above).
  - ~~The set file format.~~ Decided: versioned JSON (see above), validated on upload (format version, the limits, enough pairs for the current card, unique faces, every grouped pair in exactly one group). An `images/` folder can be added in a later format version.
