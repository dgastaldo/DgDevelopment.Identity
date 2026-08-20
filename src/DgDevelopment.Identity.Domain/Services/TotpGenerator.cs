namespace DgDevelopment.Identity.Domain.Services;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

public static class TotpGenerator
{
    public const int CodeDigits = 6;
    public const int TimeStepSeconds = 30;
    public const int SecretLengthBytes = 20;
    public const string DefaultAlgorithm = "SHA1";

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretLengthBytes);
        return Base32Encode(bytes);
    }

    public static long GetCurrentTimeStep()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TimeStepSeconds;

    public static string ComputeCode(string base32Secret, long timeStep)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);
        return ComputeCode(Base32Decode(base32Secret), timeStep);
    }

    public static string ComputeCode(byte[] secret, long timeStep)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length == 0)
            throw new ArgumentException("Secret cannot be empty.", nameof(secret));

        var counterBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes);

        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        var otp = binary % (int)Math.Pow(10, CodeDigits);
        return otp.ToString("D" + CodeDigits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    public static bool VerifyCode(string base32Secret, string code, int window = 1, long? timeStep = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (code.Length != CodeDigits)
            return false;

        var current = timeStep ?? GetCurrentTimeStep();
        for (var skew = -Math.Abs(window); skew <= Math.Abs(window); skew++)
        {
            if (FixedTimeEquals(ComputeCode(base32Secret, current + skew), code))
                return true;
        }

        return false;
    }

    public static Uri BuildProvisioningUri(string issuer, string accountName, string base32Secret, string? algorithm = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);

        var label = $"{issuer}:{accountName}";
        var query = $"secret={base32Secret}"
            + $"&issuer={Uri.EscapeDataString(issuer)}"
            + $"&algorithm={Uri.EscapeDataString(algorithm ?? DefaultAlgorithm)}"
            + $"&digits={CodeDigits}"
            + $"&period={TimeStepSeconds}";

        return new Uri($"otpauth://totp/{Uri.EscapeDataString(label)}?{query}");
    }

    public static string Base32Encode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        var bitsRemaining = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsRemaining += 8;
            while (bitsRemaining >= 5)
            {
                bitsRemaining -= 5;
                builder.Append(Base32Alphabet[(buffer >> bitsRemaining) & 0x1f]);
            }
        }

        if (bitsRemaining > 0)
            builder.Append(Base32Alphabet[(buffer << (5 - bitsRemaining)) & 0x1f]);

        return builder.ToString();
    }

    public static byte[] Base32Decode(string encoded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encoded);
        var cleaned = encoded.ToUpperInvariant().Replace("=", string.Empty, StringComparison.Ordinal);
        if (cleaned.Any(c => ValueOf(c) < 0))
            throw new FormatException("Input contains characters outside the base32 alphabet.");

        int buffer = 0;
        var bitsRemaining = 0;
        var output = new List<byte>(cleaned.Length * 5 / 8);

        foreach (var c in cleaned)
        {
            buffer = (buffer << 5) | ValueOf(c);
            bitsRemaining += 5;
            if (bitsRemaining >= 8)
            {
                bitsRemaining -= 8;
                output.Add((byte)((buffer >> bitsRemaining) & 0xff));
            }
        }

        return [.. output];
    }

    private static int ValueOf(char c)
        => c switch
        {
            >= 'A' and <= 'Z' => c - 'A',
            >= '2' and <= '7' => c - '2' + 26,
            _ => -1,
        };

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var actualBytes = Encoding.ASCII.GetBytes(actual);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}