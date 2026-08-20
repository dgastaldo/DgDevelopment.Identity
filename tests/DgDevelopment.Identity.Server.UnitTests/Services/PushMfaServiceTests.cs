namespace DgDevelopment.Identity.Server.UnitTests.Services;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Application.Services;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Services;
using DgDevelopment.Identity.Domain.ValueObjects;
using DgDevelopment.Identity.Infrastructure.Data;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Server.UnitTests.Testing;

public sealed class PushMfaServiceTests : IClassFixture<DatabaseFixture<PushMfaServiceTests>>
{
    private readonly DatabaseFixture<PushMfaServiceTests> _fixture;

    public PushMfaServiceTests(DatabaseFixture<PushMfaServiceTests> fixture)
    {
        _fixture = fixture;
    }

    private static PushMfaService CreateService(IdentityDbContext context, FakePushNotifier? notifier = null)
    {
        var notifiers = notifier is null
            ? Array.Empty<IPushNotifier>()
            : new IPushNotifier[] { notifier };

        return new PushMfaService(
            new PushDeviceRepository(context),
            new MfaChallengeRepository(context),
            new TotpSecretRepository(context),
            new FakeSecretProtector(),
            notifiers);
    }

    private async Task<User> CreateUserAsync()
    {
        await using var context = _fixture.CreateContext();
        var user = new User($"push-{Guid.NewGuid():N}", "hash", EmailAddress.FromString($"{Guid.NewGuid():N}@dgdevelopment.it"));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<PushDevice> SeedDeviceAsync(Guid userId, string? token = null)
    {
        await using var context = _fixture.CreateContext();
        var device = new PushDevice(userId, PushPlatform.Android, token ?? $"token-{Guid.NewGuid():N}", "Test device");
        context.PushDevices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private async Task RegisterDeviceAsync(Guid userId, string platform, string pushToken, string? deviceName)
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        await service.RegisterDeviceAsync(userId, platform, pushToken, deviceName, totpCode: null);
    }

    private async Task<int> CountDevicesAsync(Guid userId)
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        return (await service.GetDevicesAsync(userId)).Count;
    }

