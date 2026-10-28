using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Participants.PlayerTestKit;

namespace SyncBingo.Domain.Tests.Participants;

/// <summary>Free marking (FR-22) and unmarking (FR-24), in every combination.</summary>
public sealed class MarkingTests
{
    private const int CalledCell = 0; // ball 1, called in each test
    private const int UncalledCell = 1; // ball 16, never called

    /// <summary>free marking, unmarking, mark uncalled?, unmark called?, unmark mismark?</summary>
    public static TheoryData<bool, Unmarking, bool, bool, bool> Combinations => new()
    {
        { true, Unmarking.Any, true, true, true },
        { true, Unmarking.MismarksOnly, true, false, true },
        { true, Unmarking.Off, true, false, false },
        { false, Unmarking.Any, false, true, false },
        { false, Unmarking.MismarksOnly, false, false, false },
        { false, Unmarking.Off, false, false, false },
    };

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Each_combination_of_settings_behaves_as_specified(
        bool freeMarking, Unmarking unmarking, bool canMarkUncalled, bool canUnmarkCalled, bool canUnmarkMismark)
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { FreeMarking = freeMarking, Unmarking = unmarking });
        kit.Call(1);

        kit.Mark(CalledCell).IsSuccess.ShouldBeTrue("a called number can always be marked");
        kit.Mark(UncalledCell).IsSuccess.ShouldBe(canMarkUncalled, "mark an uncalled number");
        kit.Unmark(CalledCell).IsSuccess.ShouldBe(canUnmarkCalled, "unmark a called number");

        if (canMarkUncalled)
        {
            kit.Unmark(UncalledCell).IsSuccess.ShouldBe(canUnmarkMismark, "unmark a mismark");
        }
        else
        {
            canUnmarkMismark.ShouldBeFalse("no mismark can exist without free marking");
        }
    }

    [Fact]
    public void Refusals_explain_themselves_and_leave_the_card_alone()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { FreeMarking = false });

        var error = kit.Mark(UncalledCell).Error!;

        error.Code.ShouldBe(DomainErrorCode.NotAllowed);
        error.Message.ShouldContain("Only called squares");
        kit.Marks.ShouldBe(Bits(Free));
    }

    [Fact]
    public void Marking_twice_or_unmarking_an_unmarked_cell_changes_nothing()
    {
        var kit = new PlayerTestKit();
        kit.Call(1);

        kit.Mark(CalledCell).Value.ShouldBe(Bits(Free, CalledCell));
        kit.Mark(CalledCell).Value.ShouldBe(Bits(Free, CalledCell));
        kit.Unmark(UncalledCell).Value.ShouldBe(Bits(Free, CalledCell));
    }

    [Fact]
    public void An_always_marked_free_space_can_never_be_unmarked()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { Unmarking = Unmarking.Any });

        kit.Unmark(Free).Error!.Code.ShouldBe(DomainErrorCode.NotAllowed);
        kit.Marks.ShouldBe(Bits(Free));
    }

    [Fact]
    public void Marking_is_only_possible_during_a_round()
    {
        var kit = new PlayerTestKit();
        kit.Session.EndRound(Sessions.SessionTestKit.Now).IsSuccess.ShouldBeTrue();

        kit.Mark(CalledCell).Error!.Code.ShouldBe(DomainErrorCode.WrongState);
        kit.Unmark(CalledCell).Error!.Code.ShouldBe(DomainErrorCode.WrongState);
        kit.Undo().Error!.Code.ShouldBe(DomainErrorCode.WrongState);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(25)]
    public void Cells_off_the_card_are_refused(int cell)
    {
        new PlayerTestKit().Mark(cell).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
    }

    [Fact]
    public void Unknown_cards_are_refused()
    {
        var kit = new PlayerTestKit();

        kit.Player.Mark(cardId: 2, cell: 0, kit.Session).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
    }
}
