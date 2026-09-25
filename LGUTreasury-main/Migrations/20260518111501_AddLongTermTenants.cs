using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace LGUTreasury.Migrations
{
    /// <inheritdoc />
    public partial class AddLongTermTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillingTypeOptions",
                columns: table => new
                {
                    BillingTypeOptionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingTypeOptions", x => x.BillingTypeOptionID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LongTermPayees",
                columns: table => new
                {
                    LongTermPayeeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    FirstName = table.Column<string>(type: "longtext", nullable: false),
                    MiddleName = table.Column<string>(type: "longtext", nullable: true),
                    LastName = table.Column<string>(type: "longtext", nullable: false),
                    Suffix = table.Column<string>(type: "longtext", nullable: true),
                    ContactNumber = table.Column<string>(type: "longtext", nullable: true),
                    Address = table.Column<string>(type: "longtext", nullable: true),
                    StartMonth = table.Column<string>(type: "longtext", nullable: false),
                    BillGenerationDay = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LongTermPayees", x => x.LongTermPayeeID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AccountBillingTypes",
                columns: table => new
                {
                    AccountBillingTypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    LongTermPayeeID = table.Column<int>(type: "int", nullable: false),
                    BillingTypeName = table.Column<string>(type: "longtext", nullable: false),
                    MonthlyRate = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBillingTypes", x => x.AccountBillingTypeID);
                    table.ForeignKey(
                        name: "FK_AccountBillingTypes_LongTermPayees_LongTermPayeeID",
                        column: x => x.LongTermPayeeID,
                        principalTable: "LongTermPayees",
                        principalColumn: "LongTermPayeeID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MonthlyBills",
                columns: table => new
                {
                    MonthlyBillID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    LongTermPayeeID = table.Column<int>(type: "int", nullable: false),
                    AccountBillingTypeID = table.Column<int>(type: "int", nullable: false),
                    BillingMonth = table.Column<string>(type: "longtext", nullable: true),
                    BillingType = table.Column<string>(type: "longtext", nullable: true),
                    BilledAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ORNumber = table.Column<string>(type: "longtext", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyBills", x => x.MonthlyBillID);
                    table.ForeignKey(
                        name: "FK_MonthlyBills_AccountBillingTypes_AccountBillingTypeID",
                        column: x => x.AccountBillingTypeID,
                        principalTable: "AccountBillingTypes",
                        principalColumn: "AccountBillingTypeID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MonthlyBills_LongTermPayees_LongTermPayeeID",
                        column: x => x.LongTermPayeeID,
                        principalTable: "LongTermPayees",
                        principalColumn: "LongTermPayeeID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBillingTypes_LongTermPayeeID",
                table: "AccountBillingTypes",
                column: "LongTermPayeeID");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBills_AccountBillingTypeID",
                table: "MonthlyBills",
                column: "AccountBillingTypeID");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyBills_LongTermPayeeID",
                table: "MonthlyBills",
                column: "LongTermPayeeID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingTypeOptions");

            migrationBuilder.DropTable(
                name: "MonthlyBills");

            migrationBuilder.DropTable(
                name: "AccountBillingTypes");

            migrationBuilder.DropTable(
                name: "LongTermPayees");
        }
    }
}
