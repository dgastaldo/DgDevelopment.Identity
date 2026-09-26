using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DgDevelopment.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDevicePollTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.AddColumn<DateTime>(
                name: "LastPolledAt",
                table: "DeviceCodes",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.DropColumn(
                name: "LastPolledAt",
                table: "DeviceCodes");
        }
    }
}
