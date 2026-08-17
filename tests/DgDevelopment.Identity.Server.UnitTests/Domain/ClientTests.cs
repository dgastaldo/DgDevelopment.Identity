namespace DgDevelopment.Identity.Server.UnitTests.Domain;

using DgDevelopment.Identity.Domain.Entities;

public sealed class ClientTests
{
    private static Client CreateClient(ClientType clientType = ClientType.Confidential, bool requireConsent = true)
        => new("test-client", "secret-hash", "Test Client", clientType, requireConsent: requireConsent);

    [Fact]
    public void ConstructorDefaultsAreActiveAndEmpty()
    {
        var client = CreateClient();

        Assert.True(client.IsActive);
        Assert.True(client.RequireConsent);
        Assert.False(client.RequirePkce);
        Assert.Null(client.PlatformId);
        Assert.Empty(client.GrantTypes);
        Assert.Empty(client.Scopes);
        Assert.Empty(client.RedirectUris);
        Assert.Empty(client.PostLogoutRedirectUris);
        Assert.Empty(client.AdminConsentScopes);
        Assert.False(client.CreatedAt == default);
        Assert.False(client.UpdatedAt == default);
    }

    [Fact]
    public void ConstructorPublicClientRequiresPkce()
    {
        var client = CreateClient(ClientType.Public);

        Assert.True(client.RequirePkce);
    }

    [Fact]
    public void ConstructorHonoursRequireConsentOverride()
    {
        var client = CreateClient(requireConsent: false);

        Assert.False(client.RequireConsent);
    }

    [Fact]
    public void AddGrantTypeDeduplicates()
    {
        var client = CreateClient();
        client.AddGrantType("authorization_code");
        client.AddGrantType("authorization_code");
        client.AddGrantType("refresh_token");

        Assert.Equal(["authorization_code", "refresh_token"], client.GrantTypes.Select(g => g.GrantType));
    }

    [Fact]
    public void AddScopeDeduplicates()
    {
        var client = CreateClient();
        client.AddScope("openid");
        client.AddScope("openid");
        client.AddScope("profile");

        Assert.Equal(["openid", "profile"], client.Scopes.Select(s => s.Scope));
    }

    [Fact]
    public void AddAdminConsentScopeDeduplicates()
    {
        var client = CreateClient();
        client.AddAdminConsentScope("profile");
        client.AddAdminConsentScope("profile");
        client.AddAdminConsentScope("email");

        Assert.Equal(["profile", "email"], client.AdminConsentScopes.Select(s => s.Scope));
    }

    [Fact]
    public void AdminConsentScopesExposeClientId()
    {
        var client = CreateClient();
        client.AddAdminConsentScope("email");

        var consent = Assert.Single(client.AdminConsentScopes);
        Assert.Equal(client.Id, consent.ClientId);
        Assert.Equal("email", consent.Scope);
    }

    [Fact]
    public void AddRedirectUriDeduplicates()
    {
        var client = CreateClient();
        var uri = new Uri("https://client.example/callback");

        client.AddRedirectUri(uri);
        client.AddRedirectUri(uri);

        Assert.Equal([uri], client.RedirectUris.Select(r => r.RedirectUri));
    }

    [Fact]
    public void AddRedirectUriRejectsNull()
    {
        var client = CreateClient();

        Assert.Throws<ArgumentNullException>(() => client.AddRedirectUri(null!));
    }

    [Fact]
    public void AddPostLogoutRedirectUriDeduplicates()
    {
        var client = CreateClient();
        var uri = new Uri("https://client.example/");

        client.AddPostLogoutRedirectUri(uri);
        client.AddPostLogoutRedirectUri(uri);

        Assert.Equal([uri], client.PostLogoutRedirectUris.Select(r => r.RedirectUri));
    }

    [Fact]
    public void AddPostLogoutRedirectUriRejectsNull()
    {
        var client = CreateClient();

        Assert.Throws<ArgumentNullException>(() => client.AddPostLogoutRedirectUri(null!));
    }

    [Fact]
    public void SetSecretUpdatesHashAndBumpsUpdatedAt()
    {
        var client = CreateClient();
        var before = client.UpdatedAt;

        client.SetSecret("new-hash");

        Assert.Equal("new-hash", client.ClientSecretHash);
        Assert.True(client.UpdatedAt >= before);
    }

    [Fact]
    public void DeactivateAndActivateToggleActiveState()
    {
        var client = CreateClient();

        client.Deactivate();
        Assert.False(client.IsActive);

        client.Activate();
        Assert.True(client.IsActive);
    }
}