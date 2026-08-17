namespace DgDevelopment.Identity.Server.UnitTests.Testing;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class TestDatabaseSmokeTests : IClassFixture<DatabaseFixture<TestDatabaseSmokeTests>>
{
    private readonly DatabaseFixture<TestDatabaseSmokeTests> _fixture;

    public TestDatabaseSmokeTests(DatabaseFixture<TestDatabaseSmokeTests> fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SeededAdminClientIsActiveWithAllGrantTypesAndScopes()
    {
        await using var context = _fixture.CreateContext();
        var client = await new ClientRepository(context)
            .GetByClientIdAsync(TestConstants.AdminClientId);

        Assert.NotNull(client);
        Assert.True(client.IsActive);
        Assert.Equal(ClientType.Confidential, client.ClientType);
        Assert.Equal(
            ["authorization_code", "client_credentials", "device_code", "refresh_token"],
            client.GrantTypes.Select(g => g.GrantType).OrderBy(g => g));
        Assert.Equal(
            ["email", "openid", "profile"],
            client.Scopes.Select(s => s.Scope).OrderBy(s => s));
        Assert.Equal(2, client.RedirectUris.Count);
        Assert.Single(client.PostLogoutRedirectUris);
    }

    [Fact]
    public async Task SeededAdminClientSecretHashMatchesKnownSecret()
    {
        await using var context = _fixture.CreateContext();
        var client = await new ClientRepository(context)
            .GetByClientIdAsync(TestConstants.AdminClientId);

        Assert.NotNull(client);
        var expectedHash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(TestConstants.AdminClientSecret)));
        Assert.Equal(expectedHash, client.ClientSecretHash);
    }

    [Fact]
    public async Task SeededSuperAdminUserExistsWithVerifiedPrimaryEmailAndGroup()
    {
        await using var context = _fixture.CreateContext();
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.Emails)
            .Include(u => u.Groups)
            .SingleAsync(u => u.Username == TestConstants.SuperAdminUserName);

        Assert.True(user.IsActive);
        Assert.True(user.IsSystemAccount);
        Assert.Equal(TestConstants.SuperAdminEmail.ToUpperInvariant(), user.PrimaryEmail?.Value);
        Assert.All(user.Emails, email => Assert.True(email.IsVerified));
        Assert.Single(user.Groups);
    }

    [Fact]
    public async Task SeededSuperAdminBelongsToSuperAdminsGroupWithSuperAdminRole()
    {
        await using var context = _fixture.CreateContext();
        var roleCount = await context.Roles
            .Where(r => r.Name == "SuperAdmin")
            .SelectMany(r => r.Permissions)
            .CountAsync();
        var group = await context.Groups
            .AsNoTracking()
            .Include(g => g.Roles)
            .SingleAsync(g => g.Name == "SuperAdmins");

        Assert.Equal(27, roleCount);
        Assert.Single(group.Roles);
    }
}