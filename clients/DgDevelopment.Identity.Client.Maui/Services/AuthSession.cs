namespace DgDevelopment.Identity.Client.Maui.Services;

using DgDevelopment.Identity.Client.Core;
using Microsoft.Maui.Authentication;

/// <summary>
/// Drives the native OAuth login (via <see cref="WebAuthenticator"/> + PKCE, no client secret -
/// the server-seeded MAUI client is <c>ClientType.Public</c>) and holds the current session's
/// tokens. Pages call <see cref="EnsureFreshTokenAsync"/> before making an authenticated API call;
/// <see cref="IdentityAuthHandler"/> handles attaching whatever token is currently stored to every
/// request and clearing it on a 401.
/// </summary>
public sealed class AuthSession(IdentityClient identityClient, ITokenStore tokenStore, OidcOptions options)
{
    private TokenResponse? _tokens;

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
    }

    public async Task LogoutAsync()
    {
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
}
