using System.Security.Claims;
using SyncBingo.Api.Http;
using SyncBingo.Application.Players;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.Endpoints;

/// <summary>
/// A player's own commands (docs/05 §2). The routes carry no participant id: whose cards these
/// are comes from the token, and the application refuses anyone but a participant.
/// </summary>
internal static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this RouteGroupBuilder game)
    {
        game.MapPost("/marks", (string code, MarkRequest request, ClaimsPrincipal user, PlayerCommandHandler players, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await players.MarkAsync(game, caller, request.CardId, request.Cell, request.Marked, ct))
                    .ToHttp(marks => TypedResults.Ok(new MarksBody(marks))))).RequireRateLimiting(RateLimits.Player)
            .Produces<MarksBody>().WithName("Mark");

        game.MapPost("/cards/{cardId:int}/undo", (string code, int cardId, ClaimsPrincipal user, PlayerCommandHandler players, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await players.UndoAsync(game, caller, cardId, ct)).ToHttp(marks => TypedResults.Ok(new MarksBody(marks))))).RequireRateLimiting(RateLimits.Player)
            .Produces<MarksBody>().WithName("Undo");

        game.MapPost("/claims", (string code, CardRequest request, ClaimsPrincipal user, PlayerCommandHandler players, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await players.ClaimBingoAsync(game, caller, request.CardId, ct)).ToHttp(outcome =>
                    TypedResults.Accepted((string?)null, new ClaimAccepted(outcome.Events.OfType<WinClaimed>().Single().Claim.ClaimedAt))))).RequireRateLimiting(RateLimits.Player)
            .Produces<ClaimAccepted>(StatusCodes.Status202Accepted).WithName("ClaimBingo");

        game.MapDelete("/me", (string code, ClaimsPrincipal user, PlayerCommandHandler players, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await players.LeaveAsync(game, caller, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent).WithName("LeaveGame");

        game.MapPut("/me/auto-fill", (string code, AutoFillBody request, ClaimsPrincipal user, PlayerCommandHandler players, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await players.SetAutoFillAsync(game, caller, request.On, ct)).ToHttp(on => TypedResults.Ok(new AutoFillBody(on)))))
            .Produces<AutoFillBody>().WithName("SetAutoFill");

        game.MapPost("/cards/redraw", (string code, CardRequest request, ClaimsPrincipal user, PlayerCommandHandler players, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await players.RedrawCardAsync(game, caller, request.CardId, ct))
                    .ToHttp(card => TypedResults.Ok(new CardBody(request.CardId, card.Cells)))))
            .Produces<CardBody>().WithName("RedrawCard");
    }
}
