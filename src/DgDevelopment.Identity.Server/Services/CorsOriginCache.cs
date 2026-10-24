using DgDevelopment.Identity.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace DgDevelopment.Identity.Server.Services;

public sealed partial class CorsOriginCache : ICorsOriginCache, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CorsOriginCache> _logger;
    private readonly PeriodicTimer _refreshTimer = new(TimeSpan.FromMinutes(5));
    private HashSet<string> _origins = [];
    private HashSet<string> _redirectUris = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public CorsOriginCache(IServiceScopeFactory scopeFactory, ILogger<CorsOriginCache> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await RefreshCacheAsync(ct).ConfigureAwait(false);
        _ = Task.Run(() => RefreshLoopAsync(ct), ct);
    }

    public bool IsAllowedOrigin(string origin)
    {
        _lock.EnterReadLock();
        try
        {
            return _origins.Contains(origin);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool IsAllowedRedirectUri(string uri)
    {
        _lock.EnterReadLock();
        try
        {
            return _redirectUris.Contains(uri);
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
            var redirectUris = await repository.GetAllActiveRedirectUrisAsync(ct).ConfigureAwait(false);
            var origins = redirectUris
                .Select(uri => uri.GetLeftPart(UriPartial.Authority))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var exactUris = redirectUris
                .Select(uri => uri.ToString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _lock.EnterWriteLock();
            try
            {
                _origins = origins;
                _redirectUris = exactUris;
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

    [LoggerMessage(EventId = 1100, Level = LogLevel.Warning, Message = "Failed to refresh CORS origin cache from data source.")]
    private partial void LogRefreshFailed(Exception exception);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "Timed out while refreshing CORS origin cache from data source.")]
    private partial void LogRefreshTimedOut(Exception exception);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "CORS origin cache refresh cancelled.")]
    private partial void LogRefreshCancelled();

    public void Dispose()
    {
        _refreshTimer.Dispose();
        _lock.Dispose();
    }
}
