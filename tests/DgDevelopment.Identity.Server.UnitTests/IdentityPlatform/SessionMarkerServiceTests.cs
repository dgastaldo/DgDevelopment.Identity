namespace DgDevelopment.Identity.Server.UnitTests.IdentityPlatform;

using System.Text.Json;
using DgDevelopment.Identity.Client.Blazor;
using Xunit;

public sealed class SessionMarkerServiceTests
{
    [Fact]
    public void DecodeReturnsNullForNullOrWhitespace()
    {
        Assert.Null(SessionMarkerService.Decode(null));
        Assert.Null(SessionMarkerService.Decode(string.Empty));
        Assert.Null(SessionMarkerService.Decode("   "));
    }

    [Fact]
    public void DecodeReturnsNullForMalformedJson()
    {
        Assert.Null(SessionMarkerService.Decode("{not-json"));
    }

    [Fact]
    public void DecodeRoundTripsAValidMarker()
    {
        var marker = new SessionMarkerDto("sub-1", "Name", "name@example.com");
        var raw = JsonSerializer.Serialize(marker);

        var decoded = SessionMarkerService.Decode(raw);

        Assert.Equal(marker, decoded);
    }
}
