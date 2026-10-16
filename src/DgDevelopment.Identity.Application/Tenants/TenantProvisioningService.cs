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
    private const string UserCategory = "User";
    private const string RoleCategory = "Role";
    private const string PermissionCategory = "Permission";
    private const string GroupCategory = "Group";
    private const string PlatformCategory = "Platform";
    private const string ClientCategory = "Client";
    private const string AuditCategory = "Audit";
    private const string TenantCategory = "Tenant";
    private const string IdentityCategory = "Identity";
    private const string DashboardCategory = "Dashboard";

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

        // GlobalAdmin is the tenant's standard full-access role, created for every tenant - the
        // name doesn't grant cross-tenant power by itself, that's still gated entirely by
        // IsGlobalAdministratorAsync's IsPlatformTenant check. SuperAdmin is a separate, additional
        // role that only ever exists on the platform tenant, tied to the one seeded bootstrap
        // system account (see DbSeeder) - never created for a customer tenant.
        var globalAdminRole = new Role(tenant.Id, platform.Id, "GlobalAdmin", "Full access to this tenant's own resources");
        foreach (var permission in permissions)
            globalAdminRole.AddPermission(permission);
        await roleRepository.AddAsync(globalAdminRole, ct).ConfigureAwait(false);

        var globalAdminsGroup = new Group(tenant.Id, "GlobalAdmins", "Tenant administrator group");
        globalAdminsGroup.AddRole(globalAdminRole);
        await groupRepository.AddAsync(globalAdminsGroup, ct).ConfigureAwait(false);

        Role? superAdminRole = null;
        Group? superAdminsGroup = null;
        if (isPlatformTenant)
        {
            superAdminRole = new Role(tenant.Id, platform.Id, "SuperAdmin", "Full system access with all permissions");
            foreach (var permission in permissions)
                superAdminRole.AddPermission(permission);
            await roleRepository.AddAsync(superAdminRole, ct).ConfigureAwait(false);

            superAdminsGroup = new Group(tenant.Id, "SuperAdmins", "Super administrator group");
            superAdminsGroup.AddRole(superAdminRole);
            await groupRepository.AddAsync(superAdminsGroup, ct).ConfigureAwait(false);
        }

        return new TenantProvisioningResult(tenant, platform, globalAdminRole, globalAdminsGroup, superAdminRole, superAdminsGroup);
    }

    public async Task ReconcileAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(tenantId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Tenant '{tenantId}' does not exist.");

        var platforms = await platformRepository.GetAllAsync(ct).ConfigureAwait(false);
        var platform = platforms.First(p => p.TenantId == tenantId && p.Name == "IdentityAdmin");

        var existingPermissions = await permissionRepository.GetByTenantAsync(tenantId, ct).ConfigureAwait(false);
        var existingNames = existingPermissions.Select(p => p.Name).ToHashSet();

        var missingPermissions = StandardPermissionCatalog(tenantId, platform.Id)
            .Where(p => !existingNames.Contains(p.Name))
            .ToList();
        foreach (var permission in missingPermissions)
            await permissionRepository.AddAsync(permission, ct).ConfigureAwait(false);

        var allPermissions = existingPermissions.Concat(missingPermissions);

        var roles = await roleRepository.GetAllAsync(ct).ConfigureAwait(false);
        var rolesToReconcile = roles.Where(r => r.TenantId == tenantId
            && (r.Name == "GlobalAdmin" || (tenant.IsPlatformTenant && r.Name == "SuperAdmin")));

        foreach (var role in rolesToReconcile)
        {
            var permissionCountBeforeReconcile = role.Permissions.Count;
            foreach (var permission in allPermissions)
                role.AddPermission(permission);

            if (role.Permissions.Count > permissionCountBeforeReconcile)
                await roleRepository.UpdateAsync(role, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Kept in sync by hand with the permission names every [RequirePermission("...")] attribute in
    /// the Server project actually checks for - there is no single source of truth to generate this
    /// from, so a newly added admin-API permission needs a matching line here too.
    /// </summary>
    private static IReadOnlyCollection<Permission> StandardPermissionCatalog(Guid tenantId, Guid platformId) =>
    [
        new(tenantId, platformId, "identity-platform.user.read", "Read users", UserCategory),
        new(tenantId, platformId, "identity-platform.user.create", "Create users", UserCategory),
        new(tenantId, platformId, "identity-platform.user.update", "Update users", UserCategory),
        new(tenantId, platformId, "identity-platform.user.delete", "Delete users", UserCategory),
        new(tenantId, platformId, "identity-platform.user.read.all-tenants", "Read users across all tenants", UserCategory, isGlobal: true),
        new(tenantId, platformId, "identity-platform.role.read", "Read roles", RoleCategory),
        new(tenantId, platformId, "identity-platform.role.create", "Create roles", RoleCategory),
        new(tenantId, platformId, "identity-platform.role.update", "Update roles", RoleCategory),
        new(tenantId, platformId, "identity-platform.role.delete", "Delete roles", RoleCategory),
        new(tenantId, platformId, "identity-platform.permission.read", "Read permissions", PermissionCategory),
        new(tenantId, platformId, "identity-platform.permission.create", "Create permissions", PermissionCategory),
        new(tenantId, platformId, "identity-platform.permission.update", "Update permissions", PermissionCategory),
        new(tenantId, platformId, "identity-platform.permission.delete", "Delete permissions", PermissionCategory),
        new(tenantId, platformId, "identity-platform.group.read", "Read groups", GroupCategory),
        new(tenantId, platformId, "identity-platform.group.create", "Create groups", GroupCategory),
        new(tenantId, platformId, "identity-platform.group.update", "Update groups", GroupCategory),
        new(tenantId, platformId, "identity-platform.group.delete", "Delete groups", GroupCategory),
        new(tenantId, platformId, "identity-platform.platform.read", "Read platforms", PlatformCategory),
        new(tenantId, platformId, "identity-platform.platform.create", "Create platforms", PlatformCategory),
        new(tenantId, platformId, "identity-platform.platform.update", "Update platforms", PlatformCategory),
        new(tenantId, platformId, "identity-platform.platform.delete", "Delete platforms", PlatformCategory),
        new(tenantId, platformId, "identity-platform.client.read", "Read clients", ClientCategory),
        new(tenantId, platformId, "identity-platform.client.create", "Create clients", ClientCategory),
        new(tenantId, platformId, "identity-platform.client.update", "Update clients", ClientCategory),
        new(tenantId, platformId, "identity-platform.client.delete", "Delete clients", ClientCategory),
        new(tenantId, platformId, "identity-platform.audit.read", "Read audit logs", AuditCategory),
        new(tenantId, platformId, "identity-platform.audit.read.all-tenants", "Read audit logs across all tenants", AuditCategory, isGlobal: true),
        new(tenantId, platformId, "identity-platform.tenant.read", "Read tenants", TenantCategory, isGlobal: true),
        new(tenantId, platformId, "identity-platform.tenant.create", "Create tenants", TenantCategory, isGlobal: true),
        new(tenantId, platformId, "identity-platform.tenant.update", "Update tenants", TenantCategory, isGlobal: true),
        new(tenantId, platformId, "identity-platform.identity.manage", "Manage identity system settings", IdentityCategory, isGlobal: true),
        new(tenantId, platformId, "identity-platform.identity.superadmin", "Super administrator access", IdentityCategory, isGlobal: true),
        new(tenantId, platformId, "identity-platform.dashboard.read", "View identity platform dashboard", DashboardCategory),
    ];
}
