namespace DgDevelopment.Identity.Server.Models;

public sealed record MeEmailResponse(Guid Id, string Email, bool IsPrimary, bool IsVerified);

public sealed record MeResponse(Guid Id, string Username, IReadOnlyCollection<MeEmailResponse> Emails, bool RequireMfa, DateTime CreatedAt);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record AddEmailRequest(string? Email);
