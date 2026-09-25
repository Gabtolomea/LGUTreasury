using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace LGUTreasury.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PaymentRecords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DeletedRecords",
                columns: table => new
                {
                    DeletedRecordID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PaymentID = table.Column<int>(type: "int", nullable: false),
                    PaymentRecordPaymentID = table.Column<int>(type: "int", nullable: true),
                    PayeeName = table.Column<string>(type: "longtext", nullable: true),
                    CollectorName = table.Column<string>(type: "longtext", nullable: true),
                    CollectionType = table.Column<string>(type: "longtext", nullable: true),
                    DeletedBy_UserID = table.Column<int>(type: "int", nullable: false),
                    DeletedByUserID = table.Column<int>(type: "int", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedRecords", x => x.DeletedRecordID);
                    table.ForeignKey(
                        name: "FK_DeletedRecords_PaymentRecords_PaymentRecordPaymentID",
                        column: x => x.PaymentRecordPaymentID,
                        principalTable: "PaymentRecords",
                        principalColumn: "PaymentID");
                    table.ForeignKey(
                        name: "FK_DeletedRecords_UserAccounts_DeletedByUserID",
                        column: x => x.DeletedByUserID,
                        principalTable: "UserAccounts",
                        principalColumn: "UserID");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedRecords_DeletedByUserID",
                table: "DeletedRecords",
                column: "DeletedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_DeletedRecords_PaymentRecordPaymentID",
                table: "DeletedRecords",
                column: "PaymentRecordPaymentID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedRecords");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PaymentRecords");
        }
    }
}
