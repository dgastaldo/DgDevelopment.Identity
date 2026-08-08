namespace DgDevelopment.Identity.Domain.Entities;

public enum PermissionMode
{
    AuthOnly,
    IdentityManaged
}

public sealed class Platform
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public PermissionMode PermissionMode { get; private set; }

    private Platform() { }

    public Platform(string name, string description, PermissionMode permissionMode)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        PermissionMode = permissionMode;
    }
}
