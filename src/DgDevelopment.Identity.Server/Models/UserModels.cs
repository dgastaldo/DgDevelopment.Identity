using DgDevelopment.Identity.Domain.Entities;

namespace DgDevelopment.Identity.Server.Models;

public sealed record CreateUserRequest(string Username, string Password, string Email, bool IsSystemAccount = false);

public sealed record ResetPasswordRequest(string Password);

public sealed record ScopedAssignmentRequest(string? ScopeType = null, string? ScopeValue = null);

public sealed record UserResponse(
    Guid Id,
    string Username,
    string? Email,
    bool IsActive,
    bool IsLocked,
    bool IsSystemAccount,
    bool RequireMfa,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static UserResponse From(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new(user.Id, user.Username, user.PrimaryEmail?.Value, user.IsActive, user.IsLocked,
            user.IsSystemAccount, user.RequireMfa, user.CreatedAt, user.UpdatedAt);
    }
}

public sealed record UserPermissionResponse(string Name, string? ScopeType, string? ScopeValue);
