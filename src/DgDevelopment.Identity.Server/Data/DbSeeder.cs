using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Services;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.OAuth.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Security.Cryptography;

namespace DgDevelopment.Identity.Server.Data;

public sealed class DbSeeder(IServiceProvider serviceProvider)
{

    public async Task SeedAsync()
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var contentRoot = scope.ServiceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath;

        await db.Database.MigrateAsync().ConfigureAwait(false);

        var tenant = await SeedDefaultTenantAsync(db).ConfigureAwait(false);
        var clientSecret = await SeedPermissionsAsync(db).ConfigureAwait(false);
        await SeedRolesAsync(db, tenant.Id).ConfigureAwait(false);
        await SeedGroupsAsync(db, tenant.Id).ConfigureAwait(false);
        await SeedPlatformsAsync(db, tenant.Id).ConfigureAwait(false);
        var clientCredentials = clientSecret is null
            ? await SeedClientsAsync(db, tenant.Id).ConfigureAwait(false)
            : null;
        var hasher = scope.ServiceProvider.GetRequiredService<Domain.Services.IPasswordHasher>();
        var superadminPassword = await SeedUsersAsync(db, hasher, tenant.Id).ConfigureAwait(false);

        if (superadminPassword != null)
            WriteSuperadminCredentials(contentRoot, superadminPassword);

