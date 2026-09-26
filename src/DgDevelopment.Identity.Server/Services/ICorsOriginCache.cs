namespace DgDevelopment.Identity.Server.Services;

public interface ICorsOriginCache
{
    bool IsAllowedOrigin(string origin);

    // Exact-URI check (not just origin) over the same registered redirect/post-logout-redirect
    // URIs the CORS origin set is built from - used to validate OIDC end_session's
    // post_logout_redirect_uri against actually-registered client URIs instead of blindly
    // redirecting wherever the query string points.
    bool IsAllowedRedirectUri(string uri);

    Task InitializeAsync(CancellationToken ct = default);
}
