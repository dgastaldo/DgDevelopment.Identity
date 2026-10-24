namespace DgDevelopment.Identity.Application.Services;

public sealed record TotpEnrollment(string SecretKey, Uri ProvisioningUri);

public sealed record TotpStatus(bool IsEnabled, int AvailableBackupCodes);

public sealed record BackupCodeResult(bool IsValid, IReadOnlyCollection<string>? Codes);

public sealed record PushDeviceDto(Guid Id, string Platform, string? DeviceName, DateTime CreatedAt);

public sealed record PushChallengeCreated(Guid ChallengeId);