namespace DgDevelopment.Identity.IntegrationTests;

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Application.Tenants;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    public IdentityWebApplicationFactory Factory { get; } = new();

    public Guid DefaultTenantId { get; private set; }
    public Guid DefaultPlatformId { get; private set; }
    public Guid SuperAdminUserId { get; private set; }
    public Guid SuperAdminRoleId { get; private set; }
    public Guid SecondTenantId { get; private set; }
    public Guid SecondTenantRoleId { get; private set; }
    public string AccessToken { get; private set; } = string.Empty;

    private const string CustomerTenantClientSecret = "customer-tenant-test-client-secret-0123456789";
    private const string CustomerTenantPassword = "Customer-Tenant-Test-Password!2026";
    private Guid _customerTenantClientId;
    private string _customerTenantAccessToken = string.Empty;

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
        // no credential files written to disk, no AppHost user-secrets sync attempted). This also
        // starts ClientIdCache's 5-minute refresh loop, which is why every OAuth client that a
        // test needs to log in as must exist in the DB *before* this first CreateClient() call -
        // a client added afterward isn't in the cache yet and gets "invalid_token: audience
        // invalid" on every request until the next background refresh.
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

        // A fresh client (fresh cookie jar) - reusing loginClient here would still carry the
        // superadmin's authentication cookie from the login above, so /connect/authorize would
        // skip straight past the login form instead of showing it for this different user.
        var customerLoginClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri(IdentityWebApplicationFactory.IssuerBaseAddress),
        });
        _customerTenantAccessToken = await new OidcTestClient(customerLoginClient).LoginAndGetAccessTokenAsync(
            "customer.admin", CustomerTenantPassword, _customerTenantClientId.ToString(), CustomerTenantClientSecret,
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
        // Must match the login client's BaseAddress (IssuerBaseAddress): endpoints that re-validate
        // the bearer token themselves (e.g. /connect/userinfo) compute the expected issuer from the
        // current request's own scheme+host, so calling through a different scheme than the one the
        // token was minted under fails issuer validation even though the token itself is valid.
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(IdentityWebApplicationFactory.IssuerBaseAddress),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        return client;
    }

    /// <summary>
    /// An authenticated client for a fully provisioned, login-capable *customer* tenant (its own
    /// Client + User, real OIDC login) whose SuperAdmin holds every permission in their own
    /// catalog - including the IsGlobal-flagged ones - but is NOT the platform tenant. Exercises
    /// the real distinction IsGlobalAdministratorAsync draws: holding the permission alone isn't
    /// enough. Provisioned in SeedAsync (before the host starts) rather than lazily here - see
    /// the ClientIdCache note in InitializeAsync for why that ordering matters.
    /// </summary>
    public HttpClient CreateAuthenticatedClientForCustomerTenantAsync()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(IdentityWebApplicationFactory.IssuerBaseAddress),
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _customerTenantAccessToken);
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
        var platform = new Platform(tenant.Id, "Other Tenant Platform", "cross-tenant test platform", PermissionMode.AuthOnly);
        context.Platforms.Add(platform);
        var role = new Role(tenant.Id, platform.Id, "Other Tenant Role", "cross-tenant test role");
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
            DefaultPlatformId = existingRole.PlatformId;
            SuperAdminUserId = await db.Users.Where(u => u.Username == IntegrationTestConstants.SuperAdminUserName).Select(u => u.Id).SingleAsync().ConfigureAwait(false);
        }
        else
        {
            var hasher = new FakePasswordHasher();

            // Reuses the real provisioning service (Tenant + IdentityAdmin platform + full permission
            // catalog + SuperAdmin role + SuperAdmins group) instead of hand-duplicating that seed logic -
            // this is exactly what DbSeeder itself now calls for the real bootstrap tenant.
            var provisioning = new TenantProvisioningService(
                new TenantRepository(db), new PlatformRepository(db), new PermissionRepository(db),
                new RoleRepository(db), new GroupRepository(db));
            var provisioned = await provisioning.ProvisionAsync("Identity Tenant", IntegrationTestConstants.DefaultTenantSlug, isPlatformTenant: true).ConfigureAwait(false);

            DefaultTenantId = provisioned.Tenant.Id;
            DefaultPlatformId = provisioned.Platform.Id;
            SuperAdminRoleId = provisioned.SuperAdminRole.Id;

            var clientSecretHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(IntegrationTestConstants.AdminClientSecret)));
            var client = new Client(provisioned.Tenant.Id, Guid.Parse(IntegrationTestConstants.AdminClientId), clientSecretHash, "identity-platform", ClientType.Confidential, provisioned.Platform.Id);
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
            user.AddToGroup(provisioned.SuperAdminsGroup);
            db.Users.Add(user);
            SuperAdminUserId = user.Id;

            db.TenantMemberships.Add(new TenantMembership(provisioned.Tenant.Id, user.Id, isOwner: true));

            await db.SaveChangesAsync().ConfigureAwait(false);
        }

        await SeedCustomerTenantAsync(db).ConfigureAwait(false);
    }

    /// <summary>
    /// A second, non-platform tenant with its own login-capable SuperAdmin, seeded here (before
    /// host startup) rather than lazily from a test - see the ClientIdCache note in InitializeAsync.
    /// </summary>
    private async Task SeedCustomerTenantAsync(IdentityDbContext db)
    {
        if (await db.Tenants.AnyAsync(t => t.Slug == "customer-tenant").ConfigureAwait(false))
            return;

        var provisioning = new TenantProvisioningService(
            new TenantRepository(db), new PlatformRepository(db), new PermissionRepository(db),
            new RoleRepository(db), new GroupRepository(db));
        var provisioned = await provisioning.ProvisionAsync("Customer Tenant", "customer-tenant").ConfigureAwait(false);

        _customerTenantClientId = Guid.NewGuid();
        var clientSecretHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(CustomerTenantClientSecret)));
        var client = new Client(provisioned.Tenant.Id, _customerTenantClientId, clientSecretHash, "customer-admin-client", ClientType.Confidential, provisioned.Platform.Id);
        client.AddGrantType("authorization_code");
        client.AddGrantType("refresh_token");
        client.AddScope("openid");
        client.AddScope("profile");
        client.AddScope("email");
        client.AddRedirectUri(new Uri(IntegrationTestConstants.RedirectUri));
        db.Clients.Add(client);

        var hasher = new FakePasswordHasher();
        var passwordHash = hasher.HashPassword(CustomerTenantPassword);
        var email = EmailAddress.FromString("customer.admin@dgdevelopment.it");
        var user = new User("customer.admin", passwordHash, email, isSystemAccount: false);
        user.VerifyEmail(email);
        user.AddToGroup(provisioned.SuperAdminsGroup);
        db.Users.Add(user);
        db.TenantMemberships.Add(new TenantMembership(provisioned.Tenant.Id, user.Id, isOwner: true));

        await db.SaveChangesAsync().ConfigureAwait(false);
    }
}
