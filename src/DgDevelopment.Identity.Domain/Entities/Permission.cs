namespace DgDevelopment.Identity.Domain.Entities;

public sealed class Permission
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PlatformId { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string ResourceType { get; private set; }
    public bool IsGlobal { get; private set; }

    private Permission() { }

    public Permission(Guid tenantId, Guid platformId, string name, string description, string resourceType, bool isGlobal = false)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        PlatformId = platformId;
        Name = name;
        Description = description;
        ResourceType = resourceType;
        IsGlobal = isGlobal;
    }
}
