using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace discord_payment_bot.Migrations
{
    /// <inheritdoc />
    public partial class Transactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WiseTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", nullable: true),
                    PaymentReference = table.Column<string>(type: "TEXT", nullable: true),
                    PaymentDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", nullable: false),
                    SenderAccount = table.Column<string>(type: "TEXT", nullable: true),
                    TransactionId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WiseTransactions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WiseTransactions");
        }
    }
}
