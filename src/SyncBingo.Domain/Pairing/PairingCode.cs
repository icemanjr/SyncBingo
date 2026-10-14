using System.Diagnostics.CodeAnalysis;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Domain.Pairing;

/// <summary>
/// The short code a TV shows so the host can connect it (ADR 0010): 6 characters from the
/// game-code alphabet (no look-alikes), about 887 million codes. Shown as <c>K7M-2QX</c>;
/// read in any case, with or without the hyphen.
/// </summary>
public sealed record PairingCode
{
    public const int Length = 6;

    private PairingCode(string value) => Value = value;

    /// <summary>The 6 characters, lowercase and without the hyphen. Used for storage.</summary>
    public string Value { get; }

    public static PairingCode Generate(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var chars = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = GameCode.Alphabet[random.NextInt(GameCode.Alphabet.Length)];
        }

        return new PairingCode(new string(chars));
    }

    public static bool TryParse(string? input, [NotNullWhen(true)] out PairingCode? code)
    {
        code = null;
        var cleaned = new string((input ?? string.Empty).Where(c => c != '-' && !char.IsWhiteSpace(c)).Select(char.ToLowerInvariant).ToArray());
        if (cleaned.Length != Length || !cleaned.All(c => GameCode.Alphabet.Contains(c, StringComparison.Ordinal)))
        {
            return false;
        }

        code = new PairingCode(cleaned);
        return true;
    }

    /// <summary>The display form on the TV, e.g. <c>K7M-2QX</c>: upper case reads better from across a room.</summary>
    public override string ToString() => $"{Value[..3]}-{Value[3..]}".ToUpperInvariant();
}
