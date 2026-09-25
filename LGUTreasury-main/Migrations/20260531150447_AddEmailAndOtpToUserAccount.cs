using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGUTreasury.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailAndOtpToUserAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "UserAccounts",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtpCode",
                table: "UserAccounts",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpExpiry",
                table: "UserAccounts",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "OtpCode",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "OtpExpiry",
                table: "UserAccounts");
        }
    }
}
