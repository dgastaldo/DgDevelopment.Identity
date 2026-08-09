namespace DgDevelopment.Identity.Infrastructure.Data;

using DgDevelopment.Identity.Domain.Repositories;
using DgDevelopment.Identity.Domain.Services;
using DgDevelopment.Identity.Infrastructure.Repositories;
using DgDevelopment.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IAuthorizationCodeRepository, AuthorizationCodeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IDeviceCodeRepository, DeviceCodeRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IUserConsentRepository, UserConsentRepository>();
        services.AddScoped<ISigningKeyRepository, SigningKeyRepository>();
        services.AddScoped<ITotpSecretRepository, TotpSecretRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IPlatformRepository, PlatformRepository>();

        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();

        return services;
    }
}
