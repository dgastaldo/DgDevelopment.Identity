using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DgDevelopment.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixDefaultTenantSeeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            // AddMultiTenancy unconditionally created a "default"-slug tenant with a random
            // NEWID() on every database it ran against, including brand-new ones with nothing
            // to backfill - conflating a one-time legacy-upgrade concern with fresh-install
            // seeding (which DbSeeder.SeedDefaultTenantAsync already owns). Fix without touching
            // the already-applied old migration: if the "default" tenant actually holds
            // backfilled rows, this is a genuine upgraded database - rename it to the new
            // "identity-tenant" slug. If nothing references it, it was a spurious fresh-DB
            // artifact - delete it and let DbSeeder create the real one.
            migrationBuilder.Sql("""
                DECLARE @DefaultTenantId UNIQUEIDENTIFIER = (SELECT Id FROM Tenants WHERE Slug = N'default');

                IF @DefaultTenantId IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM AuditLogs WHERE TenantId = @DefaultTenantId)
                        OR EXISTS (SELECT 1 FROM AuthorizationCodes WHERE TenantId = @DefaultTenantId)
                        OR EXISTS (SELECT 1 FROM Clients WHERE TenantId = @DefaultTenantId)
                        OR EXISTS (SELECT 1 FROM Groups WHERE TenantId = @DefaultTenantId)
                        OR EXISTS (SELECT 1 FROM Platforms WHERE TenantId = @DefaultTenantId)
                        OR EXISTS (SELECT 1 FROM RefreshTokens WHERE TenantId = @DefaultTenantId)
                        OR EXISTS (SELECT 1 FROM Roles WHERE TenantId = @DefaultTenantId)
                        OR EXISTS (SELECT 1 FROM UserConsents WHERE TenantId = @DefaultTenantId)
                        OR EXISTS (SELECT 1 FROM UserPermissions WHERE TenantId = @DefaultTenantId)
                        OR EXISTS (SELECT 1 FROM UserRoles WHERE TenantId = @DefaultTenantId)
                    BEGIN
                        UPDATE Tenants SET Name = N'Identity Tenant', Slug = N'identity-tenant', UpdatedAt = SYSUTCDATETIME()
                        WHERE Id = @DefaultTenantId;
                    END
                    ELSE
                    BEGIN
                        DELETE FROM Tenants WHERE Id = @DefaultTenantId;
                    END
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not meaningfully reversible: renaming/deleting the tenant this migration acted on
            // can't be undone without knowing which case applied. No schema changes to revert.
        }
    }
}
