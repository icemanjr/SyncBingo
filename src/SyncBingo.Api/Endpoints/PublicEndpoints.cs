using Microsoft.Extensions.Options;
using SyncBingo.Api.Http;
using SyncBingo.Application;
using SyncBingo.Application.Queries;
using SyncBingo.Application.Sessions;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.Endpoints;

/// <summary>Creating and joining games (docs/05 §1): no token needed.</summary>
internal static class PublicEndpoints
{
    public static void MapPublicEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/sessions", async (
            CreateGameRequest request, CreateSessionHandler create, GameQueries queries, IOptions<WebOptions> web, CancellationToken ct) =>
        {
            var created = await create.HandleAsync(
                new CreateSession(request.Password, request.Settings?.ApplyTo(SessionSettings.Default), request.WinCondition ?? "normal"), ct);
            if (created.Error is { } error)
            {
                return Problems.From(error);
            }

            var game = created.Value;
            return (await queries.SnapshotAsync(game.Code, Caller.Host, ct)).ToHttp(snapshot => TypedResults.Created(
                $"/api/sessions/{game.Code.Value}",
                new GameCreated(
                    game.Code.ToString(), new Uri(web.Value.PublicOrigin, $"/join/{game.Code.Value}"), game.HostToken, SnapshotBody.From(snapshot))));
        });

        api.MapGet("/sessions/{code}/join-info", async (string code, JoinInfoQuery query, CancellationToken ct) =>
            (await query.GetAsync(code, ct)).ToHttp(info => TypedResults.Ok(info)));

        api.MapPost("/sessions/{code}/join", async (string code, JoinRequest request, JoinSessionHandler join, GameQueries queries, CancellationToken ct) =>
        {
            var joined = await join.HandleAsync(new JoinSession(code, request.Password, request.Name), ct);
            if (joined.Error is { } error)
            {
                return Problems.From(error);
            }

            var player = joined.Value;
            return (await queries.SnapshotAsync(player.Code, Caller.Participant(player.ParticipantId), ct))
                .ToHttp(snapshot => TypedResults.Ok(new Joined(player.ParticipantId, player.Token, SnapshotBody.From(snapshot))));
        });
    }
}

/// <summary>Where the web app lives, for links such as the QR's join URL.</summary>
public sealed class WebOptions
{
    public Uri PublicOrigin { get; set; } = new("http://localhost:8080");
}
