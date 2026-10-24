using DgDevelopment.Identity.Domain.ValueObjects;

namespace DgDevelopment.Identity.Domain.Entities;

public sealed class UserEmail
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public EmailAddress Email { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsVerified { get; private set; }
    public DateTime? VerifiedAt { get; private set; }

    private UserEmail() { }

    public UserEmail(Guid userId, EmailAddress email, bool isPrimary = false)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Email = email;
        IsPrimary = isPrimary;
        IsVerified = false;
    }

    public void Verify()
    {
        IsVerified = true;
        VerifiedAt = DateTime.UtcNow;
    }

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}
