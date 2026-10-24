namespace DgDevelopment.Identity.Server.UnitTests.Testing;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public static class TestDbSeeder
{
    public static async Task SeedAsync(IdentityDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        var tenantId = await SeedDefaultTenantAsync(db).ConfigureAwait(false);
        var platformId = await SeedPlatformsAsync(db, tenantId).ConfigureAwait(false);
        await SeedPermissionsAsync(db, tenantId, platformId).ConfigureAwait(false);
        await SeedRolesAsync(db, tenantId, platformId).ConfigureAwait(false);
        await SeedGroupsAsync(db, tenantId).ConfigureAwait(false);
        await SeedClientsAsync(db, tenantId).ConfigureAwait(false);
        await SeedUsersAsync(db, tenantId).ConfigureAwait(false);
    }

    private static async Task<Guid> SeedDefaultTenantAsync(IdentityDbContext db)
    {
        var existing = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == TestConstants.DefaultTenantSlug).ConfigureAwait(false);
        if (existing is not null)
            return existing.Id;

        var tenant = new Tenant("Identity Tenant", TestConstants.DefaultTenantSlug);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return tenant.Id;
    }

    private static async Task SeedPermissionsAsync(IdentityDbContext db, Guid tenantId, Guid platformId)
    {
        if (await db.Permissions.AnyAsync().ConfigureAwait(false)) return;

        var permissions = new[]
        {
            new Permission(tenantId, platformId, "identity-platform.user.read", "Read users", "User"),
            new Permission(tenantId, platformId, "identity-platform.user.create", "Create users", "User"),
            new Permission(tenantId, platformId, "identity-platform.user.update", "Update users", "User"),
            new Permission(tenantId, platformId, "identity-platform.user.delete", "Delete users", "User"),
            new Permission(tenantId, platformId, "identity-platform.user.read.all-tenants", "Read users across all tenants", "User", isGlobal: true),
            new Permission(tenantId, platformId, "identity-platform.role.read", "Read roles", "Role"),
            new Permission(tenantId, platformId, "identity-platform.role.create", "Create roles", "Role"),
            new Permission(tenantId, platformId, "identity-platform.role.update", "Update roles", "Role"),
            new Permission(tenantId, platformId, "identity-platform.role.delete", "Delete roles", "Role"),
            new Permission(tenantId, platformId, "identity-platform.permission.read", "Read permissions", "Permission"),
            new Permission(tenantId, platformId, "identity-platform.permission.create", "Create permissions", "Permission"),
            new Permission(tenantId, platformId, "identity-platform.permission.update", "Update permissions", "Permission"),
            new Permission(tenantId, platformId, "identity-platform.permission.delete", "Delete permissions", "Permission"),
            new Permission(tenantId, platformId, "identity-platform.group.read", "Read groups", "Group"),
            new Permission(tenantId, platformId, "identity-platform.group.create", "Create groups", "Group"),
            new Permission(tenantId, platformId, "identity-platform.group.update", "Update groups", "Group"),
            new Permission(tenantId, platformId, "identity-platform.group.delete", "Delete groups", "Group"),
            new Permission(tenantId, platformId, "identity-platform.platform.read", "Read platforms", "Platform"),
            new Permission(tenantId, platformId, "identity-platform.platform.create", "Create platforms", "Platform"),
            new Permission(tenantId, platformId, "identity-platform.platform.update", "Update platforms", "Platform"),
            new Permission(tenantId, platformId, "identity-platform.platform.delete", "Delete platforms", "Platform"),
            new Permission(tenantId, platformId, "identity-platform.client.read", "Read clients", "Client"),
            new Permission(tenantId, platformId, "identity-platform.client.create", "Create clients", "Client"),
            new Permission(tenantId, platformId, "identity-platform.client.update", "Update clients", "Client"),
            new Permission(tenantId, platformId, "identity-platform.client.delete", "Delete clients", "Client"),
            new Permission(tenantId, platformId, "identity-platform.audit.read", "Read audit logs", "Audit"),
            new Permission(tenantId, platformId, "identity-platform.audit.read.all-tenants", "Read audit logs across all tenants", "Audit", isGlobal: true),
            new Permission(tenantId, platformId, "identity-platform.tenant.read", "Read tenants", "Tenant", isGlobal: true),
            new Permission(tenantId, platformId, "identity-platform.tenant.create", "Create tenants", "Tenant", isGlobal: true),
            new Permission(tenantId, platformId, "identity-platform.identity.manage", "Manage identity system settings", "Identity", isGlobal: true),
            new Permission(tenantId, platformId, "identity-platform.identity.superadmin", "Super administrator access", "Identity", isGlobal: true),
            new Permission(tenantId, platformId, "identity-platform.dashboard.read", "View identity platform dashboard", "Dashboard"),
        };

        db.Permissions.AddRange(permissions);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedRolesAsync(IdentityDbContext db, Guid tenantId, Guid platformId)
    {
        if (await db.Roles.AnyAsync().ConfigureAwait(false)) return;

        var permissions = await db.Permissions.ToListAsync().ConfigureAwait(false);

        var superAdmin = new Role(tenantId, platformId, "SuperAdmin", "Full system access with all permissions");
        foreach (var permission in permissions)
            superAdmin.AddPermission(permission);

        db.Roles.Add(superAdmin);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedGroupsAsync(IdentityDbContext db, Guid tenantId)
    {
        if (await db.Groups.AnyAsync().ConfigureAwait(false)) return;

        var superAdminRole = await db.Roles.FirstAsync(r => r.Name == "SuperAdmin").ConfigureAwait(false);

        var superAdmins = new Group(tenantId, "SuperAdmins", "Super administrator group");
        superAdmins.AddRole(superAdminRole);

        db.Groups.Add(superAdmins);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task<Guid> SeedPlatformsAsync(IdentityDbContext db, Guid tenantId)
    {
        var existing = await db.Platforms.FirstOrDefaultAsync(p => p.Name == "IdentityAdmin").ConfigureAwait(false);
        if (existing is not null)
            return existing.Id;

        var platform = new Platform(tenantId, "IdentityAdmin", "Identity administration platform", PermissionMode.IdentityManaged);
        db.Platforms.Add(platform);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return platform.Id;
    }

    private static async Task SeedClientsAsync(IdentityDbContext db, Guid tenantId)
    {
        if (await db.Clients.AnyAsync().ConfigureAwait(false)) return;

        var platform = await db.Platforms.FirstAsync(p => p.Name == "IdentityAdmin").ConfigureAwait(false);
        var clientSecretHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(TestConstants.AdminClientSecret)));

        var client = new Client(tenantId, Guid.Parse(TestConstants.AdminClientId), clientSecretHash, "identity-platform", ClientType.Confidential, platform.Id);
        client.AddGrantType("authorization_code");
        client.AddGrantType("client_credentials");
        client.AddGrantType("refresh_token");
        client.AddGrantType("device_code");
        client.AddScope("openid");
        client.AddScope("profile");
        client.AddScope("email");
        client.AddRedirectUri(new Uri("https://localhost:7018/callback"));
        client.AddRedirectUri(new Uri("http://localhost:5133/callback"));
        client.AddPostLogoutRedirectUri(new Uri("https://localhost:7018/"));

        db.Clients.Add(client);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedUsersAsync(IdentityDbContext db, Guid tenantId)
    {
        if (await db.Users.AnyAsync().ConfigureAwait(false)) return;

        var hasher = new FakePasswordHasher();
        var passwordHash = hasher.HashPassword(TestConstants.SuperAdminPassword);
        var email = EmailAddress.FromString(TestConstants.SuperAdminEmail);
        var user = new User(TestConstants.SuperAdminUserName, passwordHash, email, isSystemAccount: true);
        user.VerifyEmail(email);

        var superAdminsGroup = await db.Groups.FirstAsync(g => g.Name == "SuperAdmins").ConfigureAwait(false);
        user.AddToGroup(superAdminsGroup);

        db.Users.Add(user);
        db.TenantMemberships.Add(new TenantMembership(tenantId, user.Id, isOwner: true));
        await db.SaveChangesAsync().ConfigureAwait(false);
    }
}
