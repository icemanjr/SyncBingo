using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SyncBingo.Application.Pairing;

namespace SyncBingo.Api.Realtime;

/// <summary>
/// Where a TV waits to be paired (ADR 0010): anonymous, server to client only, and able to deliver only
/// the result of the one pairing it names (<c>?pairingId=</c>, a secret only that TV knows). A result
/// approved before the TV connected is handed over on connect; an unknown or expired pairing gets
/// <c>PairingExpired</c> and the connection closes.
/// </summary>
[AllowAnonymous]
public sealed class PairingHub(IPairingStore pairings) : Hub
{
    public static string Group(string pairingId) => $"pairing:{pairingId}";

    public override async Task OnConnectedAsync()
    {
        var pairingId = Context.GetHttpContext()?.Request.Query["pairingId"].ToString() ?? string.Empty;
        var aborted = Context.ConnectionAborted;
        if (pairingId.Length != 64 || !pairingId.All(char.IsAsciiHexDigitLower))
        {
            await ExpiredAsync().ConfigureAwait(false);
            return;
        }

        // Join first, so a result approved from now on is pushed; then collect one approved earlier.
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(pairingId), aborted).ConfigureAwait(false);
        if (await pairings.TakeResultAsync(pairingId, aborted).ConfigureAwait(false) is { } result)
        {
            await Clients.Caller.SendAsync("Paired", result, aborted).ConfigureAwait(false);
        }
        else if (!await pairings.ExistsAsync(pairingId, aborted).ConfigureAwait(false))
        {
            await ExpiredAsync().ConfigureAwait(false);
            return;
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    private async Task ExpiredAsync()
    {
        await Clients.Caller.SendAsync("PairingExpired", Context.ConnectionAborted).ConfigureAwait(false);
        Context.Abort();
    }
}

/// <summary>Pushes a pairing's result to the TV waiting on it, through the backplane, on any instance.</summary>
public sealed class SignalRPairingNotifier(IHubContext<PairingHub> hub) : IPairingNotifier
{
    public Task PairedAsync(string pairingId, PairingResult result, CancellationToken cancellationToken) =>
        hub.Clients.Group(PairingHub.Group(pairingId)).SendAsync("Paired", result, cancellationToken);
}
