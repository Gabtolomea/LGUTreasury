using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGUTreasury.Migrations
{
    /// <inheritdoc />
    public partial class AddReportDownloadDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeneratedByName",
                table: "ReportLog",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FromDate",
                table: "ReportLog",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevenueTypeID",
                table: "ReportLog",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportKind",
                table: "ReportLog",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ToDate",
                table: "ReportLog",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeneratedByName",
                table: "ReportLog");

            migrationBuilder.DropColumn(
                name: "FromDate",
                table: "ReportLog");

            migrationBuilder.DropColumn(
                name: "RevenueTypeID",
                table: "ReportLog");

            migrationBuilder.DropColumn(
                name: "ReportKind",
                table: "ReportLog");

            migrationBuilder.DropColumn(
                name: "ToDate",
                table: "ReportLog");
        }
    }
}
