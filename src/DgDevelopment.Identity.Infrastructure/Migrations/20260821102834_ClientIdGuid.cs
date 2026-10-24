using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DgDevelopment.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClientIdGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId_New",
                table: "Clients",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("UPDATE [Clients] SET [ClientId_New] = NEWID()");
            migrationBuilder.DropIndex(name: "IX_Clients_ClientId", table: "Clients");
            migrationBuilder.DropColumn(name: "ClientId", table: "Clients");
            migrationBuilder.RenameColumn(name: "ClientId_New", table: "Clients", newName: "ClientId");
            migrationBuilder.AlterColumn<Guid>(name: "ClientId", table: "Clients", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.CreateIndex(name: "IX_Clients_ClientId", table: "Clients", column: "ClientId", unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<string>(name: "ClientId_Old", table: "Clients", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.Sql("UPDATE [Clients] SET [ClientId_Old] = CONVERT(nvarchar(36), [ClientId])");
            migrationBuilder.DropIndex(name: "IX_Clients_ClientId", table: "Clients");
            migrationBuilder.DropColumn(name: "ClientId", table: "Clients");
            migrationBuilder.RenameColumn(name: "ClientId_Old", table: "Clients", newName: "ClientId");
            migrationBuilder.AlterColumn<string>(name: "ClientId", table: "Clients", type: "nvarchar(200)", maxLength: 200, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(200)", oldMaxLength: 200, oldNullable: true);
            migrationBuilder.CreateIndex(name: "IX_Clients_ClientId", table: "Clients", column: "ClientId", unique: true);
        }
    }
}
