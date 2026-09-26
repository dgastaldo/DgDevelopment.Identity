namespace DgDevelopment.Identity.Server.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;
using Xunit;

public sealed class PlatformTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void ConstructorSetsFields()
    {
        var platform = new Platform(TenantId,"IdentityAdmin", "Admin platform", PermissionMode.IdentityManaged);

        Assert.False(string.IsNullOrWhiteSpace(platform.Id.ToString()));
        Assert.Equal("IdentityAdmin", platform.Name);
        Assert.Equal("Admin platform", platform.Description);
        Assert.Equal(PermissionMode.IdentityManaged, platform.PermissionMode);
    }

    [Fact]
    public void ConstructorDefaultsToIdentityManagedMode()
    {
        var platform = new Platform(TenantId,"Reporting", "Reporting platform", PermissionMode.AuthOnly);

        Assert.Equal(PermissionMode.AuthOnly, platform.PermissionMode);
    }
}