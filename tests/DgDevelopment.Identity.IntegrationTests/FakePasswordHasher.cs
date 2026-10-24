namespace DgDevelopment.Identity.IntegrationTests;

using DgDevelopment.Identity.Domain.Services;

public sealed class FakePasswordHasher : IPasswordHasher
{
    private const string Salt = "integration-test-salt";

    public string HashPassword(string password) => $"{Salt}:{password}";

    public bool VerifyPassword(string password, string hash) => hash == HashPassword(password);
}
