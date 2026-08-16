using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DgDevelopment.Identity.Client.Core;

public sealed class IdentityClient(HttpClient http, OidcOptions options)
{
    public Uri GetAuthorizeUrl(string? state = null, string? codeChallenge = null)
    {
        var url = $"{options.Authority}/connect/authorize" +
                  $"?client_id={Uri.EscapeDataString(options.ClientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(options.RedirectUri!.ToString())}" +
                  $"&response_type=code" +
                  $"&scope={Uri.EscapeDataString(string.Join(' ', options.Scopes))}";

        if (state != null)
            url += $"&state={Uri.EscapeDataString(state)}";

        if (codeChallenge != null)
            url += $"&code_challenge={Uri.EscapeDataString(codeChallenge)}&code_challenge_method=S256";

        return new Uri(url);
    }

    public Uri GetLogoutUrl(string? idTokenHint = null)
    {
        var url = $"{options.Authority}/connect/endsession" +
                  $"?post_logout_redirect_uri={Uri.EscapeDataString(options.PostLogoutRedirectUri!.ToString())}";

        if (idTokenHint != null)
            url += $"&id_token_hint={Uri.EscapeDataString(idTokenHint)}";

        return new Uri(url);
    }

    public async Task<TokenResponse> ExchangeCodeAsync(string code, string codeVerifier, Uri? redirectUri = null, CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = (redirectUri ?? options.RedirectUri!).ToString(),
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["code_verifier"] = codeVerifier
        });

        var response = await http.PostAsync(new Uri($"{options.Authority}/connect/token"), content, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync(TokenResponseJsonContext.Default.TokenResponse, ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshTokenValue, CancellationToken ct = default)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshTokenValue,
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret
        });

        var response = await http.PostAsync(new Uri($"{options.Authority}/connect/token"), content, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync(TokenResponseJsonContext.Default.TokenResponse, ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<UserInfo?> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{options.Authority}/connect/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync(UserInfoJsonContext.Default.UserInfo, ct).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        throw new HttpRequestException($"Request to '{response.RequestMessage?.RequestUri}' failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }
}

[JsonSerializable(typeof(TokenResponse))]
internal sealed partial class TokenResponseJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(UserInfo))]
internal sealed partial class UserInfoJsonContext : JsonSerializerContext;
