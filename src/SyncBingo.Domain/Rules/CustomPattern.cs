namespace SyncBingo.Domain.Rules;

/// <summary>
/// A win pattern the host drew for this game (FR-21a): a name and the squares it needs, on the card size it
/// was drawn for. It is offered only while the game uses that size, and selected like any other pattern.
/// </summary>
public sealed record CustomPattern(string Id, string Name, CardSize Size, uint Mask)
{
    public WinPattern ToWinPattern() => new(Id, Name, [Mask]);
}
