namespace DgDevelopment.Identity.IntegrationTests;

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

/// <summary>
/// Drives a real login -&gt; password -&gt; consent -&gt; authorization_code exchange over HTTP,
/// exactly as a browser-based OIDC client would, so tests exercise the real pipeline
/// (cookies, antiforgery, CORS, JWT claim mapping) instead of minting a token out-of-band.
/// </summary>
public sealed partial class OidcTestClient(HttpClient client)
{
    public async Task<string> LoginAndGetAccessTokenAsync(
        string username, string password, string clientId, string clientSecret, string redirectUri, CancellationToken ct = default)
    {
        var (verifier, challenge) = GeneratePkce();
        var authorizeUrl = "/connect/authorize"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString("openid profile email")}"
            + $"&code_challenge={Uri.EscapeDataString(challenge)}"
            + "&code_challenge_method=S256";

        var loginRedirect = await client.GetAsync(authorizeUrl, ct).ConfigureAwait(false);
        var loginUrl = RequireLocation(loginRedirect);

        var loginPage = await client.GetAsync(loginUrl, ct).ConfigureAwait(false);
        var loginToken = await ExtractAntiforgeryAsync(loginPage, ct).ConfigureAwait(false);

        var loginPost = await client.PostAsync(loginUrl, FormContent(new()
        {
            ["Identifier"] = username,
            ["__RequestVerificationToken"] = loginToken,
        }), ct).ConfigureAwait(false);
        var passwordUrl = RequireLocation(loginPost);

        var passwordPage = await client.GetAsync(passwordUrl, ct).ConfigureAwait(false);
        var passwordBody = await passwordPage.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var passwordToken = ExtractAntiforgery(passwordBody);
        var hiddenUsername = ExtractHidden(passwordBody, "Username") ?? username;
        var hiddenReturnUrl = ExtractHidden(passwordBody, "returnUrl");

        var passwordFields = new Dictionary<string, string>
        {
            ["Username"] = hiddenUsername,
            ["Password"] = password,
            ["__RequestVerificationToken"] = passwordToken,
        };
        if (hiddenReturnUrl is not null)
            passwordFields["returnUrl"] = hiddenReturnUrl;

        var passwordPost = await client.PostAsync(passwordUrl, FormContent(passwordFields), ct).ConfigureAwait(false);
        var afterPassword = RequireLocation(passwordPost);

        var authorizeAgain = await client.GetAsync(afterPassword, ct).ConfigureAwait(false);
        var next = RequireLocation(authorizeAgain);

        string code;
        if (next.ToString().Contains("/account/consent", StringComparison.OrdinalIgnoreCase))
        {
            var consentPage = await client.GetAsync(next, ct).ConfigureAwait(false);
            var consentBody = await consentPage.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var consentToken = ExtractAntiforgery(consentBody);
            var consentReturnUrl = ExtractHidden(consentBody, "returnUrl");

            var consentFields = new Dictionary<string, string>
            {
                ["action"] = "approve",
                ["__RequestVerificationToken"] = consentToken,
            };
            if (consentReturnUrl is not null)
                consentFields["returnUrl"] = consentReturnUrl;

            // Consent approval redirects back to returnUrl (/connect/authorize?...), not straight to
            // the client's redirect_uri - authorize has to be replayed now that consent is recorded.
            var consentPost = await client.PostAsync(next, FormContent(consentFields), ct).ConfigureAwait(false);
            var afterConsent = RequireLocation(consentPost);
            var authorizeAfterConsent = await client.GetAsync(afterConsent, ct).ConfigureAwait(false);
            var codeRedirect = RequireLocation(authorizeAfterConsent);
            code = ExtractQueryParam(codeRedirect, "code");
        }
        else
        {
            code = ExtractQueryParam(next, "code");
        }

        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code_verifier"] = verifier,
        }), ct).ConfigureAwait(false);

        var body = await tokenResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!tokenResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Token exchange failed with {(int)tokenResponse.StatusCode}: {body}");

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Token response had no access_token.");
    }

    private static Uri RequireLocation(HttpResponseMessage response)
        => response.Headers.Location
            ?? throw new InvalidOperationException($"Expected a redirect from {response.RequestMessage?.RequestUri}, got {(int)response.StatusCode}.");

    private static async Task<string> ExtractAntiforgeryAsync(HttpResponseMessage response, CancellationToken ct)
        => ExtractAntiforgery(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));

    private static string ExtractAntiforgery(string html)
    {
        var match = AntiforgeryRegex().Match(html);
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException("No __RequestVerificationToken found in the page.");
    }

    private static string? ExtractHidden(string html, string fieldName)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(fieldName)}\"[^>]*value=\"([^\"]*)\"");
        return match.Success ? HttpUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    private static string ExtractQueryParam(Uri uri, string name)
    {
        var query = HttpUtility.ParseQueryString(uri.Query);
        return query[name] ?? throw new InvalidOperationException($"Query parameter '{name}' not found in {uri}.");
    }

    private static FormUrlEncodedContent FormContent(Dictionary<string, string> fields) => new(fields);

    private static (string Verifier, string Challenge) GeneratePkce()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Base64Url(bytes);
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryRegex();
}
