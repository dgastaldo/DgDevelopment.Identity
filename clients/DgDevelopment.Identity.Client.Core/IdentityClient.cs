using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DgDevelopment.Identity.Client.Core;

public sealed class IdentityClient
{
    private readonly HttpClient _http;
    private readonly OidcOptions _options;

    public IdentityClient(HttpClient http, OidcOptions options)
    {
        _http = http;
        _options = options;
    }

    public string GetAuthorizeUrl(string? state = null, string? codeChallenge = null)
    {
        var url = $"{_options.Authority}/connect/authorize" +
                  $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
                  $"&response_type=code" +
                  $"&scope={Uri.EscapeDataString(string.Join(' ', _options.Scopes))}";

        if (state != null)
            url += $"&state={Uri.EscapeDataString(state)}";

        if (codeChallenge != null)
            url += $"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256";

        return url;
    }

    public string GetLogoutUrl(string? idTokenHint = null)
    {
        var url = $"{_options.Authority}/connect/endsession" +
                  $"?post_logout_redirect_uri={Uri.EscapeDataString(_options.PostLogoutRedirectUri)}";

        if (idTokenHint != null)
            url += $"&id_token_hint={Uri.EscapeDataString(idTokenHint)}";

        return url;
    }

    public async Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code_verifier"] = codeVerifier
        });

        var response = await _http.PostAsync($"{_options.Authority}/connect/token", content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(TokenResponseJsonContext.Default.TokenResponse, ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshTokenValue, CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshTokenValue,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        });

        var response = await _http.PostAsync($"{_options.Authority}/connect/token", content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(TokenResponseJsonContext.Default.TokenResponse, ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<UserInfo?> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.Authority}/connect/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(UserInfoJsonContext.Default.UserInfo, ct).ConfigureAwait(false);
    }
}

[JsonSerializable(typeof(TokenResponse))]
internal sealed partial class TokenResponseJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(UserInfo))]
internal sealed partial class UserInfoJsonContext : JsonSerializerContext;
