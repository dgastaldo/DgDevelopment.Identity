namespace DgDevelopment.Identity.Domain.Entities;

public sealed class GroupRole
{
    public Guid GroupId { get; private set; }
    public Guid RoleId { get; private set; }
    public string? ScopeType { get; private set; }
    public string? ScopeValue { get; private set; }

    private GroupRole() { }

    public GroupRole(Guid groupId, Guid roleId, string? scopeType = null, string? scopeValue = null)
    {
        GroupId = groupId;
        RoleId = roleId;
        ScopeType = scopeType;
        ScopeValue = scopeValue;
    }
}
