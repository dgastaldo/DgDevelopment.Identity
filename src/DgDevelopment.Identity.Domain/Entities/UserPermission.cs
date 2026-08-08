namespace DgDevelopment.Identity.Domain.Entities;

public sealed class UserPermission
{
    public Guid UserId { get; private set; }
    public Guid PermissionId { get; private set; }
    public string? ScopeType { get; private set; }
    public string? ScopeValue { get; private set; }

    private UserPermission() { }

    public UserPermission(Guid userId, Guid permissionId, string? scopeType = null, string? scopeValue = null)
    {
        UserId = userId;
        PermissionId = permissionId;
        ScopeType = scopeType;
        ScopeValue = scopeValue;
    }
}
