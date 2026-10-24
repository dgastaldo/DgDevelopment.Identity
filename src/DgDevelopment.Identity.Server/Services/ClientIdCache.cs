using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace DgDevelopment.Identity.Server.Services;

public sealed partial class ClientIdCache : IClientIdCache, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClientIdCache> _logger;
    private readonly PeriodicTimer _refreshTimer = new(TimeSpan.FromMinutes(5));
    private HashSet<string> _clientIds = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public ClientIdCache(IServiceScopeFactory scopeFactory, ILogger<ClientIdCache> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await RefreshCacheAsync(ct).ConfigureAwait(false);
        _ = Task.Run(() => RefreshLoopAsync(ct), ct);
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
            await RefreshCacheAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task RefreshCacheAsync(CancellationToken ct = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IClientRepository>();
            var clientIds = await repository.GetAllActiveClientIdsAsync(ct).ConfigureAwait(false);
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
        catch (DbException ex)
        {
            LogRefreshFailed(ex);
        }
        catch (TimeoutException ex)
        {
            LogRefreshTimedOut(ex);
        }
        catch (OperationCanceledException)
        {
            LogRefreshCancelled();
        }
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Warning, Message = "Failed to refresh client id cache from data source.")]
    private partial void LogRefreshFailed(Exception exception);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Timed out while refreshing client id cache from data source.")]
    private partial void LogRefreshTimedOut(Exception exception);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Client id cache refresh cancelled.")]
    private partial void LogRefreshCancelled();

    public void Dispose()
    {
        _refreshTimer.Dispose();
        _lock.Dispose();
    }
}