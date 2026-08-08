using System;

namespace DgDevelopment.Identity.Domain.Entities;

public enum ClientType
{
    Confidential,
    Public
}

public sealed class Client
{
    public Guid Id { get; private set; }
    public string ClientId { get; private set; }
    public string ClientSecretHash { get; private set; }
    public Guid? PlatformId { get; private set; }
    public string Name { get; private set; }
    public ClientType ClientType { get; private set; }
    public bool RequirePkce { get; private set; }
    public bool RequireConsent { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<ClientGrantType> _grantTypes = [];
    private readonly List<ClientScope> _scopes = [];
    private readonly List<ClientRedirectUri> _redirectUris = [];
    private readonly List<ClientPostLogoutRedirectUri> _postLogoutRedirectUris = [];

    public IReadOnlyCollection<ClientGrantType> GrantTypes => _grantTypes.AsReadOnly();
    public IReadOnlyCollection<ClientScope> Scopes => _scopes.AsReadOnly();
    public IReadOnlyCollection<ClientRedirectUri> RedirectUris => _redirectUris.AsReadOnly();
    public IReadOnlyCollection<ClientPostLogoutRedirectUri> PostLogoutRedirectUris => _postLogoutRedirectUris.AsReadOnly();

    private Client() { }

    public Client(string clientId, string clientSecretHash, string name, ClientType clientType, Guid? platformId = null)
    {
        Id = Guid.NewGuid();
        ClientId = clientId;
        ClientSecretHash = clientSecretHash;
        PlatformId = platformId;
        Name = name;
        ClientType = clientType;
        RequirePkce = clientType == ClientType.Public;
        RequireConsent = true;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSecret(string secretHash)
    {
        ClientSecretHash = secretHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddGrantType(string grantType)
    {
        if (_grantTypes.Any(g => g.GrantType == grantType))
            return;

        _grantTypes.Add(new ClientGrantType(Id, grantType));
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddScope(string scope)
    {
        if (_scopes.Any(s => s.Scope == scope))
            return;

        _scopes.Add(new ClientScope(Id, scope));
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddRedirectUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (_redirectUris.Any(r => r.RedirectUri == uri))
            return;

        _redirectUris.Add(new ClientRedirectUri(Id, uri));
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddPostLogoutRedirectUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (_postLogoutRedirectUris.Any(r => r.RedirectUri == uri))
            return;

        _postLogoutRedirectUris.Add(new ClientPostLogoutRedirectUri(Id, uri));
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
