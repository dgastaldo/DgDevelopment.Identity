namespace DgDevelopment.Identity.UnitTests.Consent;

using DgDevelopment.Identity.Application.Consent;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;

public sealed class ConsentServiceTests
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly string[] OpenId = ["openid"];
    private static readonly string[] OpenIdProfile = ["openid", "profile"];
    private static readonly string[] OpenIdProfileEmail = ["openid", "profile", "email"];
    private static readonly string[] ProfileEmail = ["profile", "email"];
    private static readonly string[] OpenIdEmail = ["openid", "email"];

    private static Client CreateClient(bool requireConsent = true, params string[] adminScopes)
    {
        var client = new Client("test-client", "secret-hash", "Test Client", ClientType.Confidential, requireConsent: requireConsent);
        foreach (var scope in adminScopes)
            client.AddAdminConsentScope(scope);
        return client;
    }

    [Fact]
    public void NeedsConsentMasterDisabledReturnsFalse()
    {
        var service = new ConsentService(new FakeUserConsentRepository());
        var client = CreateClient(requireConsent: false);

        var result = service.NeedsConsent(client, stored: null, OpenIdProfile, Now);

        Assert.False(result);
    }

    [Fact]
    public void GetScopesRequiringUserConsentAdminApprovedCoversAllReturnsEmpty()
    {
        var service = new ConsentService(new FakeUserConsentRepository());
        var client = CreateClient(adminScopes: OpenIdProfile);

        var result = service.GetScopesRequiringUserConsent(client, OpenIdProfile);

        Assert.Empty(result);
    }

    [Fact]
    public void GetScopesRequiringUserConsentMixedScopesReturnsOnlyUserScopes()
    {
        var service = new ConsentService(new FakeUserConsentRepository());
        var client = CreateClient(adminScopes: "profile");

        var result = service.GetScopesRequiringUserConsent(client, OpenIdProfileEmail);

        Assert.Equal(OpenIdEmail, result);
    }

    [Fact]
    public void NeedsConsentAllAdminApprovedReturnsFalseEvenWithoutStoredConsent()
    {
        var service = new ConsentService(new FakeUserConsentRepository());
        var client = CreateClient(adminScopes: OpenIdProfileEmail);

        var result = service.NeedsConsent(client, stored: null, OpenIdProfileEmail, Now);

        Assert.False(result);
    }

    [Fact]
    public void NeedsConsentStoredValidAndCoveringReturnsFalse()
    {
        var service = new ConsentService(new FakeUserConsentRepository());
        var client = CreateClient(adminScopes: "email");
        var stored = new UserConsent(Guid.NewGuid(), client.Id, OpenIdProfile, Now.AddDays(30));

        var result = service.NeedsConsent(client, stored, OpenIdProfileEmail, Now);

        Assert.False(result);
    }

    [Fact]
    public void NeedsConsentStoredMissingNewScopeReturnsTrue()
    {
        var service = new ConsentService(new FakeUserConsentRepository());
        var client = CreateClient();
        var stored = new UserConsent(Guid.NewGuid(), client.Id, OpenId, Now.AddDays(30));

        var result = service.NeedsConsent(client, stored, OpenIdProfile, Now);

        Assert.True(result);
    }

    [Fact]
    public void NeedsConsentNoStoredConsentReturnsTrue()
    {
        var service = new ConsentService(new FakeUserConsentRepository());
        var client = CreateClient();

        var result = service.NeedsConsent(client, stored: null, OpenId, Now);

        Assert.True(result);
    }

    [Fact]
    public void NeedsConsentStoredExpiredReturnsTrue()
    {
        var service = new ConsentService(new FakeUserConsentRepository());
        var client = CreateClient();
        var stored = new UserConsent(Guid.NewGuid(), client.Id, OpenId, Now.AddDays(-1));

        var result = service.NeedsConsent(client, stored, OpenId, Now);

        Assert.True(result);
    }

    [Fact]
    public async Task RecordConsentAsyncMergesScopesAndSlidesExpiry()
    {
        var repo = new FakeUserConsentRepository();
        var service = new ConsentService(repo);
        var client = CreateClient();
        var userId = Guid.NewGuid();
        var existing = new UserConsent(userId, client.Id, OpenId, Now.AddDays(10));
        await repo.AddOrUpdateAsync(existing);

        var lifetime = TimeSpan.FromDays(180);
        await service.RecordConsentAsync(userId, client.Id, ProfileEmail, lifetime);

        var saved = repo.LastSaved;
        Assert.NotNull(saved);
        Assert.Equal(OpenIdProfileEmail, saved.GetScopes());
        Assert.True(saved.ExpiresAt > existing.ExpiresAt);
        Assert.True(saved.ExpiresAt > Now.AddDays(179));
        Assert.True(saved.ExpiresAt <= Now.AddDays(181));
    }

    [Fact]
    public async Task RecordConsentAsyncNewRecordSavesWithSlidingExpiry()
    {
        var repo = new FakeUserConsentRepository();
        var service = new ConsentService(repo);
        var client = CreateClient();
        var userId = Guid.NewGuid();

        await service.RecordConsentAsync(userId, client.Id, OpenId, TimeSpan.FromDays(180));

        var saved = repo.LastSaved;
        Assert.NotNull(saved);
        Assert.Equal(OpenId, saved.GetScopes());
        Assert.True(saved.ExpiresAt > Now.AddDays(179));
        Assert.True(saved.ExpiresAt <= Now.AddDays(181));
    }

    private sealed class FakeUserConsentRepository : IUserConsentRepository
    {
        private readonly Dictionary<(Guid UserId, Guid ClientId), UserConsent> _consents = [];
        private readonly List<UserConsent> _history = [];

        public UserConsent? LastSaved => _history.Count > 0 ? _history[^1] : null;

        public Task<UserConsent?> GetAsync(Guid userId, Guid clientId, CancellationToken ct = default)
            => Task.FromResult(_consents.TryGetValue((userId, clientId), out var consent) ? consent : null);

        public Task<IReadOnlyCollection<UserConsent>> GetByUserAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<UserConsent>>(
                _consents.Where(kv => kv.Key.UserId == userId).Select(kv => kv.Value).ToList());

        public Task AddOrUpdateAsync(UserConsent consent, CancellationToken ct = default)
        {
            _consents[(consent.UserId, consent.ClientId)] = consent;
            _history.Add(consent);
            return Task.CompletedTask;
        }

        public Task RevokeAsync(Guid userId, Guid clientId, CancellationToken ct = default)
        {
            _consents.Remove((userId, clientId));
            return Task.CompletedTask;
        }

        public Task DeleteExpiredAsync(CancellationToken ct = default)
        {
            foreach (var result in _consents.Where(kv => kv.Value.IsExpired()).Select(kv => kv.Key).ToList())
                _consents.Remove(result);
            return Task.CompletedTask;
        }
    }
}