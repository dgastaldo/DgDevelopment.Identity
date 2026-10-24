namespace DgDevelopment.Identity.Client.Maui.Services;

using System.Security.Cryptography;
using System.Text;

internal static class Pkce
{
    public static (string Verifier, string Challenge) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Base64Url(bytes);
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
