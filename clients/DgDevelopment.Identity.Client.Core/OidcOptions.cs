namespace DgDevelopment.Identity.Client.Core;

public sealed class OidcOptions
{
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string PostLogoutRedirectUri { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Scopes { get; set; } = ["openid", "profile", "email"];
}
