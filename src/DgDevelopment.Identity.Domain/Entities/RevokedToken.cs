using System;
using System.Security.Cryptography;
using System.Text;

namespace DgDevelopment.Identity.Domain.Entities;

public sealed class RevokedToken
{
    public Guid Id { get; private set; }
    public string JtiHash { get; private set; }
    public string TokenType { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTime RevokedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private RevokedToken() { }

    public RevokedToken(string jti, string tokenType, Guid? clientId, Guid? userId, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(jti);
        ArgumentNullException.ThrowIfNull(tokenType);
        Id = Guid.NewGuid();
        JtiHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(jti)));
        TokenType = tokenType;
        ClientId = clientId;
        UserId = userId;
        RevokedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
}