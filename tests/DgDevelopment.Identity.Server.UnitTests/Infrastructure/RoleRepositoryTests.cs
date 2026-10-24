namespace DgDevelopment.Identity.Server.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class RoleRepositoryTests : IClassFixture<DatabaseFixture<RoleRepositoryTests>>
{
    private readonly DatabaseFixture<RoleRepositoryTests> _fixture;

    public RoleRepositoryTests(DatabaseFixture<RoleRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task AddAndGetByIdRoundTripsWithPermissions()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var platformId = await _fixture.GetSeededPlatformIdAsync();
        var name = UniqueName("role");
        var permission = new Permission(tenantId, platformId, $"perm-{Guid.NewGuid():N}", "desc", "User");
        var role = new Role(tenantId, platformId, name, "desc");
        role.AddPermission(permission);
        await using var context = _fixture.CreateContext();
        var repo = new RoleRepository(context);
        var permRepo = new PermissionRepository(context);
        await permRepo.AddAsync(permission);
        await repo.AddAsync(role);

        var stored = await repo.GetByIdAsync(role.Id);

        Assert.NotNull(stored);
        Assert.Equal(role.Id, stored.Id);
        Assert.Equal(name, stored.Name);
        var storedPermission = Assert.Single(stored.Permissions);
        Assert.Equal(permission.Id, storedPermission.PermissionId);
    }

    [Fact]
    public async Task GetByNameRoundTrips()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var platformId = await _fixture.GetSeededPlatformIdAsync();
        var name = UniqueName("role");
        var role = new Role(tenantId, platformId, name, "desc");
        await using var context = _fixture.CreateContext();
        var repo = new RoleRepository(context);
        await repo.AddAsync(role);

        var stored = await repo.GetByNameAsync(name);

        Assert.NotNull(stored);
        Assert.Equal(role.Id, stored.Id);
    }

    [Fact]
    public async Task GetByNameReturnsSeededSuperAdmin()
    {
        await using var context = _fixture.CreateContext();
        var repo = new RoleRepository(context);

        var stored = await repo.GetByNameAsync("SuperAdmin");

        Assert.NotNull(stored);
        Assert.NotEmpty(stored.Permissions);
    }

    [Fact]
    public async Task GetAllReturnsRolesOrderedByName()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var platformId = await _fixture.GetSeededPlatformIdAsync();
        await using var context = _fixture.CreateContext();
        var repo = new RoleRepository(context);
        await repo.AddAsync(new Role(tenantId, platformId, UniqueName("z-role"), "desc"));
        await repo.AddAsync(new Role(tenantId, platformId, UniqueName("a-role"), "desc"));

        var stored = await repo.GetAllAsync();

        Assert.Equal(stored.OrderBy(r => r.Name).Select(r => r.Name), stored.Select(r => r.Name));
        Assert.Contains(stored, r => r.Name == "SuperAdmin");
    }

    [Fact]
    public async Task UpdateAsyncPersistsAddedPermission()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var platformId = await _fixture.GetSeededPlatformIdAsync();
        var name = UniqueName("role");
        var role = new Role(tenantId, platformId, name, "desc");
        await using var context = _fixture.CreateContext();
        var repo = new RoleRepository(context);
        var permRepo = new PermissionRepository(context);
        await repo.AddAsync(role);
        var permission = new Permission(tenantId, platformId, $"perm-{Guid.NewGuid():N}", "desc", "User");
        await permRepo.AddAsync(permission);
        role.AddPermission(permission);
        await repo.UpdateAsync(role);

        var stored = await repo.GetByIdAsync(role.Id);

        Assert.NotNull(stored);
        Assert.Contains(stored.Permissions, p => p.PermissionId == permission.Id);
    }

    [Fact]
    public async Task DeleteAsyncRemovesRole()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var platformId = await _fixture.GetSeededPlatformIdAsync();
        var name = UniqueName("role");
        var role = new Role(tenantId, platformId, name, "desc");
        await using var context = _fixture.CreateContext();
        var repo = new RoleRepository(context);
        await repo.AddAsync(role);

        await repo.DeleteAsync(role.Id);

        Assert.Null(await repo.GetByNameAsync(name));
    }
}
