using System.Security.Cryptography;

namespace DgDevelopment.Identity.Domain.ValueObjects;

public sealed record CodeChallenge
{
    public string Hash { get; }
    public string Method { get; }

    public CodeChallenge(string hash, string method = "S256")
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Code challenge hash cannot be empty.", nameof(hash));
        if (method != "S256")
            throw new ArgumentException("Only S256 code challenge method is supported.", nameof(method));

        Hash = hash;
        Method = method;
    }

    public bool Verify(string verifier)
    {
        var hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier));
        var base64Hash = Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        return Hash == base64Hash;
    }
}
