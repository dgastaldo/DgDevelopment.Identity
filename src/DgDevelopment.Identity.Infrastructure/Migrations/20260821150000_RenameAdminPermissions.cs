using Microsoft.EntityFrameworkCore.Migrations;

namespace DgDevelopment.Identity.Infrastructure.Migrations;

public partial class RenameAdminPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql("UPDATE [Permissions] SET [Name] = CONCAT('identity-platform.', REPLACE([Name], ':', '.')) WHERE [Name] LIKE '%:%'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.Sql("UPDATE [Permissions] SET [Name] = REPLACE(REPLACE([Name], 'identity-platform.', ''), '.', ':') WHERE [Name] LIKE 'identity-platform.%'");
    }
}
