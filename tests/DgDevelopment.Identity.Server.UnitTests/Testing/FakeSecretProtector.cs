namespace DgDevelopment.Identity.Server.UnitTests.Testing;

using DgDevelopment.Identity.Domain.Services;

public sealed class FakeSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => $"protected:{plaintext}";

    public string Unprotect(string protectedValue)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);

        return protectedValue.StartsWith("protected:", StringComparison.Ordinal)
            ? protectedValue["protected:".Length..]
            : protectedValue;
    }
}
