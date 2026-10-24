namespace DgDevelopment.Identity.Client.Maui.Services;

using System.Diagnostics.CodeAnalysis;
using DgDevelopment.Identity.Client.Core;
using Microsoft.Maui.Authentication;

/// <summary>
/// Drives the native OAuth login (via <see cref="WebAuthenticator"/> + PKCE, no client secret -
/// the server-seeded MAUI client is <c>ClientType.Public</c>) and holds the current session's
/// tokens. Pages call <see cref="EnsureFreshTokenAsync"/> before making an authenticated API call;
/// <see cref="IdentityAuthHandler"/> handles attaching whatever token is currently stored to every
/// request and clearing it on a 401.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "Must stay public: it's a constructor parameter type on LoginPage/MainPage/MfaPage, which themselves must stay public for XAML source generation.")]
public sealed class AuthSession(IdentityClient identityClient, ITokenStore tokenStore, OidcOptions options, SessionEventClient sessionEventClient)
{
    private TokenResponse? _tokens;
    private bool _forceLogoutHandlerAttached;

    /// <summary>
    /// Raised when the server pushes a force-logout event for this device (password changed
    /// elsewhere, sessions revoked) - tokens are already cleared by the time this fires. The app
    /// (App.xaml.cs) subscribes to drive navigation back to the login page.
    /// </summary>
    public event EventHandler? ForceLoggedOut;

    public async Task<bool> IsAuthenticatedAsync()
    {
        _tokens ??= await tokenStore.GetTokensAsync().ConfigureAwait(false);
        return _tokens is not null;
    }

    public async Task LoginAsync()
    {
        var (verifier, challenge) = Pkce.Generate();
        var authorizeUrl = identityClient.GetAuthorizeUrl(codeChallenge: challenge);

        var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = authorizeUrl,
            CallbackUrl = options.RedirectUri!,
        }).ConfigureAwait(false);

        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("The authorization response did not contain a code.");

        _tokens = await identityClient.ExchangeCodeAsync(code, verifier).ConfigureAwait(false);
        await tokenStore.SaveTokensAsync(_tokens).ConfigureAwait(false);
        await EnsureSessionListenerStartedAsync().ConfigureAwait(false);
    }

    public async Task LogoutAsync()
    {
        await sessionEventClient.StopAsync().ConfigureAwait(false);
        _tokens = null;
        await tokenStore.ClearTokensAsync().ConfigureAwait(false);
    }

    /// <summary>Refreshes the access token if it's expired. Call before any authenticated API call.</summary>
    public async Task EnsureFreshTokenAsync()
    {
        _tokens ??= await tokenStore.GetTokensAsync().ConfigureAwait(false);
        if (_tokens is not { } tokens || !tokens.IsExpired() || tokens.RefreshToken is null)
            return;

        try
        {
            _tokens = await identityClient.RefreshTokenAsync(tokens.RefreshToken).ConfigureAwait(false);
            await tokenStore.SaveTokensAsync(_tokens).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            await LogoutAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens (or re-opens) the real-time channel that reports a force-logout the instant it
    /// happens, instead of only being discovered the next time a refresh fails. A no-op if
    /// already connected or if there's no signed-in user yet - safe to call from every page's
    /// OnAppearing alongside <see cref="EnsureFreshTokenAsync"/>.
    /// </summary>
    public async Task EnsureSessionListenerStartedAsync()
    {
        if (!_forceLogoutHandlerAttached)
        {
            sessionEventClient.ForceLogoutReceived += HandleForceLogoutAsync;
            _forceLogoutHandlerAttached = true;
        }

        if (sessionEventClient.IsConnected)
            return;

        _tokens ??= await tokenStore.GetTokensAsync().ConfigureAwait(false);
        if (_tokens is not { } tokens)
            return;

        var userId = JwtClaimsReader.GetSubject(tokens.AccessToken);
        if (userId is null)
            return;

        await sessionEventClient.StartAsync(options.Authority, userId, GetCurrentAccessTokenAsync).ConfigureAwait(false);
    }

    private async Task<string?> GetCurrentAccessTokenAsync()
    {
        await EnsureFreshTokenAsync().ConfigureAwait(false);
        return _tokens?.AccessToken;
    }

    private async Task HandleForceLogoutAsync()
    {
        _tokens = null;
        await tokenStore.ClearTokensAsync().ConfigureAwait(false);

        ForceLoggedOut?.Invoke(this, EventArgs.Empty);
    }
}
