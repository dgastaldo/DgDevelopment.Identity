namespace DgDevelopment.Identity.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;
using Xunit;

public sealed class GroupTests
{
    [Fact]
    public void ConstructorSetsFields()
    {
        var group = new Group("Team", "Team group");

        Assert.False(string.IsNullOrWhiteSpace(group.Id.ToString()));
        Assert.Equal("Team", group.Name);
        Assert.Equal("Team group", group.Description);
        Assert.Null(group.ParentGroupId);
        Assert.Empty(group.Roles);
    }

    [Fact]
    public void ConstructorSetsParentGroup()
    {
        var parentId = Guid.NewGuid();
        var group = new Group("Child", "desc", parentId);

        Assert.Equal(parentId, group.ParentGroupId);
    }

    [Fact]
    public void AddRoleAddsOnceAndRemoveRoleClears()
    {
        var group = new Group("Team", "desc");
        var role = new Role("Admin", "desc");

        group.AddRole(role);
        group.AddRole(role);

        Assert.Single(group.Roles);
        Assert.Equal(role.Id, Assert.Single(group.Roles).RoleId);

        group.RemoveRole(role.Id);

        Assert.Empty(group.Roles);
    }

    [Fact]
    public void SetParentUpdatesParentGroupId()
    {
        var group = new Group("Team", "desc");
        var parentId = Guid.NewGuid();

        group.SetParent(parentId);

        Assert.Equal(parentId, group.ParentGroupId);

        group.SetParent(null);

        Assert.Null(group.ParentGroupId);
    }
}