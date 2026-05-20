using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGUTreasury.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProposedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProposedAmount",
                table: "Editrequests");

            migrationBuilder.DropColumn(
                name: "ProposedDate",
                table: "Editrequests");

            migrationBuilder.DropColumn(
                name: "ProposedOR",
                table: "Editrequests");

            migrationBuilder.DropColumn(
                name: "ProposedPaymentMethod",
                table: "Editrequests");

            migrationBuilder.DropColumn(
                name: "ProposedRemarks",
                table: "Editrequests");

            migrationBuilder.DropColumn(
                name: "ProposedTypeID",
                table: "Editrequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ProposedAmount",
                table: "Editrequests",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProposedDate",
                table: "Editrequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedOR",
                table: "Editrequests",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedPaymentMethod",
                table: "Editrequests",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedRemarks",
                table: "Editrequests",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProposedTypeID",
                table: "Editrequests",
                type: "int",
                nullable: true);
        }
    }
}
