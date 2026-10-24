namespace DgDevelopment.Identity.OAuth.Services;

public interface IClientIdCache
{
    bool IsValidClientId(string clientId);
    Task InitializeAsync(CancellationToken ct = default);
}
