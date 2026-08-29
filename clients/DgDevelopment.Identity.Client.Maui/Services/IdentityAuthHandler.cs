namespace DgDevelopment.Identity.Client.Maui.Services;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;

/// <summary>
/// Attaches whatever access token is currently in storage to every outgoing request, and clears
/// it on a 401 - mirrors <c>Client.Blazor</c>'s <c>IdentityRefreshHandler</c> exactly (attach +
/// clear-on-401, no proactive refresh here). Depends only on <see cref="ITokenStore"/> rather
/// than <see cref="AuthSession"/> specifically to avoid a circular dependency: this handler is
/// wrapped around the same <c>HttpClient</c> that <c>IdentityClient</c> uses, and
/// <see cref="AuthSession"/> itself needs an <c>IdentityClient</c> to exchange/refresh codes.
/// Proactive refresh-before-expiry instead happens in <see cref="AuthSession.EnsureFreshTokenAsync"/>,
/// called explicitly by pages before they make authenticated calls.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "Constructed via DI (AddTransient<IdentityAuthHandler>() + AddHttpMessageHandler<IdentityAuthHandler>() in MauiProgram.cs), not by direct instantiation.")]
internal sealed class IdentityAuthHandler(ITokenStore tokenStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tokens = await tokenStore.GetTokensAsync().ConfigureAwait(false);
        if (tokens is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            await tokenStore.ClearTokensAsync().ConfigureAwait(false);

        return response;
    }
}
