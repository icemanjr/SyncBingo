# Sample sets

Custom content sets (ADR 0009) kept in the repository **for testing and screenshots**. The sets offered to
hosts on the site's **Ready-made sets** page live in `web/public/sets/` (Science vocabulary is a copy of the
one here); the same test checks both folders.

| File | What it is |
|------|------------|
| `science-vocabulary.json` | Science vocabulary for 6th grade and younger: the ball reads a clue, players mark the word. Five groups, one per card column, so it plays like B-I-N-G-O: **B**iology (plants, animals and ecosystems), **I**nventions & Tools (lab gear and scientific methods), **N**umbers & Shapes (elementary math and geometry), **G**eology & Earth (weather, rocks and space), **O**bjects & Forces (matter, energy and physics basics), 15 pairs each. |

`tests/SyncBingo.Domain.Tests/Content/SampleSetTests.cs` reads every file here, so a sample that stops
being a valid set fails the build.
