namespace DgDevelopment.Identity.Domain.Entities;

public sealed class SigningKey
{
    public string Id { get; private set; }
    public string Algorithm { get; private set; }
    public string KeyData { get; private set; }
    public string PublicKeyData { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private SigningKey() { }

    public SigningKey(string id, string algorithm, string keyData, string publicKeyData, DateTime expiresAt)
    {
        Id = id;
        Algorithm = algorithm;
        KeyData = keyData;
        PublicKeyData = publicKeyData;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    public void Deactivate() => IsActive = false;

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
}
