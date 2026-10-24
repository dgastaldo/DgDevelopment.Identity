namespace DgDevelopment.Identity.Server.UnitTests.Testing;

using DgDevelopment.Identity.Domain.Services;

public sealed class FakePasswordHasher : IPasswordHasher
{
    private const string Salt = "fake-salt";

    public string HashPassword(string password) => $"{Salt}:{password}";

    public bool VerifyPassword(string password, string hash) => hash == HashPassword(password);
}