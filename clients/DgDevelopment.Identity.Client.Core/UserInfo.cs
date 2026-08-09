namespace DgDevelopment.Identity.Client.Core;

public sealed class UserInfo
{
    public string Sub { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Email { get; init; }
    public bool EmailVerified { get; init; }
    public string[]? Permissions { get; init; }
}
