namespace DgDevelopment.Identity.Client.Core;

using System.Text.Json.Serialization;

public sealed class TokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; init; } = string.Empty;
    [JsonPropertyName("token_type")] public string TokenType { get; init; } = string.Empty;
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    [JsonPropertyName("id_token")] public string? IdToken { get; init; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
    [JsonPropertyName("scope")] public string Scope { get; init; } = string.Empty;
    [JsonPropertyName("issued_at")] public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public bool IsExpired() => DateTime.UtcNow >= IssuedAt.AddSeconds(ExpiresIn - 30);
}
