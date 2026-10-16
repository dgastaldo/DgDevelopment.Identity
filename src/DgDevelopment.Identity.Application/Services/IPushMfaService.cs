namespace DgDevelopment.Identity.Application.Services;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Entities;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;

public interface IPushMfaService
{
    Task<bool> HasActiveDevicesAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyCollection<PushDeviceDto>> GetDevicesAsync(Guid userId, CancellationToken ct = default);
    Task RegisterDeviceAsync(Guid userId, string platform, string pushToken, string? deviceName, string? totpCode, CancellationToken ct = default);
    Task RemoveDeviceAsync(Guid userId, Guid deviceId, CancellationToken ct = default);
    Task<PushChallengeCreated> StartChallengeAsync(Guid userId, CancellationToken ct = default);
    Task<bool> ApproveAsync(Guid challengeId, string challengeCode, CancellationToken ct = default);
    Task<bool> DenyAsync(Guid challengeId, string challengeCode, CancellationToken ct = default);
    Task<MfaChallengeStatus> GetChallengeStatusAsync(Guid challengeId, CancellationToken ct = default);
    Task<bool> IsChallengeApprovedForUserAsync(Guid challengeId, Guid userId, CancellationToken ct = default);
}

public sealed class PushMfaService : IPushMfaService
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly IPushDeviceRepository _deviceRepository;
    private readonly IMfaChallengeRepository _challengeRepository;
    private readonly ITotpSecretRepository _totpRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly IEnumerable<IPushNotifier> _notifiers;

    public PushMfaService(
        IPushDeviceRepository deviceRepository,
        IMfaChallengeRepository challengeRepository,
        ITotpSecretRepository totpRepository,
        ISecretProtector secretProtector,
        IEnumerable<IPushNotifier> notifiers)
    {
        _deviceRepository = deviceRepository;
        _challengeRepository = challengeRepository;
        _totpRepository = totpRepository;
        _secretProtector = secretProtector;
        _notifiers = notifiers;
    }

    public async Task<bool> HasActiveDevicesAsync(Guid userId, CancellationToken ct = default)
        => (await _deviceRepository.GetActiveByUserIdAsync(userId, ct).ConfigureAwait(false)).Count > 0;

    public async Task<IReadOnlyCollection<PushDeviceDto>> GetDevicesAsync(Guid userId, CancellationToken ct = default)
    {
        var devices = await _deviceRepository.GetActiveByUserIdAsync(userId, ct).ConfigureAwait(false);
        return devices
            .Select(d => new PushDeviceDto(d.Id, d.Platform.ToString(), d.DeviceName, d.CreatedAt))
            .ToList();
    }

    public async Task RegisterDeviceAsync(Guid userId, string platform, string pushToken, string? deviceName, string? totpCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pushToken);
        if (!Enum.TryParse<PushPlatform>(platform, ignoreCase: true, out var parsedPlatform))
            throw new ArgumentException($"Unknown push platform '{platform}'.", nameof(platform));

        var secret = await _totpRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (secret is { IsEnabled: true })
        {
            if (string.IsNullOrWhiteSpace(totpCode) || !TotpGenerator.VerifyCode(_secretProtector.Unprotect(secret.SecretKey), totpCode))
                throw new InvalidOperationException("A valid TOTP code is required to register a device.");
        }

        var existing = (await _deviceRepository.GetActiveByUserIdAsync(userId, ct).ConfigureAwait(false))
            .FirstOrDefault(d => d.PushToken == pushToken);

        if (existing is null)
        {
            await _deviceRepository.AddAsync(new PushDevice(userId, parsedPlatform, pushToken, deviceName), ct).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(existing.PushToken, pushToken, StringComparison.Ordinal))
            existing.UpdateToken(pushToken);
        else
            existing.MarkSeen();

        await _deviceRepository.UpdateAsync(existing, ct).ConfigureAwait(false);
    }

    public async Task RemoveDeviceAsync(Guid userId, Guid deviceId, CancellationToken ct = default)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId, ct).ConfigureAwait(false);
        if (device is null || device.UserId != userId)
            return;

        await _deviceRepository.DeleteAsync(deviceId, ct).ConfigureAwait(false);
    }

    public async Task<PushChallengeCreated> StartChallengeAsync(Guid userId, CancellationToken ct = default)
    {
        var devices = await _deviceRepository.GetActiveByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (devices.Count == 0)
            throw new InvalidOperationException("No active push device is registered for this user.");

        var challengeCode = GenerateChallengeCode();
        var challenge = new MfaChallenge(userId, "push", HashCode(challengeCode), (int)ChallengeLifetime.TotalSeconds);
        await _challengeRepository.AddAsync(challenge, ct).ConfigureAwait(false);

        var notification = new PushNotification(
            PushNotificationKind.ChallengeNew,
            userId,
            "Sign-in request",
            "A new sign-in request is pending your approval.",
            ActionToken: $"{challenge.Id}/{challengeCode}",
            ChallengeId: challenge.Id.ToString());

        foreach (var notifier in _notifiers)
            await notifier.SendAsync(notification, devices, ct).ConfigureAwait(false);

        return new PushChallengeCreated(challenge.Id);
    }

    public async Task<bool> ApproveAsync(Guid challengeId, string challengeCode, CancellationToken ct = default)
        => await ResolveAsync(challengeId, challengeCode, MfaChallengeStatus.Approved, ct).ConfigureAwait(false);

    public async Task<bool> DenyAsync(Guid challengeId, string challengeCode, CancellationToken ct = default)
        => await ResolveAsync(challengeId, challengeCode, MfaChallengeStatus.Denied, ct).ConfigureAwait(false);

    public async Task<MfaChallengeStatus> GetChallengeStatusAsync(Guid challengeId, CancellationToken ct = default)
        => (await _challengeRepository.GetStatusAsync(challengeId, ct).ConfigureAwait(false)) ?? MfaChallengeStatus.Expired;

    public async Task<bool> IsChallengeApprovedForUserAsync(Guid challengeId, Guid userId, CancellationToken ct = default)
    {
        var challenge = await _challengeRepository.GetByIdAsync(challengeId, ct).ConfigureAwait(false);
        return challenge is not null
            && challenge.UserId == userId
            && challenge.Status == MfaChallengeStatus.Approved
            && !challenge.IsExpired();
    }

    private async Task<bool> ResolveAsync(Guid challengeId, string challengeCode, MfaChallengeStatus status, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(challengeCode);

        var challenge = await _challengeRepository.GetByIdAsync(challengeId, ct).ConfigureAwait(false);
        if (challenge is null || challenge.IsExpired() || challenge.Status != MfaChallengeStatus.Pending)
            return false;

        if (!FixedTimeEquals(HashCode(challengeCode), challenge.ChallengeCodeHash))
            return false;

        if (!challenge.Resolve(status))
            return false;

        await _challengeRepository.UpdateAsync(challenge, ct).ConfigureAwait(false);

        var devices = await _deviceRepository.GetActiveByUserIdAsync(challenge.UserId, ct).ConfigureAwait(false);
        var notification = new PushNotification(
            PushNotificationKind.ChallengeResolved,
            challenge.UserId,
            status == MfaChallengeStatus.Approved ? "Sign-in approved" : "Sign-in denied",
            status == MfaChallengeStatus.Approved ? "The sign-in request was approved." : "The sign-in request was denied.",
            ChallengeId: challengeId.ToString());

        foreach (var notifier in _notifiers)
            await notifier.SendAsync(notification, devices, ct).ConfigureAwait(false);

        return true;
    }

    private static string GenerateChallengeCode()
        => System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

    private static string HashCode(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedTimeEquals(string expected, string actual)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}