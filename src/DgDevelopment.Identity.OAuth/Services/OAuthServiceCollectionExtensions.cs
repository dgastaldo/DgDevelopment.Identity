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
        services.AddScoped<IDeviceAuthorizationService, DeviceAuthorizationService>();
        services.AddScoped<ITokenIntrospectionService, TokenIntrospectionService>();
        services.AddScoped<ITokenRevocationService, TokenRevocationService>();

        return services;
    }
}
