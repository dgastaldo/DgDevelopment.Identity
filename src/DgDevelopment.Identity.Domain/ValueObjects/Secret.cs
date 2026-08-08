namespace DgDevelopment.Identity.Domain.ValueObjects;

public sealed record Secret
{
    public string Value { get; }

    public Secret()
    {
        Value = Generate();
    }

    private Secret(string value) => Value = value;

    public static Secret FromHash(string hash) => new(hash);

    public static string Generate(int length = 64)
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public override string ToString() => Value;
}
