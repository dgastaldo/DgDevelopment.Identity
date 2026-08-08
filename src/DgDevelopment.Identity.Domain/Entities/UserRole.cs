namespace DgDevelopment.Identity.Domain.Entities;

public sealed class UserRole
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public string? ScopeType { get; private set; }
    public string? ScopeValue { get; private set; }

    private UserRole() { }

    public UserRole(Guid userId, Guid roleId, string? scopeType = null, string? scopeValue = null)
    {
        UserId = userId;
        RoleId = roleId;
        ScopeType = scopeType;
        ScopeValue = scopeValue;
    }
}
