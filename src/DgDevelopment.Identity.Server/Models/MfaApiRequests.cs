namespace DgDevelopment.Identity.Server.Models;

public sealed record RegisterPushDeviceRequest(string? Platform, string? PushToken, string? DeviceName, string? TotpCode);

public sealed record ResolveChallengeRequest(string? ChallengeCode);

public sealed record EnableTotpRequest(string? Code);
