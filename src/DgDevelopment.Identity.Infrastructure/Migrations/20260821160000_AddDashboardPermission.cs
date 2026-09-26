using Microsoft.EntityFrameworkCore.Migrations;

namespace DgDevelopment.Identity.Infrastructure.Migrations;

public partial class AddDashboardSummary : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql("DECLARE @permissionId uniqueidentifier = NEWID(); IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Name] = 'identity-platform.dashboard.read') BEGIN INSERT INTO [Permissions] ([Id], [Name], [Description], [ResourceType]) VALUES (@permissionId, 'identity-platform.dashboard.read', 'View identity platform dashboard', 'Dashboard'); END ELSE BEGIN SELECT @permissionId = [Id] FROM [Permissions] WHERE [Name] = 'identity-platform.dashboard.read'; END; INSERT INTO [RolePermissions] ([RoleId], [PermissionId], [ScopeType], [ScopeValue]) SELECT [r].[Id], @permissionId, NULL, NULL FROM [Roles] AS [r] WHERE [r].[Name] = 'SuperAdmin' AND NOT EXISTS (SELECT 1 FROM [RolePermissions] WHERE [RoleId] = [r].[Id] AND [PermissionId] = @permissionId)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql("DELETE FROM [RolePermissions] WHERE [PermissionId] IN (SELECT [Id] FROM [Permissions] WHERE [Name] = 'identity-platform.dashboard.read'); DELETE FROM [Permissions] WHERE [Name] = 'identity-platform.dashboard.read'");
    }
}
