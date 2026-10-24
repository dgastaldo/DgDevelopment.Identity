using System.Security.Cryptography;

namespace DgDevelopment.Identity.Server.Security;

/// <summary>
/// One random nonce per request, used to allow the single inline &lt;script&gt; block
/// (Account/Mfa.cshtml) under a strict Content-Security-Policy without falling back to
/// 'unsafe-inline'. Registered scoped so every consumer within a request - the CSP header
/// middleware and any Razor page that emits a nonce="..." attribute - see the same value.
/// </summary>
public sealed class CspNonceService
{
    private readonly Lazy<string> _nonce = new(() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)));

    public string Nonce => _nonce.Value;
}
