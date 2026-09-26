namespace DgDevelopment.Identity.Domain.Entities;

public sealed class UserPermission
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PermissionId { get; private set; }
    public string? ScopeType { get; private set; }
    public string? ScopeValue { get; private set; }

    private UserPermission() { }

    public UserPermission(Guid tenantId, Guid userId, Guid permissionId, string? scopeType = null, string? scopeValue = null)
    {
        TenantId = tenantId;
        UserId = userId;
        PermissionId = permissionId;
        ScopeType = scopeType;
        ScopeValue = scopeValue;
    }
}
