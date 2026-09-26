namespace DgDevelopment.Identity.Domain.Entities;

public sealed class DeviceCode
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string DeviceCodeHash { get; private set; }
    public string UserCodeHash { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Scopes { get; private set; }
    public bool IsAuthorized { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime? LastPolledAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private DeviceCode() { }

    public DeviceCode(string deviceCodeHash, string userCodeHash, Guid clientId, string[] scopes, int lifetimeSeconds = 900)
    {
        Id = Guid.NewGuid();
        DeviceCodeHash = deviceCodeHash;
        UserCodeHash = userCodeHash;
        ClientId = clientId;
        Scopes = string.Join(' ', scopes);
        IsAuthorized = false;
        IsUsed = false;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddSeconds(lifetimeSeconds);
    }

    public void Authorize(Guid tenantId, Guid userId)
    {
        TenantId = tenantId;
        UserId = userId;
        IsAuthorized = true;
    }

    public void MarkUsed() => IsUsed = true;

    public void RecordPoll(DateTime now) => LastPolledAt = now;

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;

    public string[] GetScopes() => Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
