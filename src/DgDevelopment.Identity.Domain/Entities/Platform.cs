namespace DgDevelopment.Identity.Domain.Entities;

public enum PermissionMode
{
    AuthOnly,
    IdentityManaged
}

public sealed class Platform
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public PermissionMode PermissionMode { get; private set; }

    private Platform() { }

    public Platform(Guid tenantId, string name, string description, PermissionMode permissionMode)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = name;
        Description = description;
        PermissionMode = permissionMode;
    }

    public void Update(string name, string description, PermissionMode permissionMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Description = description;
        PermissionMode = permissionMode;
    }
}
