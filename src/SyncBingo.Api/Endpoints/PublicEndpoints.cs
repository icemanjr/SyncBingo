using Microsoft.Extensions.Options;
using SyncBingo.Api.Http;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.Endpoints;

/// <summary>Creating and joining games (docs/05 §1): no token needed.</summary>
internal static class PublicEndpoints
{
    public static void MapPublicEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/sessions", async (CreateGameRequest request, CreateSessionHandler create, IOptions<WebOptions> web, CancellationToken ct) =>
        {
            var created = await create.HandleAsync(
                new CreateSession(request.Password, request.Settings?.ApplyTo(SessionSettings.Default), request.WinCondition ?? "normal"), ct);
            return created.ToHttp(game => TypedResults.Created(
                $"/api/sessions/{game.Code.Value}",
                new GameCreated(game.Code.ToString(), new Uri(web.Value.PublicOrigin, $"/join/{game.Code.Value}"), game.HostToken)));
        });

        api.MapGet("/sessions/{code}/join-info", async (string code, JoinInfoQuery query, CancellationToken ct) =>
            (await query.GetAsync(code, ct)).ToHttp(info => TypedResults.Ok(info)));

        api.MapPost("/sessions/{code}/join", async (string code, JoinRequest request, JoinSessionHandler join, CancellationToken ct) =>
            (await join.HandleAsync(new JoinSession(code, request.Password, request.Name), ct))
                .ToHttp(joined => TypedResults.Ok(new Joined(joined.ParticipantId, joined.Token))));
    }
}

/// <summary>Where the web app lives, for links such as the QR's join URL.</summary>
public sealed class WebOptions
{
    public Uri PublicOrigin { get; set; } = new("http://localhost:8080");
}
