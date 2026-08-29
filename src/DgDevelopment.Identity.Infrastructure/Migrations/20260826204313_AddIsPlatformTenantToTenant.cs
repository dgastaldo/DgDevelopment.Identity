using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DgDevelopment.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPlatformTenantToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformTenant",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // The bootstrap tenant that owns this Identity instance already exists on any
            // real environment (created by DbSeeder before this migration existed) - flag it
            // as the platform tenant so IsGlobalAdministratorAsync keeps working for its
            // SuperAdmin instead of silently stripping global-admin status from everyone.
            migrationBuilder.Sql("UPDATE Tenants SET IsPlatformTenant = 1 WHERE Slug = 'identity-tenant';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "IsPlatformTenant",
                table: "Tenants");
        }
    }
}
