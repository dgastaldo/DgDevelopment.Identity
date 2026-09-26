namespace DgDevelopment.Identity.Server.UnitTests.Infrastructure;

using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class DeviceCodeRepositoryTests : IClassFixture<DatabaseFixture<DeviceCodeRepositoryTests>>
{
    private readonly DatabaseFixture<DeviceCodeRepositoryTests> _fixture;

    public DeviceCodeRepositoryTests(DatabaseFixture<DeviceCodeRepositoryTests> fixture)
    {
        _fixture = fixture;
    }

    private static string UniqueHash(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task AddAndGetByDeviceCodeHashRoundTrips()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var code = new DeviceCode(UniqueHash("device"), UniqueHash("user"), clientId, ["openid", "profile"]);
        await using var context = _fixture.CreateContext();
        var repo = new DeviceCodeRepository(context);
        await repo.AddAsync(code);

        var stored = await repo.GetByDeviceCodeHashAsync(code.DeviceCodeHash);

        Assert.NotNull(stored);
        Assert.Equal(code.Id, stored.Id);
        Assert.Equal(clientId, stored.ClientId);
        Assert.Equal(["openid", "profile"], stored.GetScopes());
        Assert.False(stored.IsAuthorized);
        Assert.False(stored.IsUsed);
    }

    [Fact]
    public async Task AddAndGetByUserCodeHashRoundTrips()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var code = new DeviceCode(UniqueHash("device"), UniqueHash("user"), clientId, ["openid"]);
        await using var context = _fixture.CreateContext();
        var repo = new DeviceCodeRepository(context);
        await repo.AddAsync(code);

        var stored = await repo.GetByUserCodeHashAsync(code.UserCodeHash);

        Assert.NotNull(stored);
        Assert.Equal(code.Id, stored.Id);
    }

    [Fact]
    public async Task GetByHashReturnsNullForUnknownCode()
    {
        await using var context = _fixture.CreateContext();
        var repo = new DeviceCodeRepository(context);

        Assert.Null(await repo.GetByDeviceCodeHashAsync("unknown-device-hash"));
        Assert.Null(await repo.GetByUserCodeHashAsync("unknown-user-hash"));
    }

    [Fact]
    public async Task AuthorizePersistsUserAssociation()
    {
        var tenantId = await _fixture.GetSeededTenantIdAsync();
        var clientId = await _fixture.GetSeededClientIdAsync();
        var userId = await _fixture.GetSeededUserIdAsync();
        var code = new DeviceCode(UniqueHash("device"), UniqueHash("user"), clientId, ["openid"]);
        await using var context = _fixture.CreateContext();
        var repo = new DeviceCodeRepository(context);
        await repo.AddAsync(code);

        await repo.AuthorizeAsync(code.Id, tenantId, userId);

        var stored = await repo.GetByDeviceCodeHashAsync(code.DeviceCodeHash);
        Assert.NotNull(stored);
        Assert.True(stored.IsAuthorized);
        Assert.Equal(tenantId, stored.TenantId);
        Assert.Equal(userId, stored.UserId);
    }

    [Fact]
    public async Task RecordPollPersistsTimestamp()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var code = new DeviceCode(UniqueHash("device"), UniqueHash("user"), clientId, ["openid"]);
        await using var context = _fixture.CreateContext();
        var repo = new DeviceCodeRepository(context);
        await repo.AddAsync(code);
        var polledAt = DateTime.UtcNow;

        await repo.RecordPollAsync(code.Id, polledAt);

        var stored = await repo.GetByDeviceCodeHashAsync(code.DeviceCodeHash);
        Assert.NotNull(stored);
        Assert.Equal(polledAt, stored.LastPolledAt);
    }

    [Fact]
    public async Task MarkAsUsedPersistsFlag()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        var code = new DeviceCode(UniqueHash("device"), UniqueHash("user"), clientId, ["openid"]);
        await using var context = _fixture.CreateContext();
        var repo = new DeviceCodeRepository(context);
        await repo.AddAsync(code);

        await repo.MarkAsUsedAsync(code.Id);

        var stored = await repo.GetByDeviceCodeHashAsync(code.DeviceCodeHash);
        Assert.NotNull(stored);
        Assert.True(stored.IsUsed);
    }

    [Fact]
    public async Task DeleteExpiredRemovesOnlyExpiredCodes()
    {
        var clientId = await _fixture.GetSeededClientIdAsync();
        await using var context = _fixture.CreateContext();
        var repo = new DeviceCodeRepository(context);
        // A lifetime of exactly 0 sets ExpiresAt to "now" at construction time, which raced against
        // DeleteExpiredAsync's own `ExpiresAt < DateTime.UtcNow` a few milliseconds later - close
        // enough to the datetime2 column's rounding that the comparison was occasionally unreliable.
        // A clearly-past lifetime removes the race instead of relying on timing.
        var expired = new DeviceCode(UniqueHash("expired"), UniqueHash("expired"), clientId, ["openid"], lifetimeSeconds: -60);
        var valid = new DeviceCode(UniqueHash("valid"), UniqueHash("valid"), clientId, ["openid"]);
        await repo.AddAsync(expired);
        await repo.AddAsync(valid);

        await repo.DeleteExpiredAsync();

        Assert.Null(await repo.GetByDeviceCodeHashAsync(expired.DeviceCodeHash));
        Assert.NotNull(await repo.GetByDeviceCodeHashAsync(valid.DeviceCodeHash));
    }
}