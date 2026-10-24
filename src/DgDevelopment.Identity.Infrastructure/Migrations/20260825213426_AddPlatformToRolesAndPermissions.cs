using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DgDevelopment.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformToRolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Name",
                table: "Permissions");

            migrationBuilder.AddColumn<Guid>(
                name: "PlatformId",
                table: "Roles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PlatformId",
                table: "Permissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Permissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill: existing rows got a Guid.Empty placeholder above (nothing to backfill from,
            // Role/Permission never carried a PlatformId before, and Permission never carried a
            // TenantId either). Assumes exactly the shape of data this system has ever actually had -
            // one "IdentityAdmin" platform per tenant, and every existing Permission used by only one
            // tenant's roles (true today; there is no multi-tenant-shared-permission-row case to
            // handle). A permission not yet referenced by any role falls back to the first tenant
            // found, which only matters for a from-scratch database with no real usage yet.
            migrationBuilder.Sql("""
                UPDATE r
                SET PlatformId = p.Id
                FROM Roles r
                INNER JOIN Platforms p ON p.TenantId = r.TenantId AND p.Name = N'IdentityAdmin';

                UPDATE perm
                SET TenantId = COALESCE(
                    (SELECT TOP 1 ro.TenantId FROM RolePermissions rp INNER JOIN Roles ro ON ro.Id = rp.RoleId WHERE rp.PermissionId = perm.Id),
                    (SELECT TOP 1 Id FROM Tenants))
                FROM Permissions perm;

                UPDATE perm
                SET PlatformId = p.Id
                FROM Permissions perm
                INNER JOIN Platforms p ON p.TenantId = perm.TenantId AND p.Name = N'IdentityAdmin';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_PlatformId",
                table: "Roles",
                column: "PlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PlatformId",
                table: "Permissions",
                column: "PlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TenantId_Name",
                table: "Permissions",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_Platforms_PlatformId",
                table: "Permissions",
                column: "PlatformId",
                principalTable: "Platforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_Tenants_TenantId",
                table: "Permissions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Platforms_PlatformId",
                table: "Roles",
                column: "PlatformId",
                principalTable: "Platforms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_Platforms_PlatformId",
                table: "Permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_Tenants_TenantId",
                table: "Permissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Platforms_PlatformId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_PlatformId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_PlatformId",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_TenantId_Name",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "PlatformId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "PlatformId",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Permissions");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true);
        }
    }
}
