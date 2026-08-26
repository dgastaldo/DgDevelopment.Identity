using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;

namespace DgDevelopment.Identity.Application.Tenants;

public sealed class TenantProvisioningService(
    ITenantRepository tenantRepository,
    IPlatformRepository platformRepository,
    IPermissionRepository permissionRepository,
    IRoleRepository roleRepository,
    IGroupRepository groupRepository) : ITenantProvisioningService
{
    public async Task<TenantProvisioningResult> ProvisionAsync(string name, string slug, bool isPlatformTenant = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        if (await tenantRepository.GetBySlugAsync(slug, ct).ConfigureAwait(false) is not null)
            throw new InvalidOperationException($"A tenant with slug '{slug}' already exists.");

        var tenant = new Tenant(name, slug, isPlatformTenant);
        await tenantRepository.AddAsync(tenant, ct).ConfigureAwait(false);

        var platform = new Platform(tenant.Id, "IdentityAdmin", "Identity administration platform", PermissionMode.IdentityManaged);
        await platformRepository.AddAsync(platform, ct).ConfigureAwait(false);

        var permissions = StandardPermissionCatalog(tenant.Id, platform.Id);
        foreach (var permission in permissions)
            await permissionRepository.AddAsync(permission, ct).ConfigureAwait(false);

        // "SuperAdmin" is reserved for the one tenant that owns the platform itself - every other
        // (customer) tenant gets a regular "Admin" role instead, even though it holds the same
        // full permission set within its own tenant-scoped catalog. Naming it "SuperAdmin" would
        // wrongly imply platform-wide power that IsGlobalAdministratorAsync's IsPlatformTenant
        // check no longer grants it anyway.
        var (roleName, roleDescription, groupName, groupDescription) = isPlatformTenant
            ? ("SuperAdmin", "Full system access with all permissions", "SuperAdmins", "Super administrator group")
            : ("Admin", "Full access to this tenant's own resources", "Admins", "Tenant administrator group");

        var adminRole = new Role(tenant.Id, platform.Id, roleName, roleDescription);
        foreach (var permission in permissions)
            adminRole.AddPermission(permission);
        await roleRepository.AddAsync(adminRole, ct).ConfigureAwait(false);

        var adminsGroup = new Group(tenant.Id, groupName, groupDescription);
        adminsGroup.AddRole(adminRole);
        await groupRepository.AddAsync(adminsGroup, ct).ConfigureAwait(false);

        return new TenantProvisioningResult(tenant, platform, adminRole, adminsGroup);
    }

    /// <summary>
    /// Kept in sync by hand with the permission names every [RequirePermission("...")] attribute in
    /// the Server project actually checks for - there is no single source of truth to generate this
    /// from, so a newly added admin-API permission needs a matching line here too.
    /// </summary>
    private static IReadOnlyCollection<Permission> StandardPermissionCatalog(Guid tenantId, Guid platformId) =>
    [
        new(tenantId, platformId, "identity-platform.user.read", "Read users", "User"),
        new(tenantId, platformId, "identity-platform.user.create", "Create users", "User"),
        new(tenantId, platformId, "identity-platform.user.update", "Update users", "User"),
        new(tenantId, platformId, "identity-platform.user.delete", "Delete users", "User"),
        new(tenantId, platformId, "identity-platform.user.read.all-tenants", "Read users across all tenants", "User", isGlobal: true),
        new(tenantId, platformId, "identity-platform.role.read", "Read roles", "Role"),
        new(tenantId, platformId, "identity-platform.role.create", "Create roles", "Role"),
        new(tenantId, platformId, "identity-platform.role.update", "Update roles", "Role"),
        new(tenantId, platformId, "identity-platform.role.delete", "Delete roles", "Role"),
        new(tenantId, platformId, "identity-platform.permission.read", "Read permissions", "Permission"),
        new(tenantId, platformId, "identity-platform.permission.create", "Create permissions", "Permission"),
        new(tenantId, platformId, "identity-platform.permission.update", "Update permissions", "Permission"),
        new(tenantId, platformId, "identity-platform.permission.delete", "Delete permissions", "Permission"),
        new(tenantId, platformId, "identity-platform.group.read", "Read groups", "Group"),
        new(tenantId, platformId, "identity-platform.group.create", "Create groups", "Group"),
        new(tenantId, platformId, "identity-platform.group.update", "Update groups", "Group"),
        new(tenantId, platformId, "identity-platform.group.delete", "Delete groups", "Group"),
        new(tenantId, platformId, "identity-platform.platform.read", "Read platforms", "Platform"),
        new(tenantId, platformId, "identity-platform.platform.create", "Create platforms", "Platform"),
        new(tenantId, platformId, "identity-platform.platform.update", "Update platforms", "Platform"),
        new(tenantId, platformId, "identity-platform.platform.delete", "Delete platforms", "Platform"),
        new(tenantId, platformId, "identity-platform.client.read", "Read clients", "Client"),
        new(tenantId, platformId, "identity-platform.client.create", "Create clients", "Client"),
        new(tenantId, platformId, "identity-platform.client.update", "Update clients", "Client"),
        new(tenantId, platformId, "identity-platform.client.delete", "Delete clients", "Client"),
        new(tenantId, platformId, "identity-platform.audit.read", "Read audit logs", "Audit"),
        new(tenantId, platformId, "identity-platform.audit.read.all-tenants", "Read audit logs across all tenants", "Audit", isGlobal: true),
        new(tenantId, platformId, "identity-platform.tenant.read", "Read tenants", "Tenant", isGlobal: true),
        new(tenantId, platformId, "identity-platform.tenant.create", "Create tenants", "Tenant", isGlobal: true),
        new(tenantId, platformId, "identity-platform.identity.manage", "Manage identity system settings", "Identity", isGlobal: true),
        new(tenantId, platformId, "identity-platform.identity.superadmin", "Super administrator access", "Identity", isGlobal: true),
        new(tenantId, platformId, "identity-platform.dashboard.read", "View identity platform dashboard", "Dashboard"),
    ];
}
