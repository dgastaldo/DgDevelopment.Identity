using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DgDevelopment.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordExpirationAndMfaGraceTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MfaGracePeriodStartedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            // Existing users get "now" rather than a sentinel date, so this migration doesn't
            // retroactively mark every account's password as already expired on deploy day - the
            // 90-day expiration clock starts fresh for them from here, same "no surprise cutover"
            // philosophy as the MFA grace period.
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                table: "Users",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MfaGracePeriodStartedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "Users");
        }
    }
}
