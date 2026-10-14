using System.Security.Claims;
using SyncBingo.Api.Http;
using SyncBingo.Application.Calling;
using SyncBingo.Application.Players;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.Endpoints;

/// <summary>The host's commands (docs/05 §3). The application refuses every caller but the host.</summary>
internal static class HostEndpoints
{
    public static void MapHostEndpoints(this RouteGroupBuilder game)
    {
        game.MapPut("/rules", (string code, RulesRequest request, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await host.ApplyRulesAsync(game, caller, settings => request.Settings?.ApplyTo(settings) ?? settings, request.WinCondition, ct))
                    .ToHttp(rules => TypedResults.Ok(new RulesView(SettingsBody.From(rules.Settings), rules.WinConditionId)))));

        game.MapPost("/rounds/countdown", (string code, CountdownRequest request, ClaimsPrincipal user, RoundHandler rounds, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await rounds.StartCountdownAsync(game, caller, request.StartInAuto, ct)).ToHttp(outcome =>
                {
                    var started = outcome.Events.OfType<RoundCountdownStarted>().Single();
                    return TypedResults.Ok(new CountdownStarted(started.Round, started.EndsAt));
                })));

        game.MapDelete("/rounds/countdown", (string code, ClaimsPrincipal user, RoundHandler rounds, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await rounds.CancelCountdownAsync(game, caller, ct)).NoContent()));

        game.MapPost("/cards/clear", (string code, ClearCardsRequest request, ClaimsPrincipal user, RoundHandler rounds, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await rounds.ClearCardsAsync(game, caller, request.RedrawCards, ct)).NoContent()));

        game.MapPost("/rounds/current/end", (string code, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await host.EndRoundAsync(game, caller, ct)).NoContent()));

        game.MapPost("/auto", (string code, ClaimsPrincipal user, DrawHandler draws, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await draws.StartAutoModeAsync(game, caller, ct)).ToHttp(Ball)));

        game.MapPost("/pause", (string code, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await host.PauseAsync(game, caller, ct)).NoContent()));

        game.MapPost("/balls", (string code, ClaimsPrincipal user, DrawHandler draws, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await draws.GrabBallAsync(game, caller, ct)).ToHttp(Ball)));

        game.MapPost("/wins", (string code, AwardRequest request, ClaimsPrincipal user, PlayerManagementHandler manage, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await manage.AwardWinAsync(game, caller, new AwardWin(request.ParticipantId, request.CardId, request.Wins ?? 1, request.ClaimId), ct))
                    .NoContent()));

        game.MapPost("/claims/{claimId:int}/dismiss", (string code, int claimId, ClaimsPrincipal user, PlayerManagementHandler manage, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await manage.DismissClaimAsync(game, caller, claimId, ct)).NoContent()));

        game.MapDelete("/participants/{id}", (string code, string id, ClaimsPrincipal user, PlayerManagementHandler manage, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await manage.RemoveParticipantAsync(game, caller, id, ct)).NoContent()));

        game.MapPost("/leaderboard/show", (string code, VisibleRequest request, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await host.ShowLeaderboardAsync(game, caller, request.Visible, ct)).NoContent()));

        game.MapPost("/end", (string code, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await host.EndSessionAsync(game, caller, ct)).NoContent()));
    }

    private static IResult Ball(CommandOutcome outcome) =>
        TypedResults.Ok(BallBody.From(outcome.Events.OfType<BallCalled>().Single().Call));
}
