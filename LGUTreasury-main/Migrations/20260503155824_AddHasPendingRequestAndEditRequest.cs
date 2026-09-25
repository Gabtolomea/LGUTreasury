using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace LGUTreasury.Migrations
{
    /// <inheritdoc />
    public partial class AddHasPendingRequestAndEditRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasPendingRequest",
                table: "PaymentRecords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EditRequest",
                columns: table => new
                {
                    RequestID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PaymentID = table.Column<int>(type: "int", nullable: false),
                    RequestedBy_UserID = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "longtext", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false),
                    ReviewedBy_UserID = table.Column<int>(type: "int", nullable: true),
                    ReviewNote = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditRequest", x => x.RequestID);
                    table.ForeignKey(
                        name: "FK_EditRequest_PaymentRecords_PaymentID",
                        column: x => x.PaymentID,
                        principalTable: "PaymentRecords",
                        principalColumn: "PaymentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EditRequest_UserAccounts_RequestedBy_UserID",
                        column: x => x.RequestedBy_UserID,
                        principalTable: "UserAccounts",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EditRequest_UserAccounts_ReviewedBy_UserID",
                        column: x => x.ReviewedBy_UserID,
                        principalTable: "UserAccounts",
                        principalColumn: "UserID");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EditRequest_PaymentID",
                table: "EditRequest",
                column: "PaymentID");

            migrationBuilder.CreateIndex(
                name: "IX_EditRequest_RequestedBy_UserID",
                table: "EditRequest",
                column: "RequestedBy_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_EditRequest_ReviewedBy_UserID",
                table: "EditRequest",
                column: "ReviewedBy_UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EditRequest");

            migrationBuilder.DropColumn(
                name: "HasPendingRequest",
                table: "PaymentRecords");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "PaymentRecords");
        }
    }
}
