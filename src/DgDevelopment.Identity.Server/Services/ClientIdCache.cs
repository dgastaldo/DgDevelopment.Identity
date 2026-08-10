using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.OAuth.Services;

namespace DgDevelopment.Identity.Server.Services;

public sealed class ClientIdCache : IClientIdCache, IDisposable
{
    private readonly IClientRepository _clientRepository;
    private readonly PeriodicTimer _refreshTimer;
    private HashSet<string> _clientIds = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public ClientIdCache(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
        _refreshTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await RefreshCacheAsync(ct).ConfigureAwait(false);
        _ = Task.Run(() => RefreshLoopAsync());
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

    private async Task RefreshLoopAsync()
    {
        while (await _refreshTimer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            try
            {
                await RefreshCacheAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private async Task RefreshCacheAsync(CancellationToken ct = default)
    {
        var clientIds = await _clientRepository.GetAllActiveClientIdsAsync(ct).ConfigureAwait(false);
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
