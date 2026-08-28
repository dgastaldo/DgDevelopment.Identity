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

    // The specific email address this token verifies (normalized/uppercased, matching
    // UserEmails.Email) - needed because a user can have more than one email once self-service
    // email management exists, so "verify the primary email" is no longer a safe assumption.
    // A plain string rather than a UserEmail.Id: UserEmail is an EF Core owned type with no
    // clean FK relationship to model from here, and User.VerifyEmail already resolves by value.
    public string TargetEmail { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private VerificationToken() { }

    public VerificationToken(Guid userId, VerificationTokenPurpose purpose, string tokenHash, string targetEmail, int lifetimeMinutes = 60)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Purpose = purpose;
        TokenHash = tokenHash;
        TargetEmail = targetEmail;
        IsUsed = false;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddMinutes(lifetimeMinutes);
    }

    public void MarkUsed() => IsUsed = true;

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
}
