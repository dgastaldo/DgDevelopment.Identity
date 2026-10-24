namespace DgDevelopment.Identity.Domain.Entities;

public enum TenantMembershipStatus
{
    Active,
    Invited,
    Removed
}

public sealed class TenantMembership
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public TenantMembershipStatus Status { get; private set; }
    public bool IsOwner { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RemovedAt { get; private set; }

    private TenantMembership() { }

    public TenantMembership(Guid tenantId, Guid userId, bool isOwner = false, TenantMembershipStatus status = TenantMembershipStatus.Active)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        IsOwner = isOwner;
        Status = status;
        CreatedAt = DateTime.UtcNow;
    }

    public void Remove()
    {
        Status = TenantMembershipStatus.Removed;
        RemovedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        Status = TenantMembershipStatus.Active;
        RemovedAt = null;
    }
}
