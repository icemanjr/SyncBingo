using SyncBingo.Domain.Rules;

namespace SyncBingo.Domain.Tests.Rules;

public sealed class WinPatternTests
{
    [Fact]
    public void Is_satisfied_when_any_one_mask_is_fully_marked()
    {
        var pattern = new WinPattern("p", "Pattern", [Cells.Of(0, 1), Cells.Of(2, 3)]);

        pattern.IsSatisfiedBy(Cells.Of(2, 3)).ShouldBeTrue();
        pattern.IsSatisfiedBy(Cells.Of(0, 3)).ShouldBeFalse();
    }

    [Fact]
    public void Rejects_a_pattern_without_masks()
    {
        Should.Throw<ArgumentException>(() => new WinPattern("p", "Pattern", []));
    }

    [Fact]
    public void Rejects_an_empty_mask_because_it_would_always_win()
    {
        Should.Throw<ArgumentException>(() => new WinPattern("p", "Pattern", [Cells.Of(0), 0u]));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_a_blank_id(string id)
    {
        Should.Throw<ArgumentException>(() => new WinPattern(id, "Pattern", [Cells.Of(0)]));
    }
}
