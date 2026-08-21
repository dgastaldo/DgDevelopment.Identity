namespace DgDevelopment.Identity.Server.UnitTests.Consent;

using DgDevelopment.Identity.Application.Consent;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class ConsentServiceTests : IClassFixture<DatabaseFixture<ConsentServiceTests>>
{
    private readonly DatabaseFixture<ConsentServiceTests> _fixture;

    public ConsentServiceTests(DatabaseFixture<ConsentServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly string[] OpenId = ["openid"];
    private static readonly string[] OpenIdProfile = ["openid", "profile"];
    private static readonly string[] OpenIdProfileEmail = ["openid", "profile", "email"];
    private static readonly string[] ProfileEmail = ["profile", "email"];
    private static readonly string[] OpenIdEmail = ["openid", "email"];

    private static Client CreateClient(bool requireConsent = true, params string[] adminScopes)
    {
        var client = new Client(TenantId, Guid.NewGuid(), "secret-hash", "Test Client", ClientType.Confidential, requireConsent: requireConsent);
        foreach (var scope in adminScopes)
            client.AddAdminConsentScope(scope);
        return client;
    }

    private static ConsentService CreateService(IdentityDbContext context)
        => new(new UserConsentRepository(context));

    [Fact]
    public async Task NeedsConsentMasterDisabledReturnsFalse()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var client = CreateClient(requireConsent: false);

        var result = service.NeedsConsent(client, stored: null, OpenIdProfile, Now);

        Assert.False(result);
    }

    [Fact]
    public async Task GetScopesRequiringUserConsentAdminApprovedCoversAllReturnsEmpty()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var client = CreateClient(adminScopes: OpenIdProfile);

        var result = service.GetScopesRequiringUserConsent(client, OpenIdProfile);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetScopesRequiringUserConsentMixedScopesReturnsOnlyUserScopes()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var client = CreateClient(adminScopes: "profile");

        var result = service.GetScopesRequiringUserConsent(client, OpenIdProfileEmail);

        Assert.Equal(OpenIdEmail, result);
    }

    [Fact]
    public async Task NeedsConsentAllAdminApprovedReturnsFalseEvenWithoutStoredConsent()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var client = CreateClient(adminScopes: OpenIdProfileEmail);

        var result = service.NeedsConsent(client, stored: null, OpenIdProfileEmail, Now);

        Assert.False(result);
    }

    [Fact]
    public async Task NeedsConsentStoredValidAndCoveringReturnsFalse()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var client = CreateClient(adminScopes: "email");
        var stored = new UserConsent(TenantId, Guid.NewGuid(), client.Id, OpenIdProfile, Now.AddDays(30));

        var result = service.NeedsConsent(client, stored, OpenIdProfileEmail, Now);

        Assert.False(result);
    }

    [Fact]
    public async Task NeedsConsentStoredMissingNewScopeReturnsTrue()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var client = CreateClient();
        var stored = new UserConsent(TenantId, Guid.NewGuid(), client.Id, OpenId, Now.AddDays(30));

        var result = service.NeedsConsent(client, stored, OpenIdProfile, Now);

        Assert.True(result);
    }

    [Fact]
    public async Task NeedsConsentNoStoredConsentReturnsTrue()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var client = CreateClient();

        var result = service.NeedsConsent(client, stored: null, OpenId, Now);

        Assert.True(result);
    }

    [Fact]
    public async Task NeedsConsentStoredExpiredReturnsTrue()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var client = CreateClient();
        var stored = new UserConsent(TenantId, Guid.NewGuid(), client.Id, OpenId, Now.AddDays(-1));

        var result = service.NeedsConsent(client, stored, OpenId, Now);

        Assert.True(result);
    }

    [Fact]
    public async Task RecordConsentAsyncMergesScopesAndSlidesExpiry()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        await using var context = _fixture.CreateContext();
        var repo = new UserConsentRepository(context);
        var service = new ConsentService(repo);
        var client = new Client(tenantId, Guid.NewGuid(), "secret-hash", "Test Client", ClientType.Confidential);
        var existing = new UserConsent(tenantId, userId, client.Id, OpenId, Now.AddDays(10));
        await repo.AddOrUpdateAsync(existing);
        var originalExpiry = existing.ExpiresAt;

        var lifetime = TimeSpan.FromDays(180);
        await service.RecordConsentAsync(userId, client, ProfileEmail, lifetime);

        var saved = await repo.GetAsync(userId, client.Id);
        Assert.NotNull(saved);
        Assert.Equal(OpenIdProfileEmail, saved.GetScopes());
        Assert.True(saved.ExpiresAt > originalExpiry);
        Assert.True(saved.ExpiresAt > Now.AddDays(179));
        Assert.True(saved.ExpiresAt <= Now.AddDays(181));
    }

    [Fact]
    public async Task RecordConsentAsyncNewRecordSavesWithSlidingExpiry()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        await using var context = _fixture.CreateContext();
        var repo = new UserConsentRepository(context);
        var service = new ConsentService(repo);
        var client = new Client(tenantId, Guid.NewGuid(), "secret-hash", "Test Client", ClientType.Confidential);

        await service.RecordConsentAsync(userId, client, OpenId, TimeSpan.FromDays(180));

        var saved = await repo.GetAsync(userId, client.Id);
        Assert.NotNull(saved);
        Assert.Equal(OpenId, saved.GetScopes());
        Assert.True(saved.ExpiresAt > Now.AddDays(179));
        Assert.True(saved.ExpiresAt <= Now.AddDays(181));
    }
}
