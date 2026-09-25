using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGUTreasury.Migrations
{
    /// <inheritdoc />
    public partial class AddIsCollectedToPaymentRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CollectedConfirmedAt",
                table: "PaymentRecords",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCollected",
                table: "PaymentRecords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectedConfirmedAt",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "IsCollected",
                table: "PaymentRecords");
        }
    }
}
