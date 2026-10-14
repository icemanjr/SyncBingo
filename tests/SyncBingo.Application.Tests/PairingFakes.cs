using SyncBingo.Application.Pairing;
using SyncBingo.Domain.Pairing;

namespace SyncBingo.Application.Tests;

/// <summary>Pairings in memory, expiring on the test clock like the Redis TTLs.</summary>
internal sealed class InMemoryPairingStore(FakeClock clock) : IPairingStore
{
    private readonly Dictionary<string, (string PairingId, DateTimeOffset Until)> _codes = [];
    private readonly Dictionary<string, DateTimeOffset> _pairings = [];
    private readonly Dictionary<string, (PairingResult Result, DateTimeOffset Until)> _results = [];
    private readonly Lock _gate = new();

    public Task<bool> TryCreateAsync(string pairingId, PairingCode code, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_codes.TryGetValue(code.Value, out var taken) && taken.Until > clock.UtcNow)
            {
                return Task.FromResult(false);
            }

            _codes[code.Value] = (pairingId, clock.UtcNow + lifetime);
            _pairings[pairingId] = clock.UtcNow + lifetime;
            return Task.FromResult(true);
        }
    }

    public Task<string?> TryClaimAsync(PairingCode code, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_codes.Remove(code.Value, out var entry) || entry.Until <= clock.UtcNow)
            {
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(entry.PairingId);
        }
    }

    public Task<bool> ExistsAsync(string pairingId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_pairings.TryGetValue(pairingId, out var until) && until > clock.UtcNow);
        }
    }

    public Task SaveResultAsync(string pairingId, PairingResult result, TimeSpan keepFor, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _results[pairingId] = (result, clock.UtcNow + keepFor);
            return Task.CompletedTask;
        }
    }

    public Task<PairingResult?> TakeResultAsync(string pairingId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_results.Remove(pairingId, out var kept) && kept.Until > clock.UtcNow ? kept.Result : null);
        }
    }
}

internal sealed class RecordingPairingNotifier : IPairingNotifier
{
    public List<(string PairingId, PairingResult Result)> Paired { get; } = [];

    public Task PairedAsync(string pairingId, PairingResult result, CancellationToken cancellationToken)
    {
        Paired.Add((pairingId, result));
        return Task.CompletedTask;
    }
}
