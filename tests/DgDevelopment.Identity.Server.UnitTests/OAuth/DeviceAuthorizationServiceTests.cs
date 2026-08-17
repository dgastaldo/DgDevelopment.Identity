namespace DgDevelopment.Identity.Server.UnitTests.OAuth;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.OAuth.Services;
using DgDevelopment.Identity.Server.UnitTests.Testing;
using Xunit;

public sealed class DeviceAuthorizationServiceTests : IClassFixture<DatabaseFixture<DeviceAuthorizationServiceTests>>
{
    private readonly DatabaseFixture<DeviceAuthorizationServiceTests> _fixture;

    public DeviceAuthorizationServiceTests(DatabaseFixture<DeviceAuthorizationServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static readonly Uri DeviceUri = new("https://idp.example/device");

    private static string Hash(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static DeviceAuthorizationService CreateService(IdentityDbContext context)
        => new(
            new ClientValidator(new ClientRepository(context)),
            new DeviceCodeRepository(context),
            new ClientRepository(context));

    [Fact]
    public async Task IssueAsyncReturnsDeviceAndUserCodeAndPersistsHashes()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var repo = new DeviceCodeRepository(context);

        var response = await service.IssueAsync(TestConstants.AdminClientId, TestConstants.AdminClientSecret, ["openid", "profile"], DeviceUri);

        Assert.False(string.IsNullOrWhiteSpace(response.DeviceCode));
        Assert.False(string.IsNullOrWhiteSpace(response.UserCode));
        Assert.Equal("https://idp.example/device", response.VerificationUri);
        Assert.Contains(response.UserCode, response.VerificationUriComplete, StringComparison.Ordinal);
        Assert.Equal(900, response.ExpiresIn);
        Assert.Equal(5, response.Interval);

        var stored = await repo.GetByDeviceCodeHashAsync(Hash(response.DeviceCode));
        Assert.NotNull(stored);
        Assert.Equal(Hash(response.UserCode.Replace("-", string.Empty, StringComparison.Ordinal)), stored.UserCodeHash);
        Assert.Equal(["openid", "profile"], stored.GetScopes());
    }

    [Fact]
    public async Task IssueAsyncUserCodeUsesDashGroupedFormat()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var response = await service.IssueAsync(TestConstants.AdminClientId, TestConstants.AdminClientSecret, ["openid"], DeviceUri);

        Assert.Matches(@"^[A-Z0-9]{4}-[A-Z0-9]{4}$", response.UserCode);
    }

    [Fact]
    public async Task IssueAsyncRejectsDisallowedScope()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<DeviceAuthorizationException>(() =>
            service.IssueAsync(TestConstants.AdminClientId, TestConstants.AdminClientSecret, ["admin"], DeviceUri));

        Assert.Equal("invalid_scope", exception.ErrorCode);
    }

    [Fact]
    public async Task IssueAsyncRejectsInvalidSecret()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<DeviceAuthorizationException>(() =>
            service.IssueAsync(TestConstants.AdminClientId, "wrong-secret", ["openid"], DeviceUri));

        Assert.Equal("invalid_client", exception.ErrorCode);
    }

    [Fact]
    public async Task IssueAsyncRejectsClientWithoutDeviceGrant()
    {
        await using var context = _fixture.CreateContext();
        var repo = new ClientRepository(context);
        var client = new Client($"no-device-{Guid.NewGuid():N}", Hash("secret"), "No Device", ClientType.Confidential);
        client.AddGrantType("authorization_code");
        await repo.AddAsync(client);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<DeviceAuthorizationException>(() =>
            service.IssueAsync(client.ClientId, "secret", ["openid"], DeviceUri));

        Assert.Equal("invalid_client", exception.ErrorCode);
    }

    [Fact]
    public async Task GetApprovalAsyncReturnsNullForUnknownCode()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var result = await service.GetApprovalAsync("UNKN-0WN0");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetApprovalAsyncReturnsClientNameAndScopesForKnownCode()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var issued = await service.IssueAsync(TestConstants.AdminClientId, TestConstants.AdminClientSecret, ["openid", "profile"], DeviceUri);

        var result = await service.GetApprovalAsync(issued.UserCode);

        Assert.NotNull(result);
        Assert.Equal("Admin UI", result.ClientName);
        Assert.Equal(["openid", "profile"], result.Scopes);
    }

    [Fact]
    public async Task ApproveAsyncAuthorizesDeviceCodeWithUser()
    {
        var userId = await _fixture.GetSeededUserIdAsync();
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var repo = new DeviceCodeRepository(context);
        var issued = await service.IssueAsync(TestConstants.AdminClientId, TestConstants.AdminClientSecret, ["openid"], DeviceUri);

        var approved = await service.ApproveAsync(issued.UserCode, userId);

        Assert.True(approved);
        var stored = await repo.GetByDeviceCodeHashAsync(Hash(issued.DeviceCode));
        Assert.NotNull(stored);
        Assert.True(stored.IsAuthorized);
        Assert.Equal(userId, stored.UserId);
    }

    [Fact]
    public async Task ApproveAsyncReturnsFalseForUnknownCode()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var approved = await service.ApproveAsync("UNKN-0WN0", Guid.NewGuid());

        Assert.False(approved);
    }

    [Fact]
    public async Task ApprovalNormalizesUserCodeWithOrWithoutDash()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        var issued = await service.IssueAsync(TestConstants.AdminClientId, TestConstants.AdminClientSecret, ["openid"], DeviceUri);

        var withoutDash = issued.UserCode.Replace("-", string.Empty, StringComparison.Ordinal);
        var result = await service.GetApprovalAsync(withoutDash);

        Assert.NotNull(result);
        Assert.Equal("Admin UI", result.ClientName);
    }
}