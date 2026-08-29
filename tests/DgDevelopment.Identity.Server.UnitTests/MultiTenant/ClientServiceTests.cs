namespace DgDevelopment.Identity.Server.UnitTests.MultiTenant;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Application.Clients;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class ClientServiceTests : IClassFixture<DatabaseFixture<ClientServiceTests>>
{
    private readonly DatabaseFixture<ClientServiceTests> _fixture;

    public ClientServiceTests(DatabaseFixture<ClientServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static ClientService CreateService(IdentityDbContext context)
        => new(new ClientRepository(context), new PlatformRepository(context));

    private static async Task<Tenant> CreateTenantAsync(IdentityDbContext context, string name)
    {
        var tenant = new Tenant(name, Unique(name.ToUpperInvariant()));
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();
        return tenant;
    }

    private static async Task<Platform> CreatePlatformAsync(IdentityDbContext context, Guid tenantId, string name = "Platform")
    {
        var platform = new Platform(tenantId, Unique(name), "description", PermissionMode.AuthOnly);
        context.Platforms.Add(platform);
        await context.SaveChangesAsync();
        return platform;
    }

    private static string Hash(string secret)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    [Fact]
    public async Task CreateAsyncPersistsGrantTypesScopesAndRedirectUris()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, "ClientCreate");
        var service = CreateService(context);

        var (client, clientSecret) = await service.CreateAsync(
            "My Client", ClientType.Confidential, null, requireConsent: true,
            ["authorization_code", "refresh_token"], ["openid", "profile"],
            [new Uri("https://example.com/callback")], [new Uri("https://example.com/")],
            [], tenant.Id);

        var stored = await new ClientRepository(context).GetByIdAsync(client.Id);
        Assert.NotNull(stored);
        Assert.Equal(Hash(clientSecret), stored.ClientSecretHash);
        Assert.Equal(["authorization_code", "refresh_token"], stored.GrantTypes.Select(g => g.GrantType).OrderBy(g => g));
        Assert.Equal(["openid", "profile"], stored.Scopes.Select(s => s.Scope).OrderBy(s => s));
        Assert.Contains(stored.RedirectUris, r => r.RedirectUri == new Uri("https://example.com/callback"));
    }

    [Fact]
    public async Task CreateAsyncThrowsWhenPlatformBelongsToDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "ClientCreatePlatformA");
        var tenantB = await CreateTenantAsync(context, "ClientCreatePlatformB");
        var otherTenantPlatform = await CreatePlatformAsync(context, tenantB.Id);
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync("Client", ClientType.Confidential, otherTenantPlatform.Id, true, [], [], [], [], [], tenantA.Id));
    }

    [Fact]
    public async Task UpdateAsyncAddsAndRemovesGrantTypesScopesAndRedirectUris()
    {
        Guid clientId;
        Guid tenantId;
        await using (var setupContext = _fixture.CreateContext())
        {
            var tenant = await CreateTenantAsync(setupContext, "ClientUpdateReconcile");
            var service = CreateService(setupContext);
            var (client, _) = await service.CreateAsync(
                "Client", ClientType.Confidential, null, true,
                ["authorization_code"], ["openid"],
                [new Uri("https://old.example.com/callback")], [],
                [], tenant.Id);
            tenantId = tenant.Id;
            clientId = client.Id;
        }

        await using var context = _fixture.CreateContext();
        var updateService = CreateService(context);

        await updateService.UpdateAsync(
            clientId, "Client renamed", requireConsent: false, platformId: null,
            grantTypes: ["authorization_code", "refresh_token"],
            scopes: ["profile"],
            redirectUris: [new Uri("https://new.example.com/callback")],
            postLogoutRedirectUris: [],
            adminConsentScopes: [],
            tenantId, allTenants: false);

        var stored = await new ClientRepository(context).GetByIdAsync(clientId);
        Assert.NotNull(stored);
        Assert.Equal("Client renamed", stored.Name);
        Assert.False(stored.RequireConsent);
        Assert.Equal(["authorization_code", "refresh_token"], stored.GrantTypes.Select(g => g.GrantType).OrderBy(g => g));
        Assert.Equal(["profile"], stored.Scopes.Select(s => s.Scope));
        Assert.Single(stored.RedirectUris);
        Assert.Contains(stored.RedirectUris, r => r.RedirectUri == new Uri("https://new.example.com/callback"));
        Assert.DoesNotContain(stored.RedirectUris, r => r.RedirectUri == new Uri("https://old.example.com/callback"));
    }

    [Fact]
    public async Task UpdateAsyncThrowsWhenClientBelongsToDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "ClientUpdateTenantA");
        var tenantB = await CreateTenantAsync(context, "ClientUpdateTenantB");
        var service = CreateService(context);
        var (client, _) = await service.CreateAsync("Client", ClientType.Confidential, null, true, [], [], [], [], [], tenantB.Id);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(client.Id, "New name", true, null, [], [], [], [], [], tenantA.Id, allTenants: false));

        Assert.Equal("The client does not belong to the active tenant.", exception.Message);
    }

    [Fact]
    public async Task RegenerateSecretAsyncChangesTheStoredHash()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, "ClientRegenerate");
        var service = CreateService(context);
        var (client, originalSecret) = await service.CreateAsync("Client", ClientType.Confidential, null, true, [], [], [], [], [], tenant.Id);

        var newSecret = await service.RegenerateSecretAsync(client.Id, tenant.Id, allTenants: false);

        Assert.NotEqual(originalSecret, newSecret);
        var stored = await new ClientRepository(context).GetByIdAsync(client.Id);
        Assert.NotNull(stored);
        Assert.Equal(Hash(newSecret), stored.ClientSecretHash);
        Assert.NotEqual(Hash(originalSecret), stored.ClientSecretHash);
    }

    [Fact]
    public async Task SetActiveAsyncPersistsDeactivation()
    {
        await using var context = _fixture.CreateContext();
        var tenant = await CreateTenantAsync(context, "ClientDeactivate");
        var service = CreateService(context);
        var (client, _) = await service.CreateAsync("Client", ClientType.Confidential, null, true, [], [], [], [], [], tenant.Id);

        await service.SetActiveAsync(client.Id, isActive: false, tenant.Id, allTenants: false);

        var stored = await new ClientRepository(context).GetByIdAsync(client.Id);
        Assert.NotNull(stored);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task GetAsyncReturnsNullForClientInDifferentTenant()
    {
        await using var context = _fixture.CreateContext();
        var homeTenant = await CreateTenantAsync(context, "ClientGetHome");
        var otherTenant = await CreateTenantAsync(context, "ClientGetOther");
        var service = CreateService(context);
        var (client, _) = await service.CreateAsync("Client", ClientType.Confidential, null, true, [], [], [], [], [], homeTenant.Id);

        var result = await service.GetAsync(client.Id, otherTenant.Id, allTenants: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPagedAsyncOnlyReturnsClientsOfTheActiveTenantUnlessAllTenants()
    {
        await using var context = _fixture.CreateContext();
        var tenantA = await CreateTenantAsync(context, "ClientPagedA");
        var tenantB = await CreateTenantAsync(context, "ClientPagedB");
        var service = CreateService(context);
        var (clientA, _) = await service.CreateAsync("Client A", ClientType.Confidential, null, true, [], [], [], [], [], tenantA.Id);
        var (clientB, _) = await service.CreateAsync("Client B", ClientType.Confidential, null, true, [], [], [], [], [], tenantB.Id);

        var scopedResult = await service.GetPagedAsync(null, 1, 100, tenantA.Id, allTenants: false);
        var globalResult = await service.GetPagedAsync(null, 1, 1000, tenantA.Id, allTenants: true);

        Assert.Contains(scopedResult.Items, c => c.Id == clientA.Id);
        Assert.DoesNotContain(scopedResult.Items, c => c.Id == clientB.Id);
        Assert.Contains(globalResult.Items, c => c.Id == clientA.Id);
        Assert.Contains(globalResult.Items, c => c.Id == clientB.Id);
    }
}
