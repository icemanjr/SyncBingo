using SyncBingo.Api.Endpoints;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.IntegrationTests.Http;

/// <summary>Partial settings on the wire: only fields that are sent change, and nothing is lost on the way.</summary>
public sealed class SettingsBodyTests
{
    /// <summary>Every setting away from its default, so a field that falls back to a default shows up.</summary>
    private static readonly SessionSettings Unusual = new()
    {
        AutoCallInterval = TimeSpan.FromSeconds(7),
        ShowBallToPlayers = false,
        HighlightMatches = true,
        ShowCallBoard = false,
        AutoFillAvailable = true,
        BingoButton = false,
        WinHighlight = false,
        WinDetection = WinDetection.AlertHostAndPause,
        RedrawPolicy = RedrawPolicy.ParticipantsMayRedraw,
        FreeMarking = false,
        RequireMarksForClaims = true,
        Unmarking = Unmarking.MismarksOnly,
        FreeSpace = FreeSpace.Earned,
        ShowRoundNumbers = false,
        MultipleWins = MultipleWins.EveryDeclaredWin,
    };

    [Fact]
    public void An_empty_body_changes_nothing()
    {
        new SettingsBody().ApplyTo(Unusual).ShouldBe(Unusual);
    }

    [Fact]
    public void A_full_body_carries_every_setting()
    {
        SettingsBody.From(Unusual).ApplyTo(SessionSettings.Default).ShouldBe(Unusual);
    }
}
