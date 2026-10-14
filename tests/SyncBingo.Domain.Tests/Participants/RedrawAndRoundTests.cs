using SyncBingo.Domain.Cards;
using SyncBingo.Domain.Participants;
using SyncBingo.Domain.Sessions;
using static SyncBingo.Domain.Tests.Participants.PlayerTestKit;
using static SyncBingo.Domain.Tests.Sessions.SessionTestKit;

namespace SyncBingo.Domain.Tests.Participants;

public sealed class RedrawAndRoundTests
{
    private static readonly Card OtherCard = new(Enumerable.Range(0, 25).Select(i => i == 12 ? 0 : KnownCard.Cells[i] + 5));

    [Fact]
    public void Joining_deals_one_card_with_the_free_space_marked()
    {
        var session = NewSession();

        var player = Participant.Join("p1", Name("Ava"), KnownCard, session, Now);

        player.Cards.ShouldHaveSingleItem().Card.ShouldBe(KnownCard);
        player.Cards[0].Marks.ShouldBe(Bits(Free));
        player.AutoFillOn.ShouldBeFalse();
    }

    [Fact]
    public void Players_may_redraw_between_rounds_when_the_host_allows_it()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { RedrawPolicy = RedrawPolicy.ParticipantsMayRedraw });
        kit.Session.EndRound(Now).IsSuccess.ShouldBeTrue();

        var replacement = kit.Player.RedrawCard(1, kit.Session, () => OtherCard).Value;

        replacement.ShouldBe(new CardReplacement(1, KnownCard, OtherCard));
        kit.Card.Card.ShouldBe(OtherCard);
        kit.Marks.ShouldBe(Bits(Free));
    }

    [Fact]
    public void A_refused_redraw_never_deals_a_card()
    {
        var kit = new PlayerTestKit(); // host controls redraws
        kit.Session.EndRound(Now).IsSuccess.ShouldBeTrue();
        var dealt = false;

        kit.Player.RedrawCard(1, kit.Session, () => { dealt = true; return OtherCard; })
            .Error!.Code.ShouldBe(DomainErrorCode.NotAllowed);

        dealt.ShouldBeFalse("no fingerprint may be claimed for a refused redraw");
    }

    [Fact]
    public void Redrawing_mid_round_is_refused()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { RedrawPolicy = RedrawPolicy.ParticipantsMayRedraw });

        kit.Player.RedrawCard(1, kit.Session, () => OtherCard).Error!.Code.ShouldBe(DomainErrorCode.WrongState);
    }

    [Fact]
    public void Redrawing_during_the_countdown_says_the_cards_are_locked()
    {
        var kit = new PlayerTestKit(SessionSettings.Default with { RedrawPolicy = RedrawPolicy.ParticipantsMayRedraw });
        kit.Session.ClearCards(redrawCards: false, Now).IsSuccess.ShouldBeTrue();
        kit.Session.StartCountdown(startInAuto: false, Now).IsSuccess.ShouldBeTrue();

        kit.Player.CheckRedraw(1, kit.Session).Error!.Message.ShouldBe("Cards are locked once the round countdown starts.");
    }

    [Fact]
    public void A_new_round_clears_marks_under_the_new_rounds_settings()
    {
        var kit = new PlayerTestKit();
        kit.Call(1);
        kit.Mark(0);
        kit.Session.ClearCards(redrawCards: false, Now).IsSuccess.ShouldBeTrue();
        kit.Session.ChangeSettings(SessionSettings.Default with { FreeSpace = FreeSpace.Earned }, Now).IsSuccess.ShouldBeTrue();
        kit.Session.NextRound(Now);

        kit.Player.ResetForNewRound(kit.Session, deal: null).ShouldBeEmpty();

        kit.Marks.ShouldBe(0u, "an earned free space starts unmarked");
        kit.Card.Card.ShouldBe(KnownCard);
        kit.Card.IssuedInRound.ShouldBe(1, "dealt in the lobby for round 1");
    }

    [Fact]
    public void Clear_cards_with_redraw_replaces_every_card_for_the_next_round()
    {
        var kit = new PlayerTestKit();
        kit.Session.ClearCards(redrawCards: true, Now).IsSuccess.ShouldBeTrue();

        var replaced = kit.Player.ResetForNewRound(kit.Session, () => OtherCard);

        replaced.ShouldBe([new CardReplacement(1, KnownCard, OtherCard)]);
        kit.Card.IssuedInRound.ShouldBe(2);
    }
}
