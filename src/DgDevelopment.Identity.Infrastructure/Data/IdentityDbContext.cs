namespace DgDevelopment.Identity.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using DgDevelopment.Identity.Domain.Entities;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserEmail> UserEmails => Set<UserEmail>();
    public DbSet<UserClaim> UserClaims => Set<UserClaim>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupRole> GroupRoles => Set<GroupRole>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientGrantType> ClientGrantTypes => Set<ClientGrantType>();
    public DbSet<ClientScope> ClientScopes => Set<ClientScope>();
    public DbSet<ClientRedirectUri> ClientRedirectUris => Set<ClientRedirectUri>();
    public DbSet<ClientPostLogoutRedirectUri> ClientPostLogoutRedirectUris => Set<ClientPostLogoutRedirectUri>();
    public DbSet<AuthorizationCode> AuthorizationCodes => Set<AuthorizationCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DeviceCode> DeviceCodes => Set<DeviceCode>();
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();
    public DbSet<TotpSecret> TotpSecrets => Set<TotpSecret>();
    public DbSet<BackupCode> BackupCodes => Set<BackupCode>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DomainEvent> DomainEvents => Set<DomainEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        System.ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
