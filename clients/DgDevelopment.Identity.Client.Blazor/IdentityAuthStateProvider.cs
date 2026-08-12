using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Client.Core;
using Microsoft.AspNetCore.Components.Authorization;

namespace DgDevelopment.Identity.Client.Blazor;

public sealed class IdentityAuthStateProvider(IdentityClient client, ITokenStore tokenStore
    //, OidcOptions options
    ) : AuthenticationStateProvider
{
    private TokenResponse? _tokens;
    private ClaimsPrincipal? _currentUser;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_currentUser?.Identity?.IsAuthenticated == true)
            return new AuthenticationState(_currentUser);

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
            catch
            {
                await tokenStore.ClearTokensAsync().ConfigureAwait(false);
                _tokens = null;
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }

        if (_tokens == null || _tokens.IsExpired())
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, _tokens.AccessToken) };
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "oidc"));

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        return new AuthenticationState(_currentUser);
    }

    public Uri GetLoginUrl()
    {
        return new Uri(client.GetAuthorizeUrl() + "&nonce=" + Guid.NewGuid().ToString("N"));
    }

    public async Task CompleteLoginAsync(string code, string codeVerifier)
    {
        _tokens = await client.ExchangeCodeAsync(code, codeVerifier).ConfigureAwait(false);
        await tokenStore.SaveTokensAsync(_tokens).ConfigureAwait(false);
        _currentUser = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LogoutAsync()
    {
        await tokenStore.ClearTokensAsync().ConfigureAwait(false);
        _tokens = null;
        _currentUser = null;
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
}
