using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SyncBingo.Api.Http;
using SyncBingo.Application.Calling;
using SyncBingo.Application.Players;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain.Content;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.Endpoints;

/// <summary>The host's commands (docs/05 §3). The application refuses every caller but the host.</summary>
internal static class HostEndpoints
{
    public static void MapHostEndpoints(this RouteGroupBuilder game)
    {
        // A custom set (FR-44, ADR 0009). The body is the set file itself, as the editor builds it or a host uploads it.
        game.MapPut("/content", (
            string code,
            [FromBody] JsonElement file,
            [FromQuery] bool? groupItems,
            ClaimsPrincipal user,
            ContentHandler content,
            CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await content.UseAsync(game, caller, file.GetRawText(), groupItems ?? false, ct)).NoContent()))
            .Accepts<JsonElement>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .WithMetadata(new RequestSizeLimitAttribute(ContentSetFile.MaxBytes * 2))
            .RequireRateLimiting(RateLimits.Content)
            .WithName("UseContent");

        game.MapDelete("/content", (string code, ClaimsPrincipal user, ContentHandler content, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await content.UseNumbersAsync(game, caller, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent)
            .RequireRateLimiting(RateLimits.Content)
            .WithName("UseNumbers");

        game.MapPut("/content/sharing", (string code, ContentSharingRequest request, ClaimsPrincipal user, ContentHandler content, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await content.SetSharingAsync(game, caller, request.PlayersMayDownload, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent)
            .WithName("SetContentSharing");

        // The host any time; a player when the host shares the set, in the lobby.
        game.MapGet("/content", (string code, ClaimsPrincipal user, ContentHandler content, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await content.DownloadAsync(game, caller, ct)).ToHttp(download =>
                    TypedResults.File(Encoding.UTF8.GetBytes(download.File), "application/json", FileName(download.Name)))))
            .Produces<JsonElement>(contentType: "application/json")
            .WithName("DownloadContent");

        game.MapPut("/rules", (string code, RulesRequest request, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await host.ApplyRulesAsync(game, caller, settings => request.Settings?.ApplyTo(settings) ?? settings, request.WinCondition, ct))
                    .ToHttp(rules => TypedResults.Ok(new RulesView(SettingsBody.From(rules.Settings), rules.WinConditionId)))))
            .Produces<RulesView>().WithName("ApplyRules");

        game.MapPost("/rounds/countdown", (string code, CountdownRequest request, ClaimsPrincipal user, RoundHandler rounds, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await rounds.StartCountdownAsync(game, caller, request.StartInAuto, ct)).ToHttp(outcome =>
                {
                    var started = outcome.Events.OfType<RoundCountdownStarted>().Single();
                    return TypedResults.Ok(new CountdownStarted(started.Round, started.EndsAt));
                })))
            .Produces<CountdownStarted>().WithName("StartCountdown");

        game.MapDelete("/rounds/countdown", (string code, ClaimsPrincipal user, RoundHandler rounds, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await rounds.CancelCountdownAsync(game, caller, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent).WithName("CancelCountdown");

        game.MapPost("/cards/clear", (string code, ClearCardsRequest request, ClaimsPrincipal user, RoundHandler rounds, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await rounds.ClearCardsAsync(game, caller, request.RedrawCards, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent).WithName("ClearCards");

        game.MapPost("/rounds/current/end", (string code, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await host.EndRoundAsync(game, caller, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent).WithName("EndRound");

        game.MapPost("/auto", (string code, ClaimsPrincipal user, DrawHandler draws, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await draws.StartAutoModeAsync(game, caller, ct)).ToHttp(Ball)))
            .Produces<BallBody>().WithName("StartAutoMode");

        game.MapPost("/pause", (string code, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await host.PauseAsync(game, caller, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent).WithName("Pause");

        game.MapPost("/balls", (string code, ClaimsPrincipal user, DrawHandler draws, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await draws.GrabBallAsync(game, caller, ct)).ToHttp(Ball)))
            .Produces<BallBody>().WithName("GrabBall");

        game.MapPost("/wins", (
            string code,
            AwardRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            ClaimsPrincipal user,
            PlayerManagementHandler manage,
            CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) =>
                (await manage.AwardWinAsync(
                    game, caller, new AwardWin(request.ParticipantId, request.CardId, request.Wins ?? 1, request.ClaimId, idempotencyKey), ct))
                    .NoContent()))
            .Produces(StatusCodes.Status204NoContent).WithName("AwardWin");

        game.MapPost("/claims/{claimId:int}/dismiss", (string code, int claimId, ClaimsPrincipal user, PlayerManagementHandler manage, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await manage.DismissClaimAsync(game, caller, claimId, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent).WithName("DismissClaim");

        game.MapDelete("/participants/{id}", (string code, string id, ClaimsPrincipal user, PlayerManagementHandler manage, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await manage.RemoveParticipantAsync(game, caller, id, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent).WithName("RemovePlayer");

        game.MapPost("/leaderboard/show", (string code, VisibleRequest request, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await host.ShowLeaderboardAsync(game, caller, request.Visible, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent).WithName("ShowLeaderboard");

        game.MapPost("/end", (string code, ClaimsPrincipal user, HostCommandHandler host, CancellationToken ct) =>
            Routes.ForCaller(code, user, async (game, caller) => (await host.EndSessionAsync(game, caller, ct)).NoContent()))
            .Produces(StatusCodes.Status204NoContent).WithName("EndGame");
    }

    /// <summary>"Times tables 1–5.json": the set's name, without characters file systems refuse.</summary>
    private static string FileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var safe = new string([.. name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '-' : c)]).Trim(' ', '.');
        return $"{(safe.Length == 0 ? "SyncBingo set" : safe)}.json";
    }

    private static IResult Ball(CommandOutcome outcome) =>
        TypedResults.Ok(BallBody.From(outcome.Events.OfType<BallCalled>().Single().Call));
}
