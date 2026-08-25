namespace DgDevelopment.Identity.IntegrationTests;

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    public IdentityWebApplicationFactory Factory { get; } = new();

    public Guid DefaultTenantId { get; private set; }
    public Guid SuperAdminUserId { get; private set; }
    public Guid SuperAdminRoleId { get; private set; }
    public Guid SecondTenantId { get; private set; }
    public Guid SecondTenantRoleId { get; private set; }
    public string AccessToken { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await using (var context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
            await context.Database.MigrateAsync().ConfigureAwait(false);
        }

        await using (var context = CreateContext())
        {
            await SeedAsync(context).ConfigureAwait(false);
        }

        // Triggers real host startup (DbSeeder.SeedAsync runs here, but every table it checks is
        // already populated above, so every one of its seed steps short-circuits as a no-op -
        // no credential files written to disk, no AppHost user-secrets sync attempted).
        var loginClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri(IdentityWebApplicationFactory.IssuerBaseAddress),
        });
        var oidc = new OidcTestClient(loginClient);
        AccessToken = await oidc.LoginAndGetAccessTokenAsync(
            IntegrationTestConstants.SuperAdminUserName,
            IntegrationTestConstants.SuperAdminPassword,
            IntegrationTestConstants.AdminClientId,
            IntegrationTestConstants.AdminClientSecret,
            IntegrationTestConstants.RedirectUri).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
        await Factory.DisposeAsync().ConfigureAwait(false);
    }

    public IdentityDbContext CreateContext()
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(IdentityWebApplicationFactory.ConnectionString)
            .Options);

    public HttpClient CreateAuthenticatedClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        return client;
    }

    /// <summary>Creates a second tenant with its own role, for cross-tenant 403 scenarios.</summary>
    public async Task<(Guid TenantId, Guid RoleId)> EnsureSecondTenantAsync()
    {
        if (SecondTenantId != Guid.Empty)
            return (SecondTenantId, SecondTenantRoleId);

        await using var context = CreateContext();
        var tenant = new Tenant("Second Tenant", IntegrationTestConstants.SecondTenantSlug);
        context.Tenants.Add(tenant);
        var role = new Role(tenant.Id, "Other Tenant Role", "cross-tenant test role");
        context.Roles.Add(role);
        await context.SaveChangesAsync().ConfigureAwait(false);

        SecondTenantId = tenant.Id;
        SecondTenantRoleId = role.Id;
        return (SecondTenantId, SecondTenantRoleId);
    }

    private async Task SeedAsync(IdentityDbContext db)
    {
        // FixDefaultTenantSeeding (the migration after AddMultiTenancy) removes any tenant the
        // old migration created spuriously on a database with nothing to backfill, so a freshly
        // migrated test database has no pre-existing tenant - this method is the sole owner of
        // seeding one, matching what DbSeeder.SeedDefaultTenantAsync does for a real install.
        var existingRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "SuperAdmin").ConfigureAwait(false);
        if (existingRole is not null)
        {
            SuperAdminRoleId = existingRole.Id;
            DefaultTenantId = existingRole.TenantId;
            SuperAdminUserId = await db.Users.Where(u => u.Username == IntegrationTestConstants.SuperAdminUserName).Select(u => u.Id).SingleAsync().ConfigureAwait(false);
            return;
        }

        var hasher = new FakePasswordHasher();

        var tenant = new Tenant("Identity Tenant", IntegrationTestConstants.DefaultTenantSlug);
        db.Tenants.Add(tenant);
        DefaultTenantId = tenant.Id;

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
            new Permission("identity-platform.group.read", "Read groups", "Group"),
            new Permission("identity-platform.group.create", "Create groups", "Group"),
            new Permission("identity-platform.group.update", "Update groups", "Group"),
            new Permission("identity-platform.group.delete", "Delete groups", "Group"),
            new Permission("identity-platform.platform.read", "Read platforms", "Platform"),
            new Permission("identity-platform.client.read", "Read clients", "Client"),
            new Permission("identity-platform.audit.read", "Read audit logs", "Audit"),
            new Permission("identity-platform.tenant.read", "Read tenants", "Tenant", isGlobal: true),
            new Permission("identity-platform.dashboard.read", "View identity platform dashboard", "Dashboard"),
        };
        db.Permissions.AddRange(permissions);

        var superAdmin = new Role(tenant.Id, "SuperAdmin", "Full system access with all permissions");
        foreach (var permission in permissions)
            superAdmin.AddPermission(permission);
        db.Roles.Add(superAdmin);
        SuperAdminRoleId = superAdmin.Id;

        var superAdmins = new Group(tenant.Id, "SuperAdmins", "Super administrator group");
        superAdmins.AddRole(superAdmin);
        db.Groups.Add(superAdmins);

        var platform = new Platform(tenant.Id, "IdentityAdmin", "Identity administration platform", PermissionMode.IdentityManaged);
        db.Platforms.Add(platform);

        var clientSecretHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(IntegrationTestConstants.AdminClientSecret)));
        var client = new Client(tenant.Id, Guid.Parse(IntegrationTestConstants.AdminClientId), clientSecretHash, "identity-platform", ClientType.Confidential, platform.Id);
        client.AddGrantType("authorization_code");
        client.AddGrantType("refresh_token");
        client.AddScope("openid");
        client.AddScope("profile");
        client.AddScope("email");
        client.AddRedirectUri(new Uri(IntegrationTestConstants.RedirectUri));
        db.Clients.Add(client);

        var passwordHash = hasher.HashPassword(IntegrationTestConstants.SuperAdminPassword);
        var email = EmailAddress.FromString(IntegrationTestConstants.SuperAdminEmail);
        var user = new User(IntegrationTestConstants.SuperAdminUserName, passwordHash, email, isSystemAccount: true);
        user.VerifyEmail(email);
        user.AddToGroup(superAdmins);
        db.Users.Add(user);
        SuperAdminUserId = user.Id;

        db.TenantMemberships.Add(new TenantMembership(tenant.Id, user.Id, isOwner: true));

        await db.SaveChangesAsync().ConfigureAwait(false);
    }
}
