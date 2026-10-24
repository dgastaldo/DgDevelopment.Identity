namespace DgDevelopment.Identity.Server.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Services;

public sealed class TotpGeneratorTests
{
    private const string RfcSecret = "12345678901234567890";
    private const string RfcSecretBase32 = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    // RFC 6238 Appendix B (SHA1, 8 digits): the counters below are T = floor(time_seconds / 30),
    // the expected values are the last 6 digits of the published 8-digit codes.
    [Theory]
    [InlineData(1, "287082")] // time 59
    [InlineData(37037036, "081804")] // time 1111111109
    [InlineData(37037037, "050471")] // time 1111111111
    [InlineData(41152263, "005924")] // time 1234567890
    [InlineData(66666666, "279037")] // time 2000000000
    [InlineData(666666666, "353130")] // time 20000000000
    public void ComputeCodeMatchesRfc6238Vectors(long timeStep, string expected)
    {
        var code = TotpGenerator.ComputeCode(RfcSecretBase32, timeStep);

        Assert.Equal(expected, code);
    }

    [Fact]
    public void ComputeCodeAcceptsRawBytesSecret()
    {
        var code1 = TotpGenerator.ComputeCode(RfcSecretBase32, 1234567890);
        var code2 = TotpGenerator.ComputeCode(System.Text.Encoding.ASCII.GetBytes(RfcSecret), 1234567890);

        Assert.Equal(code1, code2);
    }

    [Fact]
    public void VerifyCodeAcceptsCurrentWindow()
    {
        var secret = TotpGenerator.GenerateSecret();
        var step = TotpGenerator.GetCurrentTimeStep();
        var code = TotpGenerator.ComputeCode(secret, step);

        Assert.True(TotpGenerator.VerifyCode(secret, code));
    }

    [Fact]
    public void VerifyCodeRejectsWrongOrMalformedCode()
    {
        var secret = TotpGenerator.GenerateSecret();
        var step = TotpGenerator.GetCurrentTimeStep();
        var code = TotpGenerator.ComputeCode(secret, step);

        Assert.False(TotpGenerator.VerifyCode(secret, "000000"));
        Assert.False(TotpGenerator.VerifyCode(secret, "12345"));
        Assert.False(TotpGenerator.VerifyCode(secret, "abcdef"));
        Assert.False(TotpGenerator.VerifyCode(secret, code, timeStep: step + 1000));
    }

    [Fact]
    public void GenerateSecretReturnsBase32OfExpectedLength()
    {
        var secret = TotpGenerator.GenerateSecret();

        Assert.False(string.IsNullOrWhiteSpace(secret));
        Assert.All(secret, c => Assert.True("ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".Contains(c, StringComparison.Ordinal)));
    }

    [Fact]
    public void Base32RoundTrips()
    {
        byte[] data = [0x00, 0x01, 0x02, 0x7f, 0x80, 0xff, 0xde, 0xad];
        var encoded = TotpGenerator.Base32Encode(data);
        var decoded = TotpGenerator.Base32Decode(encoded);

        Assert.Equal(data, decoded);
    }

    [Fact]
    public void Base32DecodeRejectsInvalidCharacters()
    {
        Assert.Throws<FormatException>(() => TotpGenerator.Base32Decode("0"));
        Assert.Throws<FormatException>(() => TotpGenerator.Base32Decode("!@#"));
    }

    [Fact]
    public void BuildProvisioningUriIncludesExpectedQueryParameters()
    {
        var uri = TotpGenerator.BuildProvisioningUri("DgDevelopment", "john", RfcSecretBase32);

        Assert.Equal("otpauth", uri.Scheme);
        Assert.Equal("totp", uri.Host);
        Assert.Contains("secret=" + RfcSecretBase32, uri.Query, StringComparison.Ordinal);
        Assert.Contains("issuer=DgDevelopment", uri.Query, StringComparison.Ordinal);
        Assert.Contains("algorithm=SHA1", uri.Query, StringComparison.Ordinal);
        Assert.Contains("digits=6", uri.Query, StringComparison.Ordinal);
        Assert.Contains("period=30", uri.Query, StringComparison.Ordinal);
    }
}