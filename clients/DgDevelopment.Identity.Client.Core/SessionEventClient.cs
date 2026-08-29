using Microsoft.AspNetCore.SignalR.Client;

namespace DgDevelopment.Identity.Client.Core;

/// <summary>
/// Connects to the IDP's <c>/hubs/session</c> hub (Cookie,Bearer auth server-side - see
/// SessionHub/SignalRSessionEventPublisher) and raises <see cref="ForceLogoutReceived"/> when the
/// server pushes a force-logout event for the subscribed user (their password changed, sessions
/// were revoked, etc.). Shared by every client (MAUI, IdentityPlatform WASM) rather than
/// duplicated per platform, since the protocol - connect, subscribe to a per-user group, listen
/// for one named message - has nothing platform-specific about it.
/// </summary>
public sealed class SessionEventClient : IAsyncDisposable
{
    private HubConnection? _connection;

    /// <summary>Raised when the server pushes a force-logout event for the subscribed user.</summary>
    public event Func<Task>? ForceLogoutReceived;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Opens the connection and subscribes to <paramref name="userId"/>'s channel. Stops any
    /// existing connection first, so it's safe to call again (e.g. after a fresh login).
    /// <paramref name="accessTokenProvider"/> is called on every (re)connect, not just once - the
    /// same access token used at connect time may have expired by the time SignalR needs to
    /// reconnect, so callers should return whatever the current valid token is (refreshing it if
    /// needed) rather than a token captured at the call site.
    /// </summary>
    public async Task StartAsync(string authority, string userId, Func<Task<string?>> accessTokenProvider, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);

        await StopAsync().ConfigureAwait(false);

        var connection = new HubConnectionBuilder()
            .WithUrl($"{authority.TrimEnd('/')}/hubs/session", options => options.AccessTokenProvider = accessTokenProvider)
            .WithAutomaticReconnect()
            .Build();

        connection.On("ForceLogout", async () =>
        {
            if (ForceLogoutReceived is { } handler)
                await handler.Invoke().ConfigureAwait(false);
        });

        connection.Reconnected += _ => connection.InvokeAsync("SubscribeToUser", userId);

        _connection = connection;
        await connection.StartAsync(ct).ConfigureAwait(false);
        await connection.InvokeAsync("SubscribeToUser", userId, ct).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        if (_connection is not { } connection)
            return;

        _connection = null;
        await connection.StopAsync().ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
