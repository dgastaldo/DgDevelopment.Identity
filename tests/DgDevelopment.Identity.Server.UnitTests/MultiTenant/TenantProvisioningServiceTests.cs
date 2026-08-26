namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using DgDevelopment.Identity.Application.Tenants;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class TenantProvisioningServiceTests : IClassFixture<DatabaseFixture<TenantProvisioningServiceTests>>
{
    private readonly DatabaseFixture<TenantProvisioningServiceTests> _fixture;

    public TenantProvisioningServiceTests(DatabaseFixture<TenantProvisioningServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static TenantProvisioningService CreateService(IdentityDbContext context)
        => new(
            new TenantRepository(context), new PlatformRepository(context), new PermissionRepository(context),
            new RoleRepository(context), new GroupRepository(context));

    [Fact]
    public async Task ProvisionAsyncCreatesTenantPlatformCatalogRoleAndGroupAtomically()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var slug = Unique("provision");

        var result = await service.ProvisionAsync("Provisioned Tenant", slug);

        Assert.Equal("Provisioned Tenant", result.Tenant.Name);
        Assert.Equal(slug, result.Tenant.Slug);
        Assert.False(result.Tenant.IsPlatformTenant);
        Assert.Equal("IdentityAdmin", result.Platform.Name);
        Assert.Equal(result.Tenant.Id, result.Platform.TenantId);
        Assert.NotEmpty(result.AdminRole.Permissions);
        Assert.Equal(result.AdminRole.Permissions.Count,
            (await new PermissionRepository(context).GetAllAsync())
                .Count(p => p.TenantId == result.Tenant.Id));
        Assert.Contains(result.AdminsGroup.Roles, r => r.RoleId == result.AdminRole.Id);
    }

    [Fact]
    public async Task ProvisionAsyncMarksOnlyThePlatformTenantWhenRequested()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var platformResult = await service.ProvisionAsync("Platform Owner", Unique("platform-owner"), isPlatformTenant: true);
        var customerResult = await service.ProvisionAsync("Customer", Unique("customer"));

        Assert.True(platformResult.Tenant.IsPlatformTenant);
        Assert.False(customerResult.Tenant.IsPlatformTenant);
    }

    // SuperAdmin is a special concept that belongs only to the tenant that owns the platform -
    // every other tenant's own administrator is just "Admin", even though the role holds the
    // same full permission set within its own tenant-scoped catalog.
    [Fact]
    public async Task ProvisionAsyncNamesTheRoleAndGroupSuperAdminOnlyForThePlatformTenant()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var platformResult = await service.ProvisionAsync("Platform Owner", Unique("platform-owner-naming"), isPlatformTenant: true);
        var customerResult = await service.ProvisionAsync("Customer", Unique("customer-naming"));

        Assert.Equal("SuperAdmin", platformResult.AdminRole.Name);
        Assert.Equal("SuperAdmins", platformResult.AdminsGroup.Name);
        Assert.Equal("Admin", customerResult.AdminRole.Name);
        Assert.Equal("Admins", customerResult.AdminsGroup.Name);
    }

    [Fact]
    public async Task ProvisionAsyncThrowsWhenSlugAlreadyExists()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var slug = Unique("duplicate");
        await service.ProvisionAsync("First", slug);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProvisionAsync("Second", slug));

        Assert.Contains(slug, exception.Message);
    }
}
