namespace DgDevelopment.Identity.Domain.Entities;

public sealed class RolePermission
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public string? ScopeType { get; private set; }
    public string? ScopeValue { get; private set; }

    private RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId, string? scopeType = null, string? scopeValue = null)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        ScopeType = scopeType;
        ScopeValue = scopeValue;
    }
}
