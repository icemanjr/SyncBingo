using SyncBingo.Domain.Pairing;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Domain.Tests;

public sealed class PairingCodeTests
{
    [Fact]
    public void A_code_has_six_characters_without_look_alikes()
    {
        var random = new SeededRandom(1);

        for (var i = 0; i < 500; i++)
        {
            var code = PairingCode.Generate(random);
            code.Value.Length.ShouldBe(6);
            code.Value.ShouldAllBe(c => GameCode.Alphabet.Contains(c, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("K7M-2QX")]
    [InlineData("k7m2qx")]
    [InlineData(" k7m 2qx ")]
    public void It_reads_however_the_host_types_it(string typed)
    {
        PairingCode.TryParse(typed, out var code).ShouldBeTrue();

        code!.Value.ShouldBe("k7m2qx");
        code.ToString().ShouldBe("K7M-2QX");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("K7M-2Q")]
    [InlineData("K7M-2QXX")]
    [InlineData("K0M-2QX")]
    public void Anything_else_is_not_a_code(string? typed)
    {
        PairingCode.TryParse(typed, out _).ShouldBeFalse();
    }
}
