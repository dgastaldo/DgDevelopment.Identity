using DgDevelopment.Identity.Domain.Services;
using Microsoft.AspNetCore.DataProtection;

namespace DgDevelopment.Identity.Infrastructure.Security;

public sealed class TotpSecretProtector : ISecretProtector
{
    private const string Purpose = "DgDevelopment.Identity.TotpSecret";

    private readonly IDataProtector _protector;

    public TotpSecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}