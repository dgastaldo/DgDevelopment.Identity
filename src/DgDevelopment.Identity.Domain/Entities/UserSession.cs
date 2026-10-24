namespace DgDevelopment.Identity.Domain.Entities;

public sealed class UserSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string SessionId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public string AuthMethods { get; private set; }

    private UserSession() { }

    public UserSession(Guid userId, string sessionId, DateTime expiresAt, string[] authMethods)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        SessionId = sessionId;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
        IsRevoked = false;
        AuthMethods = System.Text.Json.JsonSerializer.Serialize(authMethods);
    }

    public void Revoke() => IsRevoked = true;

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;

    public string[] GetAuthMethods() =>
        System.Text.Json.JsonSerializer.Deserialize<string[]>(AuthMethods) ?? [];
}
