using DgDevelopment.Identity.Client.Blazor;
using DgDevelopment.Identity.Client.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

public static class IdentityBlazorExtensions
{
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, OidcOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<ISessionStorageService, BrowserSessionStorage>();
        services.AddScoped<ITokenStore, SessionStorageTokenStore>();
        services.AddScoped<IdentityClient>();
        services.AddScoped<IdentityAuthStateProvider>();
        services.AddScoped<IdentityRefreshHandler>();

        return services;
    }
}
