namespace DgDevelopment.Identity.Server.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;
using Xunit;

public sealed class PermissionTests
{
    [Fact]
    public void ConstructorSetsFields()
    {
        var permission = new Permission("user:read", "Read users", "User");

        Assert.False(string.IsNullOrWhiteSpace(permission.Id.ToString()));
        Assert.Equal("user:read", permission.Name);
        Assert.Equal("Read users", permission.Description);
        Assert.Equal("User", permission.ResourceType);
    }
}