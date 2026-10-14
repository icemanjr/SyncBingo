using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using SyncBingo.Api.Endpoints;
using SyncBingo.Application.Ports;
using SyncBingo.Application.Queries;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.Realtime;

/// <summary>
/// Pushes events over SignalR (docs/05 §4). Each event goes to every audience it is for, as
/// <c>event { type, seq, data }</c> with that audience's own <c>seq</c> (ADR 0002), so each client sees
/// an unbroken sequence. Private messages go to one group as <c>notice { type, data }</c>, unnumbered.
/// </summary>
public sealed class SignalREventPublisher(IHubContext<GameHub> hub) : IEventPublisher
{
    private static readonly Audience[] Streams = [Audience.Players, Audience.Host, Audience.Display];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task PublishAsync(GameCode code, IReadOnlyList<SessionEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(events);

        foreach (var e in events)
        {
            foreach (var stream in Streams.Where(stream => e.Audience.HasFlag(stream)))
            {
                var message = new EventMessage(e.GetType().Name, e.Streams.For(stream), Data(e, stream));
                await hub.Clients.Group(GameHub.RoleGroup(code, stream)).SendAsync("event", message, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task NotifyAsync(GameCode code, Recipient recipient, Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(notification);

        var group = recipient switch
        {
            Recipient.Participant player => GameHub.PlayerGroup(code, player.ParticipantId),
            _ => GameHub.RoleGroup(code, Audience.Host),
        };
        var message = new NoticeMessage(notification.GetType().Name, JsonSerializer.SerializeToNode(notification, notification.GetType(), Json));
        return hub.Clients.Group(group).SendAsync("notice", message, cancellationToken);
    }

    /// <summary>
    /// The event's payload for one audience. Players and the display get only the settings they notice,
    /// and wins and removals by name ("Sam had Bingo!"), without internal ids; a removed player knows
    /// it is them by their (unique) name, and their token stops working.
    /// </summary>
    private static JsonNode? Data(SessionEvent e, Audience stream)
    {
        var host = stream == Audience.Host;
        switch (e)
        {
            case SettingsChanged changed:
                return host
                    ? JsonSerializer.SerializeToNode(SettingsBody.From(changed.Settings), Json)
                    : JsonSerializer.SerializeToNode(VisibleSettings.From(changed.Settings), Json);
            case WinAwarded { Win: var win } when !host:
                return JsonSerializer.SerializeToNode(new { name = win.Name, winCondition = win.WinConditionId, wins = win.Wins }, Json);
            case ParticipantRemoved removed when !host:
                return JsonSerializer.SerializeToNode(new { name = removed.Name }, Json);
        }

        var data = JsonSerializer.SerializeToNode(e, e.GetType(), Json)!.AsObject();
        data.Remove("seq");
        data.Remove("audience");
        data.Remove("streams");
        return data;
    }

    public sealed record EventMessage(string Type, long Seq, JsonNode? Data);

    public sealed record NoticeMessage(string Type, JsonNode? Data);
}
