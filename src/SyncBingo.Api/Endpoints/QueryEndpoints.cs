using System.Security.Claims;
using SyncBingo.Api.Http;
using SyncBingo.Application.Queries;

namespace SyncBingo.Api.Endpoints;

/// <summary>Reading a game (docs/05): the caller's snapshot, and the host's card inspection and leaderboard.</summary>
internal static class QueryEndpoints
{
    public static void MapQueryEndpoints(this RouteGroupBuilder game)
    {
        game.MapGet("/snapshot", (string code, ClaimsPrincipal user, GameQueries queries, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await queries.SnapshotAsync(game, caller, ct)).ToHttp(snapshot => TypedResults.Ok(SnapshotBody.From(snapshot)))));

        game.MapGet("/participants/{id}/card", (string code, string id, ClaimsPrincipal user, GameQueries queries, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await queries.InspectAsync(game, caller, id, ct)).ToHttp(player => TypedResults.Ok(player))));

        game.MapGet("/leaderboard", (string code, ClaimsPrincipal user, GameQueries queries, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await queries.LeaderboardAsync(game, caller, ct)).ToHttp(board => TypedResults.Ok(board))));
    }
}
