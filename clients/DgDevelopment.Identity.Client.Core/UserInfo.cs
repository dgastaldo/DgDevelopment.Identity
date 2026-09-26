namespace DgDevelopment.Identity.Client.Core;

using System.Text.Json.Serialization;

public sealed class UserInfo
{
    [JsonPropertyName("sub")] public string Sub { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("email_verified")] public bool EmailVerified { get; init; }
    [JsonPropertyName("permissions")] public IReadOnlyCollection<string>? Permissions { get; init; }
}
