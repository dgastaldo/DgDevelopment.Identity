namespace DgDevelopment.Identity.Server.Services;

public interface ICorsOriginCache
{
    bool IsAllowedOrigin(string origin);
    Task InitializeAsync(CancellationToken ct = default);
}
