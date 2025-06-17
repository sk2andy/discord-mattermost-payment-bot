using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace discord_payment_bot.Migrations
{
    /// <inheritdoc />
    public partial class DeactivationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeactivatedAt",
                table: "RoleAssignments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationAction",
                table: "RoleAssignments",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "RoleAssignments");

            migrationBuilder.DropColumn(
                name: "DeactivationAction",
                table: "RoleAssignments");
        }
    }
}
