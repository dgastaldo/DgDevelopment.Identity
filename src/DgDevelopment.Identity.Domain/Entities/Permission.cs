namespace DgDevelopment.Identity.Domain.Entities;

public sealed class Permission
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string ResourceType { get; private set; }

    private Permission() { }

    public Permission(string name, string description, string resourceType)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        ResourceType = resourceType;
    }
}
