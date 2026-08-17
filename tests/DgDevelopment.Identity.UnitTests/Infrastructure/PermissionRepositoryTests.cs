namespace DgDevelopment.Identity.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.UnitTests.Testing;
using Xunit;

public sealed class PermissionRepositoryTests : IClassFixture<DatabaseFixture<PermissionRepositoryTests>>
{
    private readonly DatabaseFixture<PermissionRepositoryTests> _fixture;

    public PermissionRepositoryTests(DatabaseFixture<PermissionRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static string UniqueName() => $"perm-{Guid.NewGuid():N}";

    [Fact]
    public async Task AddAndGetByIdRoundTrips()
    {
        var name = UniqueName();
        var permission = new Permission(name, "desc", "User");
        await using var context = _fixture.CreateContext();
        var repo = new PermissionRepository(context);
        await repo.AddAsync(permission);

        var stored = await repo.GetByIdAsync(permission.Id);

        Assert.NotNull(stored);
        Assert.Equal(permission.Id, stored.Id);
        Assert.Equal(name, stored.Name);
        Assert.Equal("desc", stored.Description);
        Assert.Equal("User", stored.ResourceType);
    }

    [Fact]
    public async Task GetByNameRoundTrips()
    {
        var name = UniqueName();
        var permission = new Permission(name, "desc", "User");
        await using var context = _fixture.CreateContext();
        var repo = new PermissionRepository(context);
        await repo.AddAsync(permission);

        var stored = await repo.GetByNameAsync(name);

        Assert.NotNull(stored);
        Assert.Equal(permission.Id, stored.Id);
    }

    [Fact]
    public async Task GetByNameReturnsSeededPermission()
    {
        await using var context = _fixture.CreateContext();
        var repo = new PermissionRepository(context);

        var stored = await repo.GetByNameAsync("user:read");

        Assert.NotNull(stored);
        Assert.Equal("user:read", stored.Name);
    }

    [Fact]
    public async Task GetAllReturnsPermissionsOrderedByName()
    {
        await using var context = _fixture.CreateContext();
        var repo = new PermissionRepository(context);

        var stored = await repo.GetAllAsync();

        Assert.Equal(stored.OrderBy(p => p.Name).Select(p => p.Name), stored.Select(p => p.Name));
        Assert.NotEmpty(stored);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotRemoveExistingData()
    {
        var name = UniqueName();
        var permission = new Permission(name, "desc", "User");
        await using var context = _fixture.CreateContext();
        var repo = new PermissionRepository(context);
        await repo.AddAsync(permission);

        await repo.UpdateAsync(permission);

        var stored = await repo.GetByNameAsync(name);
        Assert.NotNull(stored);
        Assert.Equal(permission.Id, stored.Id);
    }

    [Fact]
    public async Task DeleteAsyncRemovesPermission()
    {
        var name = UniqueName();
        var permission = new Permission(name, "desc", "User");
        await using var context = _fixture.CreateContext();
        var repo = new PermissionRepository(context);
        await repo.AddAsync(permission);

        await repo.DeleteAsync(permission.Id);

        Assert.Null(await repo.GetByNameAsync(name));
    }
}