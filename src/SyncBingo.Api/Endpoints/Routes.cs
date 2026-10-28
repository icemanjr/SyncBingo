using System.Security.Claims;
using SyncBingo.Api.Auth;
using SyncBingo.Api.Http;
using SyncBingo.Application;
using SyncBingo.Domain;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.Endpoints;

/// <summary>Helpers shared by the endpoint groups.</summary>
internal static class Routes
{
    private static readonly IResult UnknownGame = Problems.From(Errors.SessionNotFound());

    /// <summary>Runs <paramref name="handle"/> with the route's game code and the signed-in caller; an unparseable code is a 404.</summary>
    public static Task<IResult> ForCaller(string code, ClaimsPrincipal user, Func<GameCode, Caller, Task<IResult>> handle) =>
        GameCode.TryParse(code, out var parsed)
            ? handle(parsed, SessionTokenAuthentication.CallerOf(user))
            : Task.FromResult(UnknownGame);

    /// <summary>204 on success; the events go out over SignalR.</summary>
    public static IResult NoContent<T>(this Result<T> result) => result.ToHttp(_ => TypedResults.NoContent());
}
