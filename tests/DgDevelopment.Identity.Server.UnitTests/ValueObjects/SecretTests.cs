namespace DgDevelopment.Identity.Server.UnitTests.ValueObjects;

using DgDevelopment.Identity.Domain.ValueObjects;

public sealed class SecretTests
{
    [Fact]
    public void GenerateProducesUrlSafeValues()
    {
        var secret = Secret.Generate(64);

        Assert.False(string.IsNullOrWhiteSpace(secret));
        Assert.Matches(@"^[A-Za-z0-9_-]+$", secret);
    }

    [Fact]
    public void GenerateProducesDistinctValues()
    {
        Assert.NotEqual(Secret.Generate(), Secret.Generate());
    }

    [Fact]
    public void GenerateHonoursRequestedByteLength()
    {
        var secret = Secret.Generate(64);

        Assert.True(secret.Length >= 64);
    }

    [Fact]
    public void GenerateWithShortLengthStillYieldsUsableSecret()
    {
        var secret = Secret.Generate(16);

        Assert.False(string.IsNullOrWhiteSpace(secret));
    }

    [Fact]
    public void FromHashWrapsTheGivenValue()
    {
        var secret = Secret.FromHash("known-hash");

        Assert.Equal("known-hash", secret.Value);
        Assert.Equal("known-hash", secret.ToString());
    }
}