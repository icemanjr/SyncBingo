using System.Diagnostics.CodeAnalysis;

namespace SyncBingo.Domain.Sessions;

/// <summary>
/// The code players type or scan to find a session (ADR 0006): 16 characters from an
/// alphabet without look-alikes, about 79 bits of randomness, shown as
/// <c>xxxx-xxxx-xxxx-xxxx</c>.
/// </summary>
public sealed record GameCode
{
    /// <summary>Digits and lowercase letters without 0/o and 1/i/l.</summary>
    public const string Alphabet = "23456789abcdefghjkmnpqrstuvwxyz";

    public const int Length = 16;

    private const int GroupSize = 4;

    private GameCode(string value) => Value = value;

    /// <summary>The 16 characters, lowercase and without hyphens. Used in URLs and storage keys.</summary>
    public string Value { get; }

    public static GameCode Generate(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[random.NextInt(Alphabet.Length)];
        }

        return new GameCode(new string(chars));
    }

    /// <summary>
    /// Reads a code as a person might type it: any case, with or without hyphens or
    /// spaces between the groups.
    /// </summary>
    public static bool TryParse(string? input, [NotNullWhen(true)] out GameCode? code)
    {
        code = null;
        if (input is null)
        {
            return false;
        }

        Span<char> chars = stackalloc char[Length];
        var count = 0;
        foreach (var c in input)
        {
            if (c == '-' || char.IsWhiteSpace(c))
            {
                continue;
            }

            var lower = char.ToLowerInvariant(c);
            if (count == Length || !Alphabet.Contains(lower, StringComparison.Ordinal))
            {
                return false;
            }

            chars[count++] = lower;
        }

        if (count != Length)
        {
            return false;
        }

        code = new GameCode(new string(chars));
        return true;
    }

    public static GameCode Parse(string input) =>
        TryParse(input, out var code)
            ? code
            : throw new FormatException($"'{input}' is not a valid game code.");

    /// <summary>The display form, e.g. <c>9ve4-shp3-k7mw-a2qx</c>.</summary>
    public override string ToString() =>
        string.Join('-', Enumerable.Range(0, Length / GroupSize).Select(group => Value.Substring(group * GroupSize, GroupSize)));
}
