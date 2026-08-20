namespace DgDevelopment.Identity.Domain.Entities;

public enum MfaChallengeStatus
{
    Pending,
    Approved,
    Denied,
    Expired
}

public sealed class MfaChallenge
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Provider { get; private set; }
    public string ChallengeCodeHash { get; private set; }
    public MfaChallengeStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private MfaChallenge() { }

    public MfaChallenge(Guid userId, string provider, string challengeCodeHash, int lifetimeSeconds = 300)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(challengeCodeHash);
        Id = Guid.NewGuid();
        UserId = userId;
        Provider = provider;
        ChallengeCodeHash = challengeCodeHash;
        Status = MfaChallengeStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddSeconds(lifetimeSeconds);
    }

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;

    public bool Resolve(MfaChallengeStatus status)
    {
        if (Status != MfaChallengeStatus.Pending || IsExpired())
            return false;

        Status = status;
        ResolvedAt = DateTime.UtcNow;
        return true;
    }
}