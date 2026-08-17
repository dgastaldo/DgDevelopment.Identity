namespace DgDevelopment.Identity.Server.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class GroupRepositoryTests : IClassFixture<DatabaseFixture<GroupRepositoryTests>>
{
    private readonly DatabaseFixture<GroupRepositoryTests> _fixture;

    public GroupRepositoryTests(DatabaseFixture<GroupRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task AddAndGetByIdRoundTripsWithRoles()
    {
        var name = UniqueName("group");
        var role = new Role($"role-{Guid.NewGuid():N}", "desc");
        var group = new Group(name, "desc");
        group.AddRole(role);
        await using var context = _fixture.CreateContext();
        var repo = new GroupRepository(context);
        var roleRepo = new RoleRepository(context);
        await roleRepo.AddAsync(role);
        await repo.AddAsync(group);

        var stored = await repo.GetByIdAsync(group.Id);

        Assert.NotNull(stored);
        Assert.Equal(group.Id, stored.Id);
        Assert.Equal(name, stored.Name);
        Assert.Equal(role.Id, Assert.Single(stored.Roles).RoleId);
    }

    [Fact]
    public async Task GetAllReturnsGroupsOrderedByName()
    {
        await using var context = _fixture.CreateContext();
        var repo = new GroupRepository(context);
        await repo.AddAsync(new Group(UniqueName("z-group"), "desc"));
        await repo.AddAsync(new Group(UniqueName("a-group"), "desc"));

        var stored = await repo.GetAllAsync();

        Assert.Equal(stored.OrderBy(g => g.Name).Select(g => g.Name), stored.Select(g => g.Name));
        Assert.Contains(stored, g => g.Name == "SuperAdmins");
    }

    [Fact]
    public async Task GetChildrenReturnsDirectChildrenOnly()
    {
        await using var context = _fixture.CreateContext();
        var repo = new GroupRepository(context);
        var parent = new Group(UniqueName("parent"), "desc");
        await repo.AddAsync(parent);
        var child = new Group(UniqueName("child"), "desc", parent.Id);
        var grandchild = new Group(UniqueName("grandchild"), "desc", child.Id);
        await repo.AddAsync(child);
        await repo.AddAsync(grandchild);

        var children = await repo.GetChildrenAsync(parent.Id);

        Assert.Equal(child.Id, Assert.Single(children).Id);
    }

    [Fact]
    public async Task GetChildrenReturnsEmptyForLeafGroup()
    {
        await using var context = _fixture.CreateContext();
        var repo = new GroupRepository(context);
        var leaf = new Group(UniqueName("leaf"), "desc");
        await repo.AddAsync(leaf);

        var children = await repo.GetChildrenAsync(leaf.Id);

        Assert.Empty(children);
    }

    [Fact]
    public async Task UpdateAsyncPersistsParentChange()
    {
        await using var context = _fixture.CreateContext();
        var repo = new GroupRepository(context);
        var parent = new Group(UniqueName("parent"), "desc");
        var child = new Group(UniqueName("child"), "desc");
        await repo.AddAsync(parent);
        await repo.AddAsync(child);

        child.SetParent(parent.Id);
        await repo.UpdateAsync(child);

        var stored = await repo.GetByIdAsync(child.Id);
        Assert.NotNull(stored);
        Assert.Equal(parent.Id, stored.ParentGroupId);
    }

    [Fact]
    public async Task DeleteAsyncRemovesGroup()
    {
        var name = UniqueName("group");
        var group = new Group(name, "desc");
        await using var context = _fixture.CreateContext();
        var repo = new GroupRepository(context);
        await repo.AddAsync(group);

        await repo.DeleteAsync(group.Id);

        var stored = await repo.GetAllAsync();
        Assert.DoesNotContain(stored, g => g.Id == group.Id);
    }
}