namespace DgDevelopment.Identity.Domain.Entities;

public sealed class UserConsent
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ClientId { get; private set; }
    public string GrantedScopes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    private UserConsent() { }

    public UserConsent(Guid userId, Guid clientId, string[] scopes, DateTime? expiresAt = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        ClientId = clientId;
        GrantedScopes = string.Join(' ', scopes);
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired() => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;

    public string[] GetScopes() => GrantedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
