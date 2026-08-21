using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DgDevelopment.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                table: "Roles");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserRoles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserPermissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserConsents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Roles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Platforms",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsGlobal",
                table: "Permissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Groups",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DeviceCodes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Clients",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AuthorizationCodes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AuditLogs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsOwner = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantMemberships_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantId",
                table: "UserRoles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_TenantId",
                table: "UserPermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserConsents_TenantId",
                table: "UserConsents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TenantId",
                table: "RefreshTokens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_TenantId",
                table: "Platforms",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_TenantId",
                table: "Groups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCodes_TenantId",
                table: "DeviceCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TenantId",
                table: "Clients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationCodes_TenantId",
                table: "AuthorizationCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_TenantId_UserId",
                table: "TenantMemberships",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_UserId",
                table: "TenantMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            // Seed a single "Default" tenant and backfill every pre-existing row (from before
            // multi-tenancy existed) onto it. On an empty database every UPDATE below is a no-op;
            // on the current dev/test database it reassigns the seeded system rows (SuperAdmin role,
            // SuperAdmins group, identity-platform client, IdentityAdmin platform, ...) so the
            // FK constraints added further down don't fail against the placeholder Guid.Empty that
            // the preceding AddColumn calls used as a default. DeviceCodes.TenantId is nullable and
            // intentionally left null here — it is only assigned once a device code is authorized.
            migrationBuilder.Sql("""
                DECLARE @DefaultTenantId UNIQUEIDENTIFIER = NEWID();
                DECLARE @Now DATETIME2 = SYSUTCDATETIME();

                INSERT INTO Tenants (Id, Name, Slug, IsActive, CreatedAt, UpdatedAt)
                VALUES (@DefaultTenantId, N'Default', N'default', 1, @Now, @Now);

                UPDATE AuditLogs SET TenantId = @DefaultTenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
                UPDATE AuthorizationCodes SET TenantId = @DefaultTenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
                UPDATE Clients SET TenantId = @DefaultTenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
                UPDATE Groups SET TenantId = @DefaultTenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
                UPDATE Platforms SET TenantId = @DefaultTenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
                UPDATE RefreshTokens SET TenantId = @DefaultTenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
                UPDATE Roles SET TenantId = @DefaultTenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
                UPDATE UserConsents SET TenantId = @DefaultTenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
                UPDATE UserPermissions SET TenantId = @DefaultTenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
                UPDATE UserRoles SET TenantId = @DefaultTenantId WHERE TenantId = '00000000-0000-0000-0000-000000000000';
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuthorizationCodes_Tenants_TenantId",
                table: "AuthorizationCodes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Tenants_TenantId",
                table: "Clients",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceCodes_Tenants_TenantId",
                table: "DeviceCodes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_Tenants_TenantId",
                table: "Groups",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Platforms_Tenants_TenantId",
                table: "Platforms",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Tenants_TenantId",
                table: "RefreshTokens",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Tenants_TenantId",
                table: "Roles",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserConsents_Tenants_TenantId",
                table: "UserConsents",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserPermissions_Tenants_TenantId",
                table: "UserPermissions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Tenants_TenantId",
                table: "UserRoles",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Tenants_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AuthorizationCodes_Tenants_TenantId",
                table: "AuthorizationCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Tenants_TenantId",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceCodes_Tenants_TenantId",
                table: "DeviceCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Groups_Tenants_TenantId",
                table: "Groups");

            migrationBuilder.DropForeignKey(
                name: "FK_Platforms_Tenants_TenantId",
                table: "Platforms");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Tenants_TenantId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Tenants_TenantId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserConsents_Tenants_TenantId",
                table: "UserConsents");

            migrationBuilder.DropForeignKey(
                name: "FK_UserPermissions_Tenants_TenantId",
                table: "UserPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Tenants_TenantId",
                table: "UserRoles");

            migrationBuilder.DropTable(
                name: "TenantMemberships");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_TenantId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserPermissions_TenantId",
                table: "UserPermissions");

            migrationBuilder.DropIndex(
                name: "IX_UserConsents_TenantId",
                table: "UserConsents");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TenantId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Platforms_TenantId",
                table: "Platforms");

            migrationBuilder.DropIndex(
                name: "IX_Groups_TenantId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_DeviceCodes_TenantId",
                table: "DeviceCodes");

            migrationBuilder.DropIndex(
                name: "IX_Clients_TenantId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_AuthorizationCodes_TenantId",
                table: "AuthorizationCodes");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserPermissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserConsents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Platforms");

            migrationBuilder.DropColumn(
                name: "IsGlobal",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DeviceCodes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuthorizationCodes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);
        }
    }
}
