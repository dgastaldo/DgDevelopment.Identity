namespace DgDevelopment.Identity.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }

    private readonly List<RolePermission> _permissions = [];
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    private Role() { }

    public Role(Guid tenantId, string name, string description)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        Description = description;
    }

    public void Rename(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Description = description;
    }

    public void AddPermission(Permission permission, string? scopeType = null, string? scopeValue = null)
    {
        ArgumentNullException.ThrowIfNull(permission);
        if (_permissions.Any(p => p.PermissionId == permission.Id))
            return;

        _permissions.Add(new RolePermission(Id, permission.Id, scopeType, scopeValue));
    }

    public void RemovePermission(Guid permissionId)
    {
        _permissions.RemoveAll(p => p.PermissionId == permissionId);
    }
}
