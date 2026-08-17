namespace DgDevelopment.Identity.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.UnitTests.Testing;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using Xunit;

public sealed class ClientRepositoryTests : IClassFixture<DatabaseFixture<ClientRepositoryTests>>
{
    private readonly DatabaseFixture<ClientRepositoryTests> _fixture;

    public ClientRepositoryTests(DatabaseFixture<ClientRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static Client CreateClient(string? clientId = null, string host = "one.example")
    {
        var client = new Client(clientId ?? $"test-client-{Guid.NewGuid():N}", "secret-hash", "Test Client", ClientType.Confidential);
        client.AddGrantType("authorization_code");
        client.AddGrantType("refresh_token");
        client.AddScope("openid");
        client.AddScope("profile");
        client.AddAdminConsentScope("email");
        client.AddRedirectUri(new Uri($"https://{host}/callback"));
        client.AddPostLogoutRedirectUri(new Uri($"https://{host}/"));
        return client;
    }

    [Fact]
    public async Task AddAndGetByClientIdRoundTripsOwnedCollections()
    {
        var client = CreateClient();
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        await repo.AddAsync(client);

        var stored = await repo.GetByClientIdAsync(client.ClientId);

        Assert.NotNull(stored);
        Assert.True(stored.IsActive);
        Assert.Equal("secret-hash", stored.ClientSecretHash);
        Assert.Equal(ClientType.Confidential, stored.ClientType);
        Assert.Equal(
            ["authorization_code", "refresh_token"],
            stored.GrantTypes.Select(g => g.GrantType).OrderBy(g => g));
        Assert.Equal(["openid", "profile"], stored.Scopes.Select(s => s.Scope).OrderBy(s => s));
        Assert.Equal(["email"], stored.AdminConsentScopes.Select(s => s.Scope));
        Assert.Equal(new Uri("https://one.example/callback"), Assert.Single(stored.RedirectUris).RedirectUri);
        Assert.Equal(new Uri("https://one.example/"), Assert.Single(stored.PostLogoutRedirectUris).RedirectUri);
    }

    [Fact]
    public async Task AddAndGetByIdRoundTripsOwnedCollections()
    {
        var client = CreateClient();
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        await repo.AddAsync(client);

        var stored = await repo.GetByIdAsync(client.Id);

        Assert.NotNull(stored);
        Assert.Equal(client.ClientId, stored.ClientId);
        Assert.Equal(["email"], stored.AdminConsentScopes.Select(s => s.Scope));
        Assert.Equal(
            ["authorization_code", "refresh_token"],
            stored.GrantTypes.Select(g => g.GrantType).OrderBy(g => g));
    }

    [Fact]
    public async Task GetByClientIdReturnsNullForUnknownClient()
    {
        await using var context = _fixture.CreateContext();
        var stored = await new ClientRepository(context).GetByClientIdAsync("does-not-exist");

        Assert.Null(stored);
    }

    [Fact]
    public async Task GetByIdReturnsNullForUnknownClient()
    {
        await using var context = _fixture.CreateContext();
        var stored = await new ClientRepository(context).GetByIdAsync(Guid.NewGuid());

        Assert.Null(stored);
    }

    [Fact]
    public async Task UpdateAsyncPersistsScalarChanges()
    {
        var client = CreateClient();
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        await repo.AddAsync(client);

        var tracked = await context.Clients.SingleAsync(c => c.Id == client.Id);
        tracked.SetSecret("updated-hash");
        tracked.Deactivate();
        await repo.UpdateAsync(tracked);

        var stored = await repo.GetByClientIdAsync(client.ClientId);
        Assert.NotNull(stored);
        Assert.Equal("updated-hash", stored.ClientSecretHash);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task DeleteRemovesClientAndOwnedCollections()
    {
        var client = CreateClient();
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        await repo.AddAsync(client);

        await repo.DeleteAsync(client.Id);

        Assert.Null(await repo.GetByClientIdAsync(client.ClientId));
        Assert.Equal(0, await context.Clients.CountAsync(c => c.Id == client.Id));
        Assert.Equal(0, await CountOwnedAsync(context, "ClientGrantTypes", client.Id));
        Assert.Equal(0, await CountOwnedAsync(context, "ClientScopes", client.Id));
        Assert.Equal(0, await CountOwnedAsync(context, "ClientAdminConsents", client.Id));
        Assert.Equal(0, await CountOwnedAsync(context, "ClientRedirectUris", client.Id));
        Assert.Equal(0, await CountOwnedAsync(context, "ClientPostLogoutRedirectUris", client.Id));
    }

    private static async Task<int> CountOwnedAsync(IdentityDbContext context, string tableName, Guid clientId)
    {
        var sql = tableName switch
        {
            "ClientGrantTypes" => FormattableStringFactory.Create("SELECT COUNT(*) AS [Value] FROM ClientGrantTypes WHERE ClientId = {0}", clientId),
            "ClientScopes" => FormattableStringFactory.Create("SELECT COUNT(*) AS [Value] FROM ClientScopes WHERE ClientId = {0}", clientId),
            "ClientAdminConsents" => FormattableStringFactory.Create("SELECT COUNT(*) AS [Value] FROM ClientAdminConsents WHERE ClientId = {0}", clientId),
            "ClientRedirectUris" => FormattableStringFactory.Create("SELECT COUNT(*) AS [Value] FROM ClientRedirectUris WHERE ClientId = {0}", clientId),
            "ClientPostLogoutRedirectUris" => FormattableStringFactory.Create("SELECT COUNT(*) AS [Value] FROM ClientPostLogoutRedirectUris WHERE ClientId = {0}", clientId),
            _ => throw new ArgumentOutOfRangeException(nameof(tableName)),
        };

        var count = await context.Database.SqlQuery<int>(sql).SingleAsync();
        return count;
    }

    [Fact]
    public async Task GetAllActiveClientIdsExcludesDeactivatedClients()
    {
        var active = CreateClient();
        var deactivated = CreateClient();
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        await repo.AddAsync(active);
        await repo.AddAsync(deactivated);

        var tracked = await context.Clients.SingleAsync(c => c.Id == deactivated.Id);
        tracked.Deactivate();
        await repo.UpdateAsync(tracked);

        var ids = await repo.GetAllActiveClientIdsAsync();

        Assert.Contains(active.ClientId, ids);
        Assert.DoesNotContain(deactivated.ClientId, ids);
    }

    [Fact]
    public async Task GetAllActiveRedirectUrisExcludesDeactivatedClients()
    {
        var active = CreateClient(host: "one.example");
        var deactivated = CreateClient(host: "two.example");
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        await repo.AddAsync(active);
        await repo.AddAsync(deactivated);

        var tracked = await context.Clients.SingleAsync(c => c.Id == deactivated.Id);
        tracked.Deactivate();
        await repo.UpdateAsync(tracked);

        var uris = await repo.GetAllActiveRedirectUrisAsync();

        Assert.Contains(new Uri("https://one.example/callback"), uris);
        Assert.Contains(new Uri("https://one.example/"), uris);
        Assert.DoesNotContain(new Uri("https://two.example/callback"), uris);
    }
}