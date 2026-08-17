namespace DgDevelopment.Identity.UnitTests.DeviceFlow;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.OAuth.Services;

public sealed class DeviceAuthorizationServiceTests
{
    private static readonly string[] OpenId = ["openid"];
    private static readonly string[] OpenIdProfile = ["openid", "profile"];
    private static readonly string[] OpenIdEmail = ["openid", "email"];
    private static readonly Uri DeviceUri = new("https://idp.example/device");

    private static string Hash(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static Client CreateClient(params string[] scopes)
    {
        var client = new Client("device-client", "secret-hash", "Device Client", ClientType.Confidential);
        client.AddGrantType("device_code");
        foreach (var scope in scopes)
            client.AddScope(scope);
        return client;
    }

    private sealed class FakeClientValidator(Client client) : IClientValidator
    {
        public Task<ClientValidationResult> ValidateAsync(string? clientId, string? clientSecret, string grantType, CancellationToken ct = default)
            => Task.FromResult(new ClientValidationResult(true, client, null));
    }

    private sealed class FakeDeviceCodeRepository : IDeviceCodeRepository
    {
        private readonly Dictionary<string, DeviceCode> _byDeviceCodeHash = [];
        private readonly Dictionary<string, DeviceCode> _byUserCodeHash = [];

        public IReadOnlyCollection<DeviceCode> Stored => _byDeviceCodeHash.Values.ToList();

        public Task<DeviceCode?> GetByUserCodeHashAsync(string userCodeHash, CancellationToken ct = default)
            => Task.FromResult(_byUserCodeHash.TryGetValue(userCodeHash, out var code) ? code : null);

        public Task<DeviceCode?> GetByDeviceCodeHashAsync(string deviceCodeHash, CancellationToken ct = default)
            => Task.FromResult(_byDeviceCodeHash.TryGetValue(deviceCodeHash, out var code) ? code : null);

        public Task AddAsync(DeviceCode code, CancellationToken ct = default)
        {
            _byDeviceCodeHash[code.DeviceCodeHash] = code;
            _byUserCodeHash[code.UserCodeHash] = code;
            return Task.CompletedTask;
        }

        public Task AuthorizeAsync(Guid id, Guid userId, CancellationToken ct = default)
        {
            var code = _byDeviceCodeHash.Values.FirstOrDefault(c => c.Id == id);
            code?.Authorize(userId);
            return Task.CompletedTask;
        }

        public Task RecordPollAsync(Guid id, DateTime now, CancellationToken ct = default)
        {
            var code = _byDeviceCodeHash.Values.FirstOrDefault(c => c.Id == id);
            code?.RecordPoll(now);
            return Task.CompletedTask;
        }

        public Task MarkAsUsedAsync(Guid id, CancellationToken ct = default)
        {
            var code = _byDeviceCodeHash.Values.FirstOrDefault(c => c.Id == id);
            code?.MarkUsed();
            return Task.CompletedTask;
        }

        public Task DeleteExpiredAsync(CancellationToken ct = default)
        {
            foreach (var code in _byDeviceCodeHash.Values.Where(c => c.IsExpired()).ToList())
            {
                _byDeviceCodeHash.Remove(code.DeviceCodeHash);
                _byUserCodeHash.Remove(code.UserCodeHash);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeClientRepository(Client client) : IClientRepository
    {
        public Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Client?>(client);

        public Task<Client?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
            => Task.FromResult<Client?>(client);

        public Task<IReadOnlyCollection<string>> GetAllActiveClientIdsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<string>>([]);

        public Task<IReadOnlyCollection<Uri>> GetAllActiveRedirectUrisAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyCollection<Uri>>([]);

        public Task AddAsync(Client client, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Client client, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static DeviceAuthorizationService CreateService(Client client, FakeDeviceCodeRepository repo)
        => new(new FakeClientValidator(client), repo, new FakeClientRepository(client));

    [Fact]
    public async Task IssueAsyncReturnsDeviceAndUserCodeAndPersistsHashes()
    {
        var client = CreateClient("openid", "profile");
        var repo = new FakeDeviceCodeRepository();
        var service = CreateService(client, repo);

        var response = await service.IssueAsync("device-client", "secret", OpenIdProfile, DeviceUri);

        Assert.False(string.IsNullOrWhiteSpace(response.DeviceCode));
        Assert.False(string.IsNullOrWhiteSpace(response.UserCode));
        Assert.Equal("https://idp.example/device", response.VerificationUri);
        Assert.Contains(response.UserCode, response.VerificationUriComplete, StringComparison.Ordinal);
        Assert.Equal(900, response.ExpiresIn);
        Assert.Equal(5, response.Interval);

        var stored = Assert.Single(repo.Stored);
        Assert.Equal(Hash(response.DeviceCode), stored.DeviceCodeHash);
        Assert.Equal(Hash(response.UserCode.Replace("-", string.Empty, StringComparison.Ordinal)), stored.UserCodeHash);
        Assert.Equal(OpenIdProfile, stored.GetScopes());
    }

    [Fact]
    public async Task IssueAsyncUserCodeUsesDashGroupedFormat()
    {
        var client = CreateClient("openid");
        var repo = new FakeDeviceCodeRepository();
        var service = CreateService(client, repo);

        var response = await service.IssueAsync("device-client", "secret", OpenId, DeviceUri);

        Assert.Matches(@"^[A-Z0-9]{4}-[A-Z0-9]{4}$", response.UserCode);
    }

    [Fact]
    public async Task IssueAsyncRejectsDisallowedScope()
    {
        var client = CreateClient("openid");
        var repo = new FakeDeviceCodeRepository();
        var service = CreateService(client, repo);

        var exception = await Assert.ThrowsAsync<DeviceAuthorizationException>(() =>
            service.IssueAsync("device-client", "secret", OpenIdEmail, DeviceUri));

        Assert.Equal("invalid_scope", exception.ErrorCode);
        Assert.Empty(repo.Stored);
    }

    [Fact]
    public async Task IssueAsyncRejectsClientWithoutDeviceGrant()
    {
        var client = new Client("no-device", "secret-hash", "No Device", ClientType.Confidential);
        client.AddGrantType("authorization_code");
        client.AddScope("openid");
        var repo = new FakeDeviceCodeRepository();
        var validator = new FakeClientValidatorForGrant(client);
        var service = new DeviceAuthorizationService(validator, repo, new FakeClientRepository(client));

        var exception = await Assert.ThrowsAsync<DeviceAuthorizationException>(() =>
            service.IssueAsync("no-device", "secret", OpenId, DeviceUri));

        Assert.Equal("invalid_client", exception.ErrorCode);
        Assert.Empty(repo.Stored);
    }

    [Fact]
    public async Task GetApprovalAsyncReturnsNullForUnknownCode()
    {
        var client = CreateClient("openid");
        var repo = new FakeDeviceCodeRepository();
        var service = CreateService(client, repo);
        await service.IssueAsync("device-client", "secret", OpenId, DeviceUri);

        var result = await service.GetApprovalAsync("UNKN-0WN0");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetApprovalAsyncReturnsClientNameAndScopesForKnownCode()
    {
        var client = CreateClient("openid", "profile");
        var repo = new FakeDeviceCodeRepository();
        var service = CreateService(client, repo);
        var issued = await service.IssueAsync("device-client", "secret", OpenIdProfile, DeviceUri);

        var result = await service.GetApprovalAsync(issued.UserCode);

        Assert.NotNull(result);
        Assert.Equal("Device Client", result.ClientName);
        Assert.Equal(OpenIdProfile, result.Scopes);
    }

    [Fact]
    public async Task ApproveAsyncAuthorizesDeviceCodeWithUser()
    {
        var client = CreateClient("openid");
        var repo = new FakeDeviceCodeRepository();
        var service = CreateService(client, repo);
        var issued = await service.IssueAsync("device-client", "secret", OpenId, DeviceUri);
        var userId = Guid.NewGuid();

        var approved = await service.ApproveAsync(issued.UserCode, userId);

        Assert.True(approved);
        var stored = Assert.Single(repo.Stored);
        Assert.True(stored.IsAuthorized);
        Assert.Equal(userId, stored.UserId);
    }

    [Fact]
    public async Task ApproveAsyncReturnsFalseForUnknownCode()
    {
        var client = CreateClient("openid");
        var repo = new FakeDeviceCodeRepository();
        var service = CreateService(client, repo);

        var approved = await service.ApproveAsync("UNKN-0WN0", Guid.NewGuid());

        Assert.False(approved);
    }

    [Fact]
    public async Task ApprovalNormalizesUserCodeWithOrWithoutDash()
    {
        var client = CreateClient("openid");
        var repo = new FakeDeviceCodeRepository();
        var service = CreateService(client, repo);
        var issued = await service.IssueAsync("device-client", "secret", OpenId, DeviceUri);

        var withoutDash = issued.UserCode.Replace("-", string.Empty, StringComparison.Ordinal);
        var result = await service.GetApprovalAsync(withoutDash);

        Assert.NotNull(result);
        Assert.Equal("Device Client", result.ClientName);
    }

    private sealed class FakeClientValidatorForGrant(Client client) : IClientValidator
    {
        public Task<ClientValidationResult> ValidateAsync(string? clientId, string? clientSecret, string grantType, CancellationToken ct = default)
        {
            var valid = client.GrantTypes.Any(g => g.GrantType == grantType);
            return Task.FromResult(valid
                ? new ClientValidationResult(true, client, null)
                : new ClientValidationResult(false, null, "Grant type not allowed for this client."));
        }
    }
}