namespace DgDevelopment.Identity.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string TokenHash { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SessionId { get; private set; }
    public string Scopes { get; private set; }
    public Guid? PreviousTokenId { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private RefreshToken() { }

    public RefreshToken(Guid tenantId, string tokenHash, Guid clientId, Guid userId, Guid sessionId, string[] scopes,
        Guid? previousTokenId = null, int lifetimeDays = 30)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        TokenHash = tokenHash;
        ClientId = clientId;
        UserId = userId;
        SessionId = sessionId;
        Scopes = string.Join(' ', scopes);
        PreviousTokenId = previousTokenId;
        IsRevoked = false;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddDays(lifetimeDays);
    }

    public void Revoke() => IsRevoked = true;

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;

    public string[] GetScopes() => Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
