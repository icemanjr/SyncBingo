using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SyncBingo.Api.Auth;
using SyncBingo.Application;
using SyncBingo.Domain.Sessions;

namespace SyncBingo.Api.Realtime;

/// <summary>
/// The game's event hub (ADR 0002): server to client only, so it has no methods clients can call.
/// On connect the caller joins the group for its role in its own game, and a player also joins a
/// group of their own for private messages (auto-fill, a replaced card, a private win, removal).
/// </summary>
[Authorize]
public sealed class GameHub : Hub
{
    public static string RoleGroup(GameCode code, Audience stream) => $"s:{code.Value}:{stream.ToString().ToLowerInvariant()}";

    public static string PlayerGroup(GameCode code, string participantId) => $"s:{code.Value}:p:{participantId}";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;
        var code = SessionTokenAuthentication.CodeOf(user);
        var caller = SessionTokenAuthentication.CallerOf(user);
        var stream = caller.Role switch
        {
            CallerRole.Host => Audience.Host,
            CallerRole.Participant => Audience.Players,
            CallerRole.Display => Audience.Display,
            _ => Audience.None,
        };
        if (stream == Audience.None)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(code, stream)).ConfigureAwait(false);
        if (caller.ParticipantId is { } participantId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(code, participantId)).ConfigureAwait(false);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}
