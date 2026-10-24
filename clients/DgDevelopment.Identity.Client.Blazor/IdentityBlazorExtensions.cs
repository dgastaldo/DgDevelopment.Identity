using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.Client.Core;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityBlazorExtensions
{
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, OidcOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<ISessionStorageService, BrowserSessionStorage>();
        services.AddScoped<ITokenStore, SessionStorageTokenStore>();
        services.AddScoped<ISessionMarkerService, SessionMarkerService>();
        services.AddScoped<SessionEventClient>();
        services.AddAuthorizationCore();
        services.AddScoped<IdentityAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<IdentityAuthStateProvider>());
        services.AddScoped<IdentityRefreshHandler>(sp => new IdentityRefreshHandler(
            sp.GetRequiredService<ITokenStore>(),
            sp.GetRequiredService<IdentityAuthStateProvider>));
        services.AddCascadingAuthenticationState();

        return services;
    }
}
