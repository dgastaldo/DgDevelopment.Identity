namespace DgDevelopment.Identity.Domain.Entities;

public sealed class PasswordHistoryEntry
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PasswordHistoryEntry() { }

    public PasswordHistoryEntry(Guid userId, string passwordHash)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        PasswordHash = passwordHash;
        CreatedAt = DateTime.UtcNow;
    }
}
