using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DgDevelopment.Identity.Client.Core;
using Microsoft.AspNetCore.Components.Authorization;

namespace DgDevelopment.Identity.Client.Blazor;

public class IdentityAuthStateProvider(
    IdentityClient client, ITokenStore tokenStore, ISessionMarkerService markerService, SessionEventClient sessionEventClient, OidcOptions options) : AuthenticationStateProvider
{
    private TokenResponse? _tokens;
    private ClaimsPrincipal? _currentUser;
    private UserInfo? _userInfo;
    private bool _forceLogoutHandlerAttached;

    /// <summary>
    /// Raised when the server pushes a force-logout event for this user (password changed
    /// elsewhere, sessions revoked) - tokens are already cleared and auth state already
    /// renotified by the time this fires. Consumers (e.g. MainLayout) subscribe to drive
    /// navigation back to the login page. Only ever raised client-side (WASM) - see
    /// EnsureSessionListenerStartedAsync.
    /// </summary>
    public event Func<Task>? ForceLoggedOut;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_currentUser?.Identity?.IsAuthenticated == true)
        {
            await EnsureSessionListenerStartedAsync().ConfigureAwait(false);
            return new AuthenticationState(_currentUser);
        }

        _tokens = await tokenStore.GetTokensAsync().ConfigureAwait(false);

        if (_tokens == null)
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        if (_tokens.IsExpired() && _tokens.RefreshToken != null)
        {
            try
            {
                _tokens = await client.RefreshTokenAsync(_tokens.RefreshToken).ConfigureAwait(false);
                await tokenStore.SaveTokensAsync(_tokens).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                return await ClearTokensAndReturnAnonymousAsync().ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return await ClearTokensAndReturnAnonymousAsync().ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return await ClearTokensAndReturnAnonymousAsync().ConfigureAwait(false);
            }
        }

        if (_tokens == null || _tokens.IsExpired())
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        if (_userInfo == null)
        {
            try
            {
                _userInfo = await client.GetUserInfoAsync(_tokens.AccessToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                return await ClearTokensAndReturnAnonymousAsync().ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return await ClearTokensAndReturnAnonymousAsync().ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return await ClearTokensAndReturnAnonymousAsync().ConfigureAwait(false);
            }
        }

        _currentUser = BuildPrincipal(_userInfo!, _tokens.AccessToken);

        await markerService.SetAsync(_userInfo!).ConfigureAwait(false);
        await EnsureSessionListenerStartedAsync().ConfigureAwait(false);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        return new AuthenticationState(_currentUser);
    }

    protected virtual async Task<AuthenticationState> ClearTokensAndReturnAnonymousAsync()
    {
        await tokenStore.ClearTokensAsync().ConfigureAwait(false);
        await markerService.ClearAsync().ConfigureAwait(false);
        _tokens = null;
        _currentUser = null;
        _userInfo = null;
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>
    /// Opens (or re-opens) the real-time channel that reports a force-logout the instant it
    /// happens. Only meaningful client-side (WASM) - this base class's GetAuthenticationStateAsync/
    /// CompleteLoginAsync only ever run for real in the browser, since ServerIdentityAuthStateProvider
    /// overrides GetAuthenticationStateAsync entirely for prerender/server rendering (cookie-based,
    /// no real tokens) - the OperatingSystem.IsBrowser() guard is still here so this stays inert in
    /// case that assumption ever changes. A no-op if already connected or there's no signed-in user.
    /// </summary>
    protected async Task EnsureSessionListenerStartedAsync()
    {
        if (!OperatingSystem.IsBrowser())
            return;

        if (!_forceLogoutHandlerAttached)
        {
            sessionEventClient.ForceLogoutReceived += HandleForceLogoutAsync;
            _forceLogoutHandlerAttached = true;
        }

        if (sessionEventClient.IsConnected || _tokens is not { } tokens)
            return;

        var userId = JwtClaimsReader.GetSubject(tokens.AccessToken);
        if (userId is null)
            return;

        await sessionEventClient.StartAsync(options.Authority, userId, GetCurrentAccessTokenAsync).ConfigureAwait(false);
    }

    private async Task<string?> GetCurrentAccessTokenAsync()
    {
        if (_tokens is { } tokens && tokens.IsExpired() && tokens.RefreshToken is not null)
        {
            try
            {
                _tokens = await client.RefreshTokenAsync(tokens.RefreshToken).ConfigureAwait(false);
                await tokenStore.SaveTokensAsync(_tokens).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return null;
            }
        }

        return _tokens?.AccessToken;
    }

    private async Task HandleForceLogoutAsync()
    {
        await ClearTokensAndReturnAnonymousAsync().ConfigureAwait(false);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));

        if (ForceLoggedOut is { } handler)
            await handler.Invoke().ConfigureAwait(false);
    }

    public Uri GetLoginUrl(string? state = null, string? codeChallenge = null, string? tenant = null)
    {
        return new Uri(client.GetAuthorizeUrl(state, codeChallenge, tenant) + "&nonce=" + Guid.NewGuid().ToString("N"));
    }

    public async Task CompleteLoginAsync(string code, string codeVerifier, Uri? redirectUri = null)
    {
        _tokens = await client.ExchangeCodeAsync(code, codeVerifier, redirectUri).ConfigureAwait(false);
        await tokenStore.SaveTokensAsync(_tokens).ConfigureAwait(false);

        try
        {
            _userInfo = await client.GetUserInfoAsync(_tokens.AccessToken).ConfigureAwait(false);
        }
        catch
        {
            await tokenStore.ClearTokensAsync().ConfigureAwait(false);
            _tokens = null;
            throw;
        }

        _currentUser = BuildPrincipal(_userInfo, _tokens.AccessToken);

        await markerService.SetAsync(_userInfo!).ConfigureAwait(false);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public async Task LogoutAsync()
    {
        await sessionEventClient.StopAsync().ConfigureAwait(false);
        await tokenStore.ClearTokensAsync().ConfigureAwait(false);
        await markerService.ClearAsync().ConfigureAwait(false);
        _tokens = null;
        _currentUser = null;
        _userInfo = null;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
    }

    public static (string codeVerifier, string codeChallenge) GeneratePkce()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Convert.ToBase64String(bytes).Replace("+", "-", StringComparison.Ordinal).Replace("/", "_", StringComparison.Ordinal).TrimEnd('=');

        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Convert.ToBase64String(challengeBytes).Replace("+", "-", StringComparison.Ordinal).Replace("/", "_", StringComparison.Ordinal).TrimEnd('=');

        return (verifier, challenge);
    }

    protected static ClaimsPrincipal BuildPrincipal(UserInfo? userInfo, string? accessToken = null)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrEmpty(userInfo?.Sub))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userInfo.Sub));
        if (!string.IsNullOrEmpty(userInfo?.Name))
            claims.Add(new Claim(ClaimTypes.Name, userInfo.Name));
        if (!string.IsNullOrEmpty(userInfo?.Email))
            claims.Add(new Claim(ClaimTypes.Email, userInfo.Email));

        // Read directly off the access token (unverified - see JwtClaimsReader) rather than adding a
        // round trip: /connect/userinfo doesn't carry permissions, and this is UI-gating only, not a
        // security boundary (every API call is still enforced server-side). Not available during SSR
        // prerender (no accessToken there), so permission-gated nav briefly hides until the WASM
        // client goes interactive and re-runs this with the real token.
        foreach (var permission in JwtClaimsReader.GetPermissions(accessToken))
            claims.Add(new Claim("permission", permission));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "oidc"));
    }
}