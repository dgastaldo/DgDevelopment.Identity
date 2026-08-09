namespace DgDevelopment.Identity.Client.Core;

public sealed class TokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public string? IdToken { get; init; }
    public string? RefreshToken { get; init; }
    public string Scope { get; init; } = string.Empty;
    public DateTime IssuedAt { get; init; } = DateTime.UtcNow;

    public bool IsExpired() => DateTime.UtcNow >= IssuedAt.AddSeconds(ExpiresIn - 30);
}
