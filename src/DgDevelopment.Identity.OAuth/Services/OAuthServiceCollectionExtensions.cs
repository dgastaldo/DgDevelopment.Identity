namespace DgDevelopment.Identity.OAuth.Services;

using Microsoft.Extensions.DependencyInjection;

public static class OAuthServiceCollectionExtensions
{
    public static IServiceCollection AddOAuthEngine(this IServiceCollection services)
    {
        services.AddScoped<IKeyMaterialService, KeyMaterialService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IClientValidator, ClientValidator>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}
