using System;

namespace DgDevelopment.Identity.Domain.Entities;

public sealed class ClientRedirectUri
{
    public Guid ClientId { get; private set; }
    public Uri RedirectUri { get; private set; }

    private ClientRedirectUri() { }

    public ClientRedirectUri(Guid clientId, Uri redirectUri)
    {
        ArgumentNullException.ThrowIfNull(redirectUri);
        ClientId = clientId;
        RedirectUri = redirectUri;
    }
}
