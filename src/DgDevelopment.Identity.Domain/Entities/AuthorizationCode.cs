using System;

namespace DgDevelopment.Identity.Domain.Entities;

public sealed class AuthorizationCode
{
    public Guid Id { get; private set; }
    public string CodeHash { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid UserId { get; private set; }
    public Uri RedirectUri { get; private set; }
    public string Scopes { get; private set; }
    public string? CodeChallengeHash { get; private set; }
    public string? CodeChallengeMethod { get; private set; }
    public bool IsUsed { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private AuthorizationCode() { }

    public AuthorizationCode(string codeHash, Guid clientId, Guid userId, Uri redirectUri, string[] scopes,
        string? codeChallengeHash = null, string? codeChallengeMethod = null, int lifetimeSeconds = 300)
    {
        ArgumentNullException.ThrowIfNull(redirectUri);
        Id = Guid.NewGuid();
        CodeHash = codeHash;
        ClientId = clientId;
        UserId = userId;
        RedirectUri = redirectUri;
        Scopes = string.Join(' ', scopes);
        CodeChallengeHash = codeChallengeHash;
        CodeChallengeMethod = codeChallengeMethod;
        IsUsed = false;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddSeconds(lifetimeSeconds);
    }

    public void MarkUsed() => IsUsed = true;

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;

    public string[] GetScopes() => Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
