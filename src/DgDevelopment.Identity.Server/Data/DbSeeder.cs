using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.OAuth.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;

namespace DgDevelopment.Identity.Server.Data;

public sealed class DbSeeder
{
    private readonly IServiceProvider _serviceProvider;

    public DbSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task SeedAsync()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await SeedPermissionsAsync(db);
        await SeedRolesAsync(db);
        await SeedGroupsAsync(db);
        await SeedPlatformsAsync(db);
        await SeedClientsAsync(db);
        await SeedUsersAsync(db, scope.ServiceProvider);
    }

    private static async Task SeedPermissionsAsync(IdentityDbContext db)
    {
        if (await db.Permissions.AnyAsync().ConfigureAwait(false)) return;

        var permissions = new[]
        {
            new Permission("user:read", "Read users", "User"),
            new Permission("user:create", "Create users", "User"),
            new Permission("user:update", "Update users", "User"),
            new Permission("user:delete", "Delete users", "User"),
            new Permission("role:read", "Read roles", "Role"),
            new Permission("role:create", "Create roles", "Role"),
            new Permission("role:update", "Update roles", "Role"),
            new Permission("role:delete", "Delete roles", "Role"),
            new Permission("permission:read", "Read permissions", "Permission"),
            new Permission("permission:create", "Create permissions", "Permission"),
            new Permission("permission:update", "Update permissions", "Permission"),
            new Permission("permission:delete", "Delete permissions", "Permission"),
            new Permission("group:read", "Read groups", "Group"),
            new Permission("group:create", "Create groups", "Group"),
            new Permission("group:update", "Update groups", "Group"),
            new Permission("group:delete", "Delete groups", "Group"),
            new Permission("platform:read", "Read platforms", "Platform"),
            new Permission("platform:create", "Create platforms", "Platform"),
            new Permission("platform:update", "Update platforms", "Platform"),
            new Permission("platform:delete", "Delete platforms", "Platform"),
            new Permission("client:read", "Read clients", "Client"),
            new Permission("client:create", "Create clients", "Client"),
            new Permission("client:update", "Update clients", "Client"),
            new Permission("client:delete", "Delete clients", "Client"),
            new Permission("audit:read", "Read audit logs", "Audit"),
            new Permission("identity:manage", "Manage identity system settings", "Identity"),
            new Permission("identity:superadmin", "Super administrator access", "Identity"),
        };

        db.Permissions.AddRange(permissions);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedRolesAsync(IdentityDbContext db)
    {
        if (await db.Roles.AnyAsync().ConfigureAwait(false)) return;

        var permissions = await db.Permissions.ToListAsync().ConfigureAwait(false);

        var superAdmin = new Role("SuperAdmin", "Full system access with all permissions");

        foreach (var permission in permissions)
            superAdmin.AddPermission(permission);

        db.Roles.Add(superAdmin);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedGroupsAsync(IdentityDbContext db)
    {
        if (await db.Groups.AnyAsync().ConfigureAwait(false)) return;

        var superAdminRole = await db.Roles.FirstAsync(r => r.Name == "SuperAdmin").ConfigureAwait(false);

        var superAdmins = new Group("SuperAdmins", "Super administrator group");
        superAdmins.AddRole(superAdminRole);

        db.Groups.Add(superAdmins);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedPlatformsAsync(IdentityDbContext db)
    {
        if (await db.Platforms.AnyAsync().ConfigureAwait(false)) return;

        var platform = new Platform("IdentityAdmin", "Identity administration platform", PermissionMode.IdentityManaged);
        db.Platforms.Add(platform);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedClientsAsync(IdentityDbContext db)
    {
        if (await db.Clients.AnyAsync().ConfigureAwait(false)) return;

        var platform = await db.Platforms.FirstAsync(p => p.Name == "IdentityAdmin").ConfigureAwait(false);
        var clientSecret = Secret.Generate(32);
        var clientSecretHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(clientSecret)));

        var client = new Client("admin-ui", clientSecretHash, "Admin UI", ClientType.Confidential, platform.Id);
        client.AddGrantType("authorization_code");
        client.AddGrantType("client_credentials");
        client.AddGrantType("refresh_token");
        client.AddScope("openid");
        client.AddScope("profile");
        client.AddScope("email");
        client.AddRedirectUri(new Uri("https://localhost:7157/signin-oidc"));
        client.AddRedirectUri(new Uri("http://localhost:5281/signin-oidc"));
        client.AddPostLogoutRedirectUri(new Uri("https://localhost:7157/signout-callback-oidc"));

        db.Clients.Add(client);
        await db.SaveChangesAsync().ConfigureAwait(false);

        Console.WriteLine($"--- Admin UI Client Secret: {clientSecret} ---");
    }

    private static async Task SeedUsersAsync(IdentityDbContext db, IServiceProvider sp)
    {
        if (await db.Users.AnyAsync().ConfigureAwait(false)) return;

        var password = Secret.Generate(16);
        var passwordHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)));

        var email = EmailAddress.FromString("identity.superadmin@dgdevelopment.it");
        var user = new User("identity.superadmin", passwordHash, email, isSystemAccount: true);
        user.VerifyEmail(email);

        var superAdminsGroup = await db.Groups.FirstAsync(g => g.Name == "SuperAdmins").ConfigureAwait(false);
        user.AddToGroup(superAdminsGroup);

        db.Users.Add(user);
        await db.SaveChangesAsync().ConfigureAwait(false);

        Console.WriteLine("==============================================");
        Console.WriteLine("  SUPERADMIN CREDENTIALS (SAVE THESE!)");
        Console.WriteLine("  Username: identity.superadmin");
        Console.WriteLine($"  Password: {password}");
        Console.WriteLine("  Email:    identity.superadmin@dgdevelopment.it");
        Console.WriteLine("==============================================");
    }
}
