using SyncBingo.Domain.Cards;

namespace SyncBingo.Domain.Tests.Cards;

public sealed class CardTests
{
    private static readonly Card Sample = new([7, 22, 0, 51, 66]);

    [Fact]
    public void Cards_with_the_same_numbers_in_the_same_cells_are_equal()
    {
        var copy = new Card([7, 22, 0, 51, 66]);

        copy.ShouldBe(Sample);
        copy.GetHashCode().ShouldBe(Sample.GetHashCode());
        copy.Fingerprint.ShouldBe("7-22-0-51-66");
    }

    [Fact]
    public void The_same_numbers_in_a_different_order_are_a_different_card()
    {
        new Card([22, 7, 0, 51, 66]).ShouldNotBe(Sample);
    }

    [Theory]
    [InlineData(7, 0)]
    [InlineData(66, 4)]
    public void CellOf_finds_a_number_on_the_card(int ball, int cell)
    {
        Sample.CellOf(ball).ShouldBe(cell);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(Card.FreeCellNumber)]
    public void CellOf_is_null_for_numbers_not_on_the_card(int ball)
    {
        Sample.CellOf(ball).ShouldBeNull();
    }

    [Fact]
    public void Rejects_an_empty_card()
    {
        Should.Throw<ArgumentException>(() => new Card([]));
    }
}
