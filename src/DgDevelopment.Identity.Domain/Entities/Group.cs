namespace DgDevelopment.Identity.Domain.Entities;

public sealed class Group
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public Guid? ParentGroupId { get; private set; }

    private readonly List<GroupRole> _roles = [];
    public IReadOnlyCollection<GroupRole> Roles => _roles.AsReadOnly();

    private Group() { }

    public Group(string name, string description, Guid? parentGroupId = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        ParentGroupId = parentGroupId;
    }

    public void AddRole(Role role, string? scopeType = null, string? scopeValue = null)
    {
        if (_roles.Any(r => r.RoleId == role.Id))
            return;

        _roles.Add(new GroupRole(Id, role.Id, scopeType, scopeValue));
    }

    public void RemoveRole(Guid roleId)
    {
        _roles.RemoveAll(r => r.RoleId == roleId);
    }

    public void SetParent(Guid? parentGroupId) => ParentGroupId = parentGroupId;
}