    private async Task<IReadOnlyCollection<PushDeviceDto>> GetDevicesAsync(Guid userId)
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        return await service.GetDevicesAsync(userId);
    }

    [Fact]
    public async Task HasActiveDevicesReflectsRegisteredDevices()
    {
        var user = await CreateUserAsync();

        await using var context = _fixture.CreateContext();
        var service = CreateService(context);
        Assert.False(await service.HasActiveDevicesAsync(user.Id));

        await SeedDeviceAsync(user.Id);
        Assert.True(await service.HasActiveDevicesAsync(user.Id));
    }

    [Fact]
    public async Task RegisterDevicePersistsDevice()
    {
        var user = await CreateUserAsync();

        await RegisterDeviceAsync(user.Id, "Android", "abc-123", "Pixel");

        Assert.Equal(1, await CountDevicesAsync(user.Id));
        var device = (await GetDevicesAsync(user.Id)).Single();
        Assert.Equal("Android", device.Platform);
        Assert.Equal("Pixel", device.DeviceName);
    }

    [Fact]
    public async Task RegisterDeviceRejectsUnknownPlatform()
    {
        var user = await CreateUserAsync();
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RegisterDeviceAsync(user.Id, "Symbian", "token", "Nokia", totpCode: null));
    }

    [Fact]
    public async Task RegisterDeviceIsIdempotentForSameToken()
    {
        var user = await CreateUserAsync();

        await RegisterDeviceAsync(user.Id, "Android", "token-1", "Device");
        await RegisterDeviceAsync(user.Id, "Android", "token-1", "Device");

        Assert.Equal(1, await CountDevicesAsync(user.Id));
    }

    [Fact]
    public async Task RemoveDeviceDeletesOnlyOwnedDevice()
    {
        var user = await CreateUserAsync();
        var other = await CreateUserAsync();
        var device = await SeedDeviceAsync(user.Id);
        var otherDevice = await SeedDeviceAsync(other.Id);
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await service.RemoveDeviceAsync(user.Id, otherDevice.Id);
        Assert.Equal(1, await CountDevicesAsync(user.Id));

        await service.RemoveDeviceAsync(user.Id, device.Id);
        Assert.Equal(0, await CountDevicesAsync(user.Id));
    }

    private async Task<PushChallengeCreated> StartChallengeAsync(Guid userId, FakePushNotifier notifier)
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context, notifier);
        return await service.StartChallengeAsync(userId);
    }

    [Fact]
    public async Task StartChallengeThrowsWithoutDevices()
    {
        var user = await CreateUserAsync();
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartChallengeAsync(user.Id));
    }

    [Fact]
    public async Task StartChallengeCreatesPendingChallengeAndNotifies()
    {
        var user = await CreateUserAsync();
        await SeedDeviceAsync(user.Id);
        var notifier = new FakePushNotifier();

        var created = await StartChallengeAsync(user.Id, notifier);

        Assert.NotEqual(Guid.Empty, created.ChallengeId);
        await using var readContext = _fixture.CreateContext();
        var challenge = await new MfaChallengeRepository(readContext).GetByIdAsync(created.ChallengeId);
        Assert.NotNull(challenge);
        Assert.Equal(MfaChallengeStatus.Pending, challenge.Status);
        Assert.Single(notifier.SentNotifications);
        Assert.Equal(PushNotificationKind.ChallengeNew, notifier.SentNotifications[0].Kind);
    }

    [Fact]
    public async Task ApproveRequiresValidChallengeCode()
    {
        var user = await CreateUserAsync();
        await SeedDeviceAsync(user.Id);
        var notifier = new FakePushNotifier();
        var created = await StartChallengeAsync(user.Id, notifier);
        await using var context = _fixture.CreateContext();
        var service = CreateService(context, notifier);

        Assert.False(await service.ApproveAsync(created.ChallengeId, "000000"));
    }

    [Fact]
    public async Task ApproveWithCodeResolvesChallenge()
    {
        var user = await CreateUserAsync();
        await SeedDeviceAsync(user.Id);
        var notifier = new FakePushNotifier();
        var created = await StartChallengeAsync(user.Id, notifier);
        var code = notifier.ExtractChallengeCode(created.ChallengeId);
        await using var context = _fixture.CreateContext();
        var service = CreateService(context, notifier);

        var approved = await service.ApproveAsync(created.ChallengeId, code!);

        Assert.True(approved);
        await using var readContext = _fixture.CreateContext();
        var challenge = await new MfaChallengeRepository(readContext).GetByIdAsync(created.ChallengeId);
        Assert.NotNull(challenge);
        Assert.Equal(MfaChallengeStatus.Approved, challenge.Status);
        Assert.NotNull(challenge.ResolvedAt);
        Assert.Equal(2, notifier.SentNotifications.Count);
        Assert.Equal(PushNotificationKind.ChallengeResolved, notifier.SentNotifications[1].Kind);
    }

    [Fact]
    public async Task DenyWithCodeResolvesChallenge()
    {
        var user = await CreateUserAsync();
        await SeedDeviceAsync(user.Id);
        var notifier = new FakePushNotifier();
        var created = await StartChallengeAsync(user.Id, notifier);
        var code = notifier.ExtractChallengeCode(created.ChallengeId);
        await using var context = _fixture.CreateContext();
        var service = CreateService(context, notifier);

        var denied = await service.DenyAsync(created.ChallengeId, code!);

        Assert.True(denied);
        await using var readContext = _fixture.CreateContext();
        var challenge = await new MfaChallengeRepository(readContext).GetByIdAsync(created.ChallengeId);
        Assert.Equal(MfaChallengeStatus.Denied, challenge!.Status);
    }

    [Fact]
    public async Task ApproveIsIdempotentAfterResolution()
    {
        var user = await CreateUserAsync();
        await SeedDeviceAsync(user.Id);
        var notifier = new FakePushNotifier();
        var created = await StartChallengeAsync(user.Id, notifier);
        var code = notifier.ExtractChallengeCode(created.ChallengeId);
        await using var context = _fixture.CreateContext();
        var service = CreateService(context, notifier);

        Assert.True(await service.ApproveAsync(created.ChallengeId, code!));
        Assert.False(await service.ApproveAsync(created.ChallengeId, code!));
    }

    [Fact]
    public async Task IsChallengeApprovedForUserMatches()
    {
        var user = await CreateUserAsync();
        var other = await CreateUserAsync();
        await SeedDeviceAsync(user.Id);
        var notifier = new FakePushNotifier();
        var created = await StartChallengeAsync(user.Id, notifier);
        var code = notifier.ExtractChallengeCode(created.ChallengeId);
        await using var context = _fixture.CreateContext();
        var service = CreateService(context, notifier);

        Assert.False(await service.IsChallengeApprovedForUserAsync(created.ChallengeId, user.Id));

        await service.ApproveAsync(created.ChallengeId, code!);

        Assert.True(await service.IsChallengeApprovedForUserAsync(created.ChallengeId, user.Id));
        Assert.False(await service.IsChallengeApprovedForUserAsync(created.ChallengeId, other.Id));
    }

    [Fact]
    public async Task GetChallengeStatusReturnsExpiredForUnknownChallenge()
    {
        await using var context = _fixture.CreateContext();
        var service = CreateService(context);

        var status = await service.GetChallengeStatusAsync(Guid.NewGuid());

        Assert.Equal(MfaChallengeStatus.Expired, status);
    }

    private sealed class FakePushNotifier : IPushNotifier
    {
        public List<PushNotification> SentNotifications { get; } = [];

        public Task SendAsync(PushNotification notification, IReadOnlyCollection<PushDevice> devices, CancellationToken ct = default)
        {
            SentNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public string? ExtractChallengeCode(Guid challengeId)
        {
            var action = SentNotifications
                .Where(n => n.Kind == PushNotificationKind.ChallengeNew && n.ChallengeId == challengeId.ToString())
                .Select(n => n.ActionToken)
                .FirstOrDefault();

            return action?.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
        }
    }
}