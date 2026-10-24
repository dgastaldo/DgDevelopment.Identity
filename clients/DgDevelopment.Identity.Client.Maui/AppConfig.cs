namespace DgDevelopment.Identity.Client.Maui;

using DgDevelopment.Identity.Client.Core;

/// <summary>
/// Matches the Public (PKCE-only) client <c>DbSeeder</c> provisions on the bootstrap tenant -
/// see <c>DbSeeder.SeedMauiClientAsync</c>. <see cref="Authority"/> is the one value you'll need
/// to change per environment: Aspire assigns a random HTTPS port to the Server project on every
/// run - check the AppHost console output (or run it once and read the port it reports) and
/// update this constant to match.
/// </summary>
internal static class AppConfig
{
    public const string Authority = "https://localhost:7157";
    public const string ClientId = "8f2b1a4c-6d3e-4a7b-9c1d-2e5f6a7b8c9d";
    public const string RedirectUri = "dgidentityapp://callback";

    public static OidcOptions CreateOidcOptions() => new()
    {
        Authority = Authority,
        ClientId = ClientId,
        RedirectUri = new Uri(RedirectUri),
        PostLogoutRedirectUri = new Uri(RedirectUri),
        Scopes = ["openid", "profile", "email"],
    };
}
