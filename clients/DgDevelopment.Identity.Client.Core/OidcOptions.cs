namespace DgDevelopment.Identity.Client.Core;

public sealed class OidcOptions
{
    public string Authority { get; set; } = string.Empty;

    // Where to send the browser for /connect/authorize and /connect/endsession. Falls back to
    // Authority when unset - correct whenever the browser can reach Authority directly (local dev,
    // Aspire, MAUI). Only needs setting when Authority is a server-to-server address the browser
    // can't resolve, e.g. a Docker-internal hostname like http://identity-server:8080 - there,
    // PublicAuthority must be the identity server's LAN/public base URL instead.
    public string? PublicAuthority { get; set; }

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public Uri? RedirectUri { get; set; }
    public Uri? PostLogoutRedirectUri { get; set; }
    public IReadOnlyCollection<string> Scopes { get; set; } = ["openid", "profile", "email"];
}