        if (clientCredentials is { } credentials)
        {
            WriteClientCredentials(contentRoot, credentials.ClientId, credentials.ClientSecret);
            await SyncClientSecretToAppHostAsync(contentRoot, credentials.ClientId, credentials.ClientSecret).ConfigureAwait(false);
        }
    }

    private static async Task<Tenant> SeedDefaultTenantAsync(IdentityDbContext db)
    {
        var existing = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == "default").ConfigureAwait(false);
        if (existing is not null)
            return existing;

        var tenant = new Tenant("Default", "default");
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return tenant;
    }

    private static async Task<string?> SeedPermissionsAsync(IdentityDbContext db)
    {
        if (await db.Permissions.AnyAsync().ConfigureAwait(false)) return null;

        var permissions = new[]
        {
            new Permission("identity-platform.user.read", "Read users", "User"),
            new Permission("identity-platform.user.create", "Create users", "User"),
            new Permission("identity-platform.user.update", "Update users", "User"),
            new Permission("identity-platform.user.delete", "Delete users", "User"),
            new Permission("identity-platform.user.read.all-tenants", "Read users across all tenants", "User", isGlobal: true),
            new Permission("identity-platform.role.read", "Read roles", "Role"),
            new Permission("identity-platform.role.create", "Create roles", "Role"),
            new Permission("identity-platform.role.update", "Update roles", "Role"),
            new Permission("identity-platform.role.delete", "Delete roles", "Role"),
            new Permission("identity-platform.permission.read", "Read permissions", "Permission"),
            new Permission("identity-platform.permission.create", "Create permissions", "Permission"),
            new Permission("identity-platform.permission.update", "Update permissions", "Permission"),
            new Permission("identity-platform.permission.delete", "Delete permissions", "Permission"),
            new Permission("identity-platform.group.read", "Read groups", "Group"),
            new Permission("identity-platform.group.create", "Create groups", "Group"),
            new Permission("identity-platform.group.update", "Update groups", "Group"),
            new Permission("identity-platform.group.delete", "Delete groups", "Group"),
            new Permission("identity-platform.platform.read", "Read platforms", "Platform"),
            new Permission("identity-platform.platform.create", "Create platforms", "Platform"),
            new Permission("identity-platform.platform.update", "Update platforms", "Platform"),
            new Permission("identity-platform.platform.delete", "Delete platforms", "Platform"),
            new Permission("identity-platform.client.read", "Read clients", "Client"),
            new Permission("identity-platform.client.create", "Create clients", "Client"),
            new Permission("identity-platform.client.update", "Update clients", "Client"),
            new Permission("identity-platform.client.delete", "Delete clients", "Client"),
            new Permission("identity-platform.audit.read", "Read audit logs", "Audit"),
            new Permission("identity-platform.audit.read.all-tenants", "Read audit logs across all tenants", "Audit", isGlobal: true),
            new Permission("identity-platform.tenant.read", "Read tenants", "Tenant", isGlobal: true),
            new Permission("identity-platform.identity.manage", "Manage identity system settings", "Identity", isGlobal: true),
            new Permission("identity-platform.identity.superadmin", "Super administrator access", "Identity", isGlobal: true),
            new Permission("identity-platform.dashboard.read", "View identity platform dashboard", "Dashboard"),
        };

        db.Permissions.AddRange(permissions);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return null;
    }

    private static async Task SeedRolesAsync(IdentityDbContext db, Guid tenantId)
    {
        if (await db.Roles.AnyAsync().ConfigureAwait(false)) return;

        var permissions = await db.Permissions.ToListAsync().ConfigureAwait(false);

        var superAdmin = new Role(tenantId, "SuperAdmin", "Full system access with all permissions");

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

    private static async Task SeedPlatformsAsync(IdentityDbContext db, Guid tenantId)
    {
        if (await db.Platforms.AnyAsync().ConfigureAwait(false)) return;

        var platform = new Platform(tenantId, "IdentityAdmin", "Identity administration platform", PermissionMode.IdentityManaged);
        db.Platforms.Add(platform);
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task<(Guid ClientId, string ClientSecret)?> SeedClientsAsync(IdentityDbContext db, Guid tenantId)
    {
        if (await db.Clients.AnyAsync().ConfigureAwait(false)) return null;

        var platform = await db.Platforms.FirstAsync(p => p.Name == "IdentityAdmin").ConfigureAwait(false);
        var clientSecret = Secret.Generate(32);
        var clientSecretHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(clientSecret)));

        var client = new Client(tenantId, Guid.NewGuid(), clientSecretHash, "identity-platform", ClientType.Confidential, platform.Id);
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

        Console.WriteLine($"--- IdentityPlatform Client ID: {client.ClientId} ---");
        Console.WriteLine($"--- IdentityPlatform Client Secret: {clientSecret} ---");
        return (client.ClientId, clientSecret);
    }

    private static async Task<string?> SeedUsersAsync(IdentityDbContext db, Domain.Services.IPasswordHasher hasher, Guid tenantId)
    {
        if (await db.Users.AnyAsync().ConfigureAwait(false)) return null;

        var password = Secret.Generate(16);
        var passwordHash = hasher.HashPassword(password);

        var email = EmailAddress.FromString("identity.superadmin@dgdevelopment.it");
        var user = new User("identity.superadmin", passwordHash, email, isSystemAccount: true);
        user.VerifyEmail(email);

        var superAdminsGroup = await db.Groups.FirstAsync(g => g.Name == "SuperAdmins").ConfigureAwait(false);
        user.AddToGroup(superAdminsGroup);

        db.Users.Add(user);
        db.TenantMemberships.Add(new TenantMembership(tenantId, user.Id, isOwner: true));
        await db.SaveChangesAsync().ConfigureAwait(false);

        Console.WriteLine("==============================================");
        Console.WriteLine("  SUPERADMIN CREDENTIALS (SAVE THESE!)");
        Console.WriteLine("  Username: identity.superadmin");
        Console.WriteLine($"  Password: {password}");
        Console.WriteLine("  Email:    identity.superadmin@dgdevelopment.it");
        Console.WriteLine("==============================================");

        return password;
    }

    private static void WriteSuperadminCredentials(string contentRoot, string password)
    {
        var path = Path.Combine(contentRoot, "superadmin-credentials.txt");
        var content = $"""
        ==============================================
          DgDevelopment Identity - SuperAdmin Credentials
        ==============================================
          Username: identity.superadmin
          Password: {password}
          Email:    identity.superadmin@dgdevelopment.it
        ==============================================
          Store this file in a secure location.
          Do not commit to version control.
        ==============================================
        """;

        File.WriteAllText(path, content);
        Console.WriteLine($"SuperAdmin credentials saved to: {path}");
    }

    private static void WriteClientCredentials(string contentRoot, Guid clientId, string clientSecret)
    {
        var path = Path.Combine(contentRoot, "admin-client-credentials.txt");
        var content = $"""
        ==============================================
          DgDevelopment Identity - Admin Client Credentials
        ==============================================
          Client ID: {clientId}
          Client Secret: {clientSecret}
        ==============================================
          Store this file in a secure location.
          Do not commit to version control.
        ==============================================
        """;

        File.WriteAllText(path, content);
        Console.WriteLine($"Admin client credentials saved to: {path}");
    }

    private static async Task SyncClientSecretToAppHostAsync(string contentRoot, Guid clientId, string clientSecret, CancellationToken ct = default)
    {
        var appHostDir = Path.GetFullPath(Path.Combine(contentRoot, "..", "DgDevelopment.Identity.AppHost"));

        if (!Directory.Exists(appHostDir))
        {
            Console.WriteLine($"AppHost secret sync skipped: directory not found at {appHostDir}");
            return;
        }

        try
        {
            foreach (var (key, value) in new[]
                     {
                         ("Identity:AdminClientId", clientId.ToString()),
                         ("Identity:AdminClientSecret", clientSecret)
                     })
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"user-secrets set \"{key}\" \"{value}\" --project \"{appHostDir}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                    return;

                var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
                var error = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
                await process.WaitForExitAsync(ct).ConfigureAwait(false);

                Console.WriteLine(output.Trim());
                if (!string.IsNullOrWhiteSpace(error))
                    Console.WriteLine(error.Trim());
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException or OperationCanceledException)
        {
            Console.WriteLine($"AppHost secret sync failed: {ex.Message}");
        }
    }
}
