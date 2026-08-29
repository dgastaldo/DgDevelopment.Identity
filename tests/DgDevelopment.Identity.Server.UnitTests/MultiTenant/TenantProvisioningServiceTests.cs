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
        Assert.NotEmpty(result.GlobalAdminRole.Permissions);
        Assert.Equal(result.GlobalAdminRole.Permissions.Count,
            (await new PermissionRepository(context).GetAllAsync())
                .Count(p => p.TenantId == result.Tenant.Id));
        Assert.Contains(result.GlobalAdminsGroup.Roles, r => r.RoleId == result.GlobalAdminRole.Id);
        Assert.Null(result.SuperAdminRole);
        Assert.Null(result.SuperAdminsGroup);
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

    // GlobalAdmin is created for every tenant (the standard full-access role), but SuperAdmin is
    // a separate, additional role that only ever exists on the tenant that owns the platform
    // itself, tied to the one seeded bootstrap system account - never created for a customer tenant.
    [Fact]
    public async Task ProvisionAsyncOnlyAddsASuperAdminRoleForThePlatformTenant()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var platformResult = await service.ProvisionAsync("Platform Owner", Unique("platform-owner-naming"), isPlatformTenant: true);
        var customerResult = await service.ProvisionAsync("Customer", Unique("customer-naming"));

        Assert.Equal("GlobalAdmin", platformResult.GlobalAdminRole.Name);
        Assert.Equal("GlobalAdmins", platformResult.GlobalAdminsGroup.Name);
        Assert.Equal("SuperAdmin", platformResult.SuperAdminRole?.Name);
        Assert.Equal("SuperAdmins", platformResult.SuperAdminsGroup?.Name);

        Assert.Equal("GlobalAdmin", customerResult.GlobalAdminRole.Name);
        Assert.Equal("GlobalAdmins", customerResult.GlobalAdminsGroup.Name);
        Assert.Null(customerResult.SuperAdminRole);
        Assert.Null(customerResult.SuperAdminsGroup);
    }

    [Fact]
    public async Task ReconcileAsyncBackfillsAMissingPermissionAndGrantsItToGlobalAdmin()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var permissionRepository = new PermissionRepository(context);
        var roleRepository = new RoleRepository(context);
        var provisioned = await service.ProvisionAsync("Reconcile Tenant", Unique("reconcile"));

        // Simulates catalog drift: a permission the current StandardPermissionCatalog says this
        // tenant should have is missing from its catalog (e.g. added to the catalog after this
        // tenant was already provisioned).
        var permissions = await permissionRepository.GetByTenantAsync(provisioned.Tenant.Id);
        var staleUpdatePermission = permissions.Single(p => p.Name == "identity-platform.tenant.update");
        await permissionRepository.DeleteAsync(staleUpdatePermission.Id);

        await service.ReconcileAsync(provisioned.Tenant.Id);

        var permissionsAfterReconcile = await permissionRepository.GetByTenantAsync(provisioned.Tenant.Id);
        var restoredPermission = Assert.Single(permissionsAfterReconcile, p => p.Name == "identity-platform.tenant.update");

        var globalAdminRole = await roleRepository.GetByIdAsync(provisioned.GlobalAdminRole.Id);
        Assert.Contains(globalAdminRole!.Permissions, rp => rp.PermissionId == restoredPermission.Id);
    }

    [Fact]
    public async Task ReconcileAsyncIsANoOpWhenTheCatalogIsAlreadyUpToDate()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var permissionRepository = new PermissionRepository(context);
        var provisioned = await service.ProvisionAsync("Reconcile NoOp Tenant", Unique("reconcile-noop"));

        var before = await permissionRepository.GetByTenantAsync(provisioned.Tenant.Id);

        await service.ReconcileAsync(provisioned.Tenant.Id);

        var after = await permissionRepository.GetByTenantAsync(provisioned.Tenant.Id);
        Assert.Equal(before.Select(p => p.Id).OrderBy(id => id), after.Select(p => p.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task ReconcileAsyncOnlyGrantsARestoredPermissionToSuperAdminOnThePlatformTenant()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var permissionRepository = new PermissionRepository(context);
        var roleRepository = new RoleRepository(context);
        var provisioned = await service.ProvisionAsync("Reconcile Platform Tenant", Unique("reconcile-platform"), isPlatformTenant: true);

        var permissions = await permissionRepository.GetByTenantAsync(provisioned.Tenant.Id);
        var staleUpdatePermission = permissions.Single(p => p.Name == "identity-platform.tenant.update");
        await permissionRepository.DeleteAsync(staleUpdatePermission.Id);

        await service.ReconcileAsync(provisioned.Tenant.Id);

        var restoredPermission = (await permissionRepository.GetByTenantAsync(provisioned.Tenant.Id))
            .Single(p => p.Name == "identity-platform.tenant.update");
        var superAdminRole = await roleRepository.GetByIdAsync(provisioned.SuperAdminRole!.Id);
        Assert.Contains(superAdminRole!.Permissions, rp => rp.PermissionId == restoredPermission.Id);
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

        Assert.Contains(slug, exception.Message, StringComparison.Ordinal);
    }
}
