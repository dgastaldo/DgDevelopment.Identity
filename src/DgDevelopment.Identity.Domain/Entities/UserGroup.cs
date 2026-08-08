namespace DgDevelopment.Identity.Domain.Entities;

public sealed class UserGroup
{
    public Guid UserId { get; private set; }
    public Guid GroupId { get; private set; }

    private UserGroup() { }

    public UserGroup(Guid userId, Guid groupId)
    {
        UserId = userId;
        GroupId = groupId;
    }
}
