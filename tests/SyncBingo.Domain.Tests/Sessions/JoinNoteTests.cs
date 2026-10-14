using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>The note the host shows with the join QR (FR-49).</summary>
public sealed class JoinNoteTests
{
    [Fact]
    public void The_note_reaches_the_host_and_the_display_only()
    {
        var session = NewSession();

        var changed = session.SetJoinNote("  Password: harvest2026  ", Now).Single<JoinNoteChanged>();

        changed.Note.ShouldBe("Password: harvest2026");
        changed.Audience.ShouldBe(Audience.Host | Audience.Display);
        session.JoinNote.ShouldBe("Password: harvest2026");
    }

    [Fact]
    public void It_can_change_even_in_auto_mode()
    {
        InState(SessionState.Auto).SetJoinNote("Hint: our school's name", Now).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_note_clears_it(string? blank)
    {
        var session = NewSession();
        session.SetJoinNote("Password: harvest2026", Now).IsSuccess.ShouldBeTrue();

        session.SetJoinNote(blank, Now).Single<JoinNoteChanged>().Note.ShouldBeNull();

        session.JoinNote.ShouldBeNull();
    }

    [Fact]
    public void A_long_note_is_refused()
    {
        var session = NewSession();

        session.SetJoinNote(new string('x', Session.MaxJoinNoteLength + 1), Now).Error!.Code.ShouldBe(DomainErrorCode.Invalid);
        session.SetJoinNote(new string('x', Session.MaxJoinNoteLength), Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void The_same_note_again_changes_nothing()
    {
        var session = NewSession();
        session.SetJoinNote("Hint", Now).IsSuccess.ShouldBeTrue();

        session.SetJoinNote("Hint", Now).Value.ShouldBeEmpty();
    }

    [Fact]
    public void The_note_survives_a_restore()
    {
        var session = NewSession();
        session.SetJoinNote("Hint", Now).IsSuccess.ShouldBeTrue();

        Session.Restore(session.ToSnapshot()).JoinNote.ShouldBe("Hint");
    }
}
