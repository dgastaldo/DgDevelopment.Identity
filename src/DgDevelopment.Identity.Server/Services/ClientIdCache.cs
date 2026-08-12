using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.OAuth.Services;

namespace DgDevelopment.Identity.Server.Services;

internal sealed class ClientIdCache(IClientRepository clientRepository) : IClientIdCache, IDisposable
{
    private readonly PeriodicTimer _refreshTimer = new(TimeSpan.FromMinutes(5));
    private HashSet<string> _clientIds = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await RefreshCacheAsync(ct).ConfigureAwait(false);
        _ = Task.Run(() =>
        {
            return RefreshLoopAsync(ct);
        }, ct);
    }

    public bool IsValidClientId(string clientId)
    {
        _lock.EnterReadLock();
        try
        {
            return _clientIds.Contains(clientId);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private async Task RefreshLoopAsync(CancellationToken ct = default)
    {
        while (await _refreshTimer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                await RefreshCacheAsync(ct).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private async Task RefreshCacheAsync(CancellationToken ct = default)
    {
        var clientIds = await clientRepository.GetAllActiveClientIdsAsync(ct).ConfigureAwait(false);
        var set = new HashSet<string>(clientIds, StringComparer.Ordinal);

        _lock.EnterWriteLock();
        try
        {
            _clientIds = set;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _refreshTimer.Dispose();
        _lock.Dispose();
    }
}
