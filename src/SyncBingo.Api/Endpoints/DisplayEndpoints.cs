using System.Security.Claims;
using Microsoft.Extensions.Options;
using SyncBingo.Api.Http;
using SyncBingo.Application.Displays;
using SyncBingo.Application.Pairing;
using SyncBingo.Application.Sessions;

namespace SyncBingo.Api.Endpoints;

/// <summary>The host's display views (FR-28) and the join note shown on them (FR-49). Host only.</summary>
internal static class DisplayEndpoints
{
    public static void MapDisplayEndpoints(this RouteGroupBuilder game)
    {
        game.MapPost("/display-links", (string code, ClaimsPrincipal user, DisplayHandler displays, IOptions<WebOptions> web, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await displays.CreateLinkAsync(game, caller, ct)).ToHttp(issued => TypedResults.Created(
                    "/api/sessions/" + game.Value + "/displays",
                    new DisplayLinkCreated(issued.DisplayId, new Uri(web.Value.PublicOrigin, $"/display/{game.Value}#t={issued.Token}"))))));

        game.MapDelete("/display-links", (string code, ClaimsPrincipal user, DisplayHandler displays, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await displays.RevokeLinksAsync(game, caller, ct)).NoContent()));

        game.MapGet("/displays", (string code, ClaimsPrincipal user, DisplayHandler displays, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await displays.ListAsync(game, caller, ct)).ToHttp(list => TypedResults.Ok(list))));

        game.MapDelete("/displays/{id}", (string code, string id, ClaimsPrincipal user, DisplayHandler displays, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await displays.DisconnectAsync(game, caller, id, ct)).NoContent()));

        game.MapPost("/display-pairings", (string code, PairingApproval request, ClaimsPrincipal user, PairingHandler pairing, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await pairing.ApproveAsync(game, caller, request.PairingCode, ct)).NoContent()))
            .RequireRateLimiting(RateLimits.Approve);

        game.MapPut("/join-note", (string code, JoinNoteRequest request, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await host.SetJoinNoteAsync(game, caller, request.Note, ct)).NoContent()));
    }

    /// <summary>A TV starts pairing (ADR 0010): anonymous, limited per address.</summary>
    public static void MapTvEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/tv/pairings", async (PairingHandler pairing, CancellationToken ct) =>
        {
            var started = await pairing.StartAsync(ct);
            return TypedResults.Created(
                (string?)null, new PairingCreated(started.PairingId, started.Code.ToString(), started.ExpiresAt));
        }).RequireRateLimiting(RateLimits.Pairing);
    }
}
