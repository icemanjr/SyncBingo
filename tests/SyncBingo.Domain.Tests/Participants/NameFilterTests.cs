using SyncBingo.Domain.Participants;

namespace SyncBingo.Domain.Tests.Participants;

/// <summary>Offensive names are refused, through the usual disguises, without blocking innocent ones.</summary>
public sealed class NameFilterTests
{
    [Theory]
    [InlineData("Fuck")]
    [InlineData("FUUUCK")]
    [InlineData("f.u.c.k")]
    [InlineData("f u c k")]
    [InlineData("Sh1t Head")]
    [InlineData("$hit")]
    [InlineData("Bitches")]
    [InlineData("Ass")]
    [InlineData("Big Ass")]
    [InlineData("a s s")]
    [InlineData("Dick")]
    [InlineData("Tits")]
    [InlineData("Àss")]
    public void Refuses_an_offensive_name_however_it_is_disguised(string name)
    {
        NameFilter.IsAllowed(name).ShouldBeFalse(name);
    }

    [Theory]
    [InlineData("Ava")]
    [InlineData("Cassandra")]
    [InlineData("Hancock")]
    [InlineData("Dickens")]
    [InlineData("Grape")]
    [InlineData("Essex")]
    [InlineData("Analise")]
    [InlineData("Fagan")]
    [InlineData("Matsui")]
    [InlineData("Mr Bass")]
    [InlineData("Shelly")]
    [InlineData("Titus")]
    [InlineData("Player 1-12")]
    [InlineData("Zoë")]
    public void Lets_innocent_names_through(string name)
    {
        NameFilter.IsAllowed(name).ShouldBeTrue(name);
    }
}
