using SyncBingo.Domain.Content;
using SyncBingo.Domain.Sessions;
using SyncBingo.Domain.Tests.Content;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Sessions;

/// <summary>A game played with a custom set (ADR 0009, FR-44).</summary>
public sealed class ContentSessionTests
{
    private static ContentSet Sums()
    {
        var entries = ContentTestKit.Words(23);
        entries.Add(new ContentEntry("2 + 5", "7"));
        return ContentTestKit.Valid(entries, "Sums and words");
    }

    [Fact]
    public void Using_a_set_in_the_lobby_switches_the_balls_to_its_items()
    {
        var session = NewSession();

        var events = session.UseContent(Sums(), groupItems: false, Now).Value;

        events.ShouldHaveSingleItem().ShouldBeOfType<ContentChanged>().ShouldBe(
            new ContentChanged("Sums and words", 24, false) with { Seq = events[0].Seq, Audience = events[0].Audience, Streams = events[0].Streams });
        events[0].Audience.ShouldBe(Audience.Everyone);
        session.Content!.Name.ShouldBe("Sums and words");
        session.Variant.BallMax.ShouldBe(24);
        session.BallsRemaining.ShouldBe(24);
    }

    [Theory]
    [InlineData(SessionState.Starting)]
    [InlineData(SessionState.Manual)]
    [InlineData(SessionState.Auto)]
    [InlineData(SessionState.Ended)]
    public void The_set_changes_only_in_the_lobby(SessionState state)
    {
        var session = InState(state);

        var result = session.UseContent(Sums(), groupItems: false, Now);

        result.Error!.Code.ShouldBe(DomainErrorCode.WrongState);
        session.Content.ShouldBeNull();
    }

    [Fact]
    public void A_set_that_doesnt_fit_changes_nothing()
    {
        var session = NewSession();

        var result = session.UseContent(Sums(), groupItems: true, Now);

        result.Error!.Message.ShouldBe("Group items needs every entry in a group.");
        session.Content.ShouldBeNull();
        session.Version.ShouldBe(0);
    }

    [Fact]
    public void A_called_ball_carries_what_is_called_and_what_is_marked()
    {
        var session = NewSession();
        session.UseContent(Sums(), groupItems: false, Now);
        session.NextRound(Now);

        var calls = Enumerable.Range(0, 24).Select(_ => session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now).Value.OfType<BallCalled>().Single().Call).ToList();

        var sum = calls.Single(call => call.Number == 24);
        sum.Call.ShouldBe("2 + 5");
        sum.Square.ShouldBe("7");
        sum.Letter.ShouldBe(string.Empty);
        session.BallsRemaining.ShouldBe(0);
    }

    [Fact]
    public void Grouped_balls_are_labelled_with_their_group()
    {
        var session = NewSession();
        session.UseContent(ContentTestKit.Valid(ContentTestKit.TimesTables()), groupItems: true, Now);
        session.NextRound(Now);

        var call = session.DrawBall(DrawSource.Manual, new SeededRandom(3), Now).Value.OfType<BallCalled>().Single().Call;

        call.Letter.ShouldBe(session.Content![call.Number].Group);
        session.GroupItems.ShouldBeTrue();
    }

    [Fact]
    public void Changing_the_set_clears_the_last_rounds_calls()
    {
        var session = NewSession();
        session.NextRound(Now);
        session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now);
        session.EndRound(Now);
        session.Calls.ShouldNotBeEmpty();

        session.UseContent(Sums(), groupItems: false, Now);

        session.Calls.ShouldBeEmpty();
        session.BallsRemaining.ShouldBe(24);
    }

    [Fact]
    public void Each_round_records_the_set_it_used()
    {
        var session = NewSession();
        session.NextRound(Now);
        session.ClearCards(redrawCards: false, Now);
        session.UseContent(Sums(), groupItems: false, Now);
        session.NextRound(Now);

        session.Rounds.Select(round => round.ContentName).ShouldBe([null, "Sums and words"]);
    }

    [Fact]
    public void Back_to_numbers()
    {
        var session = NewSession();
        session.UseContent(Sums(), groupItems: false, Now);

        var events = session.UseContent(null, groupItems: true, Now).Value;

        events.ShouldHaveSingleItem().ShouldBeOfType<ContentChanged>().Name.ShouldBeNull();
        session.Content.ShouldBeNull();
        session.GroupItems.ShouldBeFalse();
        session.Variant.Id.ShouldBe("classic-75");
        session.BallsRemaining.ShouldBe(75);
    }

    [Fact]
    public void A_stored_session_comes_back_with_its_set()
    {
        var session = NewSession();
        var set = ContentTestKit.Valid(ContentTestKit.TimesTables(), "Tables");
        session.UseContent(set, groupItems: true, Now);
        session.NextRound(Now);
        session.DrawBall(DrawSource.Manual, new SeededRandom(1), Now);

        var snapshot = session.ToSnapshot();
        var restored = Session.Restore(snapshot, set);

        snapshot.VariantId.ShouldBe("classic-75");
        snapshot.ContentName.ShouldBe("Tables");
        restored.Content.ShouldBeSameAs(set);
        restored.GroupItems.ShouldBeTrue();
        restored.Variant.Columns.ShouldBe(session.Variant.Columns);
        restored.Calls.ShouldBe(session.Calls);
        restored.BallsRemaining.ShouldBe(24);
    }

    [Fact]
    public void A_stored_session_missing_its_set_is_a_corrupt_record()
    {
        var session = NewSession();
        session.UseContent(Sums(), groupItems: false, Now);

        Should.Throw<InvalidOperationException>(() => Session.Restore(session.ToSnapshot()))
            .Message.ShouldContain("custom set that wasn't found");
    }
}

/// <summary>"Call board shows" (FR-29): which face of a set's items the boards show.</summary>
public sealed class BoardSideTests
{
    [Fact]
    public void Boards_show_the_ball_side_until_the_host_chooses_otherwise() =>
        NewSession().BoardSide.ShouldBe(BoardSide.Ball);

    [Theory]
    [InlineData(SessionState.BetweenRounds)]
    [InlineData(SessionState.Starting)]
    [InlineData(SessionState.Manual)]
    [InlineData(SessionState.Auto)]
    public void The_host_switches_it_at_any_time_but_after_the_game(SessionState state)
    {
        var session = InState(state);

        var events = session.SetBoardSide(BoardSide.Square, Now).Value;

        events.ShouldHaveSingleItem().ShouldBeOfType<BoardSideChanged>().Side.ShouldBe(BoardSide.Square);
        events[0].Audience.ShouldBe(Audience.Everyone, "every board changes");
        session.BoardSide.ShouldBe(BoardSide.Square);
    }

    [Fact]
    public void Not_once_the_game_has_ended() =>
        InState(SessionState.Ended).SetBoardSide(BoardSide.Square, Now).Error!.Code.ShouldBe(DomainErrorCode.WrongState);

    [Fact]
    public void Choosing_the_side_already_shown_changes_nothing()
    {
        var session = NewSession();

        session.SetBoardSide(BoardSide.Ball, Now).Value.ShouldBeEmpty();
        session.Version.ShouldBe(0);
    }

    [Fact]
    public void Only_the_two_sides() =>
        NewSession().SetBoardSide((BoardSide)7, Now).Error!.Code.ShouldBe(DomainErrorCode.Invalid);

    [Fact]
    public void A_stored_session_keeps_it()
    {
        var session = NewSession();
        session.SetBoardSide(BoardSide.Square, Now);

        Session.Restore(session.ToSnapshot()).BoardSide.ShouldBe(BoardSide.Square);
    }
}
