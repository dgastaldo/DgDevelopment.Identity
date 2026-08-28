namespace DgDevelopment.Identity.Domain.Entities;

public enum VerificationTokenPurpose
{
    EmailVerification,
    PasswordReset
}

public sealed class VerificationToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public VerificationTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private VerificationToken() { }

    public VerificationToken(Guid userId, VerificationTokenPurpose purpose, string tokenHash, int lifetimeMinutes = 60)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Purpose = purpose;
        TokenHash = tokenHash;
        IsUsed = false;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddMinutes(lifetimeMinutes);
    }

    public void MarkUsed() => IsUsed = true;

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
}
