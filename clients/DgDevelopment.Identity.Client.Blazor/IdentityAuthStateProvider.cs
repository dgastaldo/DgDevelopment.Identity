using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DgDevelopment.Identity.Client.Core;
using Microsoft.AspNetCore.Components.Authorization;

namespace DgDevelopment.Identity.Client.Blazor;

public class IdentityAuthStateProvider(IdentityClient client, ITokenStore tokenStore, ISessionMarkerService markerService) : AuthenticationStateProvider
{
    private TokenResponse? _tokens;
    private ClaimsPrincipal? _currentUser;
    private UserInfo? _userInfo;

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

        _currentUser = BuildPrincipal(_userInfo!);

        await markerService.SetAsync(_userInfo!).ConfigureAwait(false);

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

    public Uri GetLoginUrl(string? state = null, string? codeChallenge = null)
    {
        return new Uri(client.GetAuthorizeUrl(state, codeChallenge) + "&nonce=" + Guid.NewGuid().ToString("N"));
    }

    public async Task CompleteLoginAsync(string code, string codeVerifier, Uri? redirectUri = null)
    {
        _tokens = await client.ExchangeCodeAsync(code, codeVerifier, redirectUri).ConfigureAwait(false);
        await tokenStore.SaveTokensAsync(_tokens).ConfigureAwait(false);
        _currentUser = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LogoutAsync()
    {
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

    protected static ClaimsPrincipal BuildPrincipal(UserInfo? userInfo)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrEmpty(userInfo?.Sub))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userInfo.Sub));
        if (!string.IsNullOrEmpty(userInfo?.Name))
            claims.Add(new Claim(ClaimTypes.Name, userInfo.Name));
        if (!string.IsNullOrEmpty(userInfo?.Email))
            claims.Add(new Claim(ClaimTypes.Email, userInfo.Email));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "oidc"));
    }
}