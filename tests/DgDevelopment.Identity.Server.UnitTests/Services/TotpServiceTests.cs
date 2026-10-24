namespace DgDevelopment.Identity.Server.UnitTests.Services;

using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Services;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;

public sealed class TotpServiceTests : IClassFixture<DatabaseFixture<TotpServiceTests>>
{
    private readonly DatabaseFixture<TotpServiceTests> _fixture;

    public TotpServiceTests(DatabaseFixture<TotpServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private async Task<User> CreateUserAsync()
    {
        await using var context = _fixture.CreateContext();
        var user = new User($"totp-{Guid.NewGuid():N}", "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@dgdevelopment.it"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<TotpEnrollment> EnrollAsync(User user)
    {
        await using var context = _fixture.CreateContext();
        var service = new TotpService(new TotpSecretRepository(context), new FakeSecretProtector());
        return await service.EnrollAsync(user.Id, user.Username);
    }

    private async Task<BackupCodeResult> EnableAsync(User user, string code)
    {
        await using var context = _fixture.CreateContext();
        var service = new TotpService(new TotpSecretRepository(context), new FakeSecretProtector());
        return await service.EnableAsync(user.Id, code);
    }

    private async Task<bool> IsEnabledAsync(User user)
    {
        await using var context = _fixture.CreateContext();
        var service = new TotpService(new TotpSecretRepository(context), new FakeSecretProtector());
        return await service.IsEnabledAsync(user.Id);
    }

    [Fact]
    public async Task EnrollAsyncGeneratesSecretAndProvisioningUri()
    {
        var user = await CreateUserAsync();

        var enrollment = await EnrollAsync(user);

        Assert.False(string.IsNullOrWhiteSpace(enrollment.SecretKey));
        Assert.Equal("otpauth", enrollment.ProvisioningUri.Scheme);
        Assert.Contains("secret=", enrollment.ProvisioningUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IsEnabledAsyncReturnsFalseInitiallyAndTrueAfterEnable()
    {
        var user = await CreateUserAsync();

        Assert.False(await IsEnabledAsync(user));
        await EnrollAsync(user);
        Assert.False(await IsEnabledAsync(user));
    }

    [Fact]
    public async Task EnableAsyncReturnsBackupCodesAndEnablesTotp()
    {
        var user = await CreateUserAsync();
        var enrollment = await EnrollAsync(user);
        var code = TotpGenerator.ComputeCode(enrollment.SecretKey, TotpGenerator.GetCurrentTimeStep());

        var result = await EnableAsync(user, code);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Codes);
        Assert.Equal(10, result.Codes!.Count);
        Assert.True(await IsEnabledAsync(user));
    }

    [Fact]
    public async Task EnableAsyncRejectsInvalidCode()
    {
        var user = await CreateUserAsync();
        await EnrollAsync(user);

        var result = await EnableAsync(user, "000000");

        Assert.False(result.IsValid);
        Assert.False(await IsEnabledAsync(user));
    }

    [Fact]
    public async Task VerifyAsyncAcceptsGeneratedCode()
    {
        var user = await CreateUserAsync();
        var enrollment = await EnrollAsync(user);
        var code = TotpGenerator.ComputeCode(enrollment.SecretKey, TotpGenerator.GetCurrentTimeStep());
        await EnableAsync(user, code);

        await using var context = _fixture.CreateContext();
        var service = new TotpService(new TotpSecretRepository(context), new FakeSecretProtector());
        var verifyCode = TotpGenerator.ComputeCode(enrollment.SecretKey, TotpGenerator.GetCurrentTimeStep());

        Assert.True(await service.VerifyAsync(user.Id, verifyCode));
    }

    [Fact]
    public async Task VerifyAsyncAcceptsBackupCodeOnce()
    {
        var user = await CreateUserAsync();
        var enrollment = await EnrollAsync(user);
        var code = TotpGenerator.ComputeCode(enrollment.SecretKey, TotpGenerator.GetCurrentTimeStep());
        var result = await EnableAsync(user, code);
        var backup = result.Codes!.First();

        await using var context = _fixture.CreateContext();
        var service = new TotpService(new TotpSecretRepository(context), new FakeSecretProtector());

        Assert.True(await service.VerifyAsync(user.Id, backup));
        Assert.False(await service.VerifyAsync(user.Id, backup));
    }

    [Fact]
    public async Task VerifyAsyncRejectsWhenDisabled()
    {
        var user = await CreateUserAsync();

        await using var context = _fixture.CreateContext();
        var service = new TotpService(new TotpSecretRepository(context), new FakeSecretProtector());

        Assert.False(await service.VerifyAsync(user.Id, "123456"));
    }

    [Fact]
    public async Task RegenerateBackupCodesReplacesExistingCodes()
    {
        var user = await CreateUserAsync();
        var enrollment = await EnrollAsync(user);
        var code = TotpGenerator.ComputeCode(enrollment.SecretKey, TotpGenerator.GetCurrentTimeStep());
        var initial = await EnableAsync(user, code);

        await using var context = _fixture.CreateContext();
        var service = new TotpService(new TotpSecretRepository(context), new FakeSecretProtector());
        var regenerated = await service.RegenerateBackupCodesAsync(user.Id);

        Assert.True(regenerated.IsValid);
        Assert.Equal(initial.Codes!.Count, regenerated.Codes!.Count);
        Assert.NotEqual(initial.Codes.First(), regenerated.Codes.First());
    }

    [Fact]
    public async Task DisableAsyncDisablesTotp()
    {
        var user = await CreateUserAsync();
        var enrollment = await EnrollAsync(user);
        var code = TotpGenerator.ComputeCode(enrollment.SecretKey, TotpGenerator.GetCurrentTimeStep());
        await EnableAsync(user, code);

        await using var context = _fixture.CreateContext();
        var service = new TotpService(new TotpSecretRepository(context), new FakeSecretProtector());
        await service.DisableAsync(user.Id);

        Assert.False(await IsEnabledAsync(user));
    }

    [Fact]
    public async Task GetStatusReportsBackupCodeAvailability()
    {
        var user = await CreateUserAsync();

        await using var beforeContext = _fixture.CreateContext();
        var beforeService = new TotpService(new TotpSecretRepository(beforeContext), new FakeSecretProtector());
        var before = await beforeService.GetStatusAsync(user.Id);
        Assert.False(before.IsEnabled);
        Assert.Equal(0, before.AvailableBackupCodes);

        var enrollment = await EnrollAsync(user);
        var code = TotpGenerator.ComputeCode(enrollment.SecretKey, TotpGenerator.GetCurrentTimeStep());
        await EnableAsync(user, code);

        await using var afterContext = _fixture.CreateContext();
        var afterService = new TotpService(new TotpSecretRepository(afterContext), new FakeSecretProtector());
        var after = await afterService.GetStatusAsync(user.Id);
        Assert.True(after.IsEnabled);
        Assert.Equal(10, after.AvailableBackupCodes);
    }
}