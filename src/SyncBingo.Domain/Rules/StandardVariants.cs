namespace SyncBingo.Domain.Rules;

/// <summary>Built-in rule sets. Shapes match the tables in docs/03 §5.</summary>
public static class StandardVariants
{
    /// <summary>
    /// Standard 75-ball Bingo: a 5×5 card with a free centre, columns B I N G O
    /// drawing from 1–15, 16–30, 31–45, 46–60 and 61–75.
    /// </summary>
    public static BingoVariant Classic75 { get; } = CreateClassic75();

    private static BingoVariant CreateClassic75()
    {
        var layout = new CardLayout(rows: 5, columns: 5, freeCell: 12);

        WinPattern Shape(string id, string name, params string[] rows) =>
            new(id, name, [layout.ParseShape(rows)]);

        return new BingoVariant(
            id: "classic-75",
            layout,
            ballMax: 75,
            columns:
            [
                new("B", 1, 15),
                new("I", 16, 30),
                new("N", 31, 45),
                new("G", 46, 60),
                new("O", 61, 75),
            ],
            patterns:
            [
                new WinPattern("normal", "Normal Bingo", layout.Lines()),
                Shape("corners", "Four Corners",
                    "X...X",
                    ".....",
                    ".....",
                    ".....",
                    "X...X"),
                Shape("B", "Letter B",
                    "XXXX.",
                    "X...X",
                    "XXXX.",
                    "X...X",
                    "XXXX."),
                Shape("I", "Letter I",
                    "XXXXX",
                    "..X..",
                    "..X..",
                    "..X..",
                    "XXXXX"),
                Shape("N", "Letter N",
                    "X...X",
                    "XX..X",
                    "X.X.X",
                    "X..XX",
                    "X...X"),
                Shape("G", "Letter G",
                    "XXXXX",
                    "X....",
                    "X.XXX",
                    "X...X",
                    "XXXXX"),
                Shape("O", "Letter O",
                    "XXXXX",
                    "X...X",
                    "X...X",
                    "X...X",
                    "XXXXX"),
                new WinPattern("blackout", "Blackout", [layout.AllCells]),
            ]);
    }
}
