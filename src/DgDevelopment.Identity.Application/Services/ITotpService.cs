namespace DgDevelopment.Identity.Application.Services;

using System.Security.Cryptography;
using System.Text;
using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;

public interface ITotpService
{
    Task<TotpEnrollment> EnrollAsync(Guid userId, string accountName, CancellationToken ct = default);
    Task<bool> IsEnabledAsync(Guid userId, CancellationToken ct = default);
    Task<TotpStatus> GetStatusAsync(Guid userId, CancellationToken ct = default);
    Task<BackupCodeResult> EnableAsync(Guid userId, string verificationCode, CancellationToken ct = default);
    Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default);
    Task<BackupCodeResult> RegenerateBackupCodesAsync(Guid userId, CancellationToken ct = default);
    Task DisableAsync(Guid userId, CancellationToken ct = default);
}

public sealed class TotpService : ITotpService
{
    private const int BackupCodeCount = 10;

    private readonly ITotpSecretRepository _totpRepository;
    private readonly ISecretProtector _secretProtector;

    public TotpService(ITotpSecretRepository totpRepository, ISecretProtector secretProtector)
    {
        _totpRepository = totpRepository;
        _secretProtector = secretProtector;
    }

    public async Task<TotpEnrollment> EnrollAsync(Guid userId, string accountName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);

        var existing = await _totpRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (existing is { IsEnabled: true })
            throw new InvalidOperationException("TOTP is already enabled for this user.");

        if (existing is null)
        {
            existing = new DgDevelopment.Identity.Domain.Entities.TotpSecret(userId, _secretProtector.Protect(TotpGenerator.GenerateSecret()));
            await _totpRepository.AddAsync(existing, ct).ConfigureAwait(false);
        }

        var secretKey = _secretProtector.Unprotect(existing.SecretKey);
        var provisioningUri = TotpGenerator.BuildProvisioningUri("DgDevelopment", accountName, secretKey);
        return new TotpEnrollment(secretKey, provisioningUri);
    }

    public async Task<bool> IsEnabledAsync(Guid userId, CancellationToken ct = default)
    {
        var secret = await _totpRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        return secret is { IsEnabled: true };
    }

    public async Task<TotpStatus> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var secret = await _totpRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (secret is not { IsEnabled: true })
            return new TotpStatus(IsEnabled: false, AvailableBackupCodes: 0);

        return new TotpStatus(IsEnabled: true, AvailableBackupCodes: secret.AvailableBackupCodes());
    }

    public async Task<BackupCodeResult> EnableAsync(Guid userId, string verificationCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationCode);

        var secret = await _totpRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (secret is null)
            throw new InvalidOperationException("Enroll a TOTP secret before enabling it.");

        if (secret.IsEnabled)
            throw new InvalidOperationException("TOTP is already enabled for this user.");

        if (!TotpGenerator.VerifyCode(_secretProtector.Unprotect(secret.SecretKey), verificationCode))
            return new BackupCodeResult(IsValid: false, Codes: null);

        secret.Enable();
        var codes = GenerateBackupCodes();
        secret.RegenerateBackupCodes(codes.Select(c => HashCode(NormalizeBackupCode(c))).ToList());
        await _totpRepository.UpdateAsync(secret, ct).ConfigureAwait(false);

        return new BackupCodeResult(IsValid: true, Codes: codes);
    }

    public async Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var secret = await _totpRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (secret is not { IsEnabled: true })
            return false;

        if (TotpGenerator.VerifyCode(_secretProtector.Unprotect(secret.SecretKey), code))
            return true;

        var normalized = NormalizeBackupCode(code);
        if (secret.RedeemBackupCode(HashCode(normalized)) is not null)
        {
            await _totpRepository.UpdateAsync(secret, ct).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    public async Task<BackupCodeResult> RegenerateBackupCodesAsync(Guid userId, CancellationToken ct = default)
    {
        var secret = await _totpRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (secret is not { IsEnabled: true })
            return new BackupCodeResult(IsValid: false, Codes: null);

        var codes = GenerateBackupCodes();
        secret.RegenerateBackupCodes(codes.Select(c => HashCode(NormalizeBackupCode(c))).ToList());
        await _totpRepository.UpdateAsync(secret, ct).ConfigureAwait(false);

        return new BackupCodeResult(IsValid: true, Codes: codes);
    }

    public async Task DisableAsync(Guid userId, CancellationToken ct = default)
    {
        var secret = await _totpRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (secret is null)
            return;

        secret.Disable();
        await _totpRepository.UpdateAsync(secret, ct).ConfigureAwait(false);
    }

    private static List<string> GenerateBackupCodes()
    {
        var codes = new List<string>(BackupCodeCount);
        for (var i = 0; i < BackupCodeCount; i++)
        {
            var value = TotpGenerator.Base32Encode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6));
            codes.Add($"{value[..5]}-{value[5..]}");
        }

        return codes;
    }

    private static string NormalizeBackupCode(string code)
        => code.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

    private static string HashCode(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}