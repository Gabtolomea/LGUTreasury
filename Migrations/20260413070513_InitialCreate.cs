using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace LGUTreasury.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Payees",
                columns: table => new
                {
                    PayeeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Firstname = table.Column<string>(type: "longtext", nullable: false),
                    Middlename = table.Column<string>(type: "longtext", nullable: true),
                    Lastname = table.Column<string>(type: "longtext", nullable: false),
                    Suffix = table.Column<string>(type: "longtext", nullable: true),
                    ContactNumber = table.Column<string>(type: "longtext", nullable: true),
                    ResidenceAddress = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payees", x => x.PayeeID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RevenueCategories",
                columns: table => new
                {
                    CategoryID = table.Column<string>(type: "varchar(255)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueCategories", x => x.CategoryID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeID = table.Column<string>(type: "longtext", nullable: false),
                    PasswordHash = table.Column<string>(type: "longtext", nullable: false),
                    Role = table.Column<string>(type: "longtext", nullable: false),
                    FirstName = table.Column<string>(type: "longtext", nullable: false),
                    MiddleName = table.Column<string>(type: "longtext", nullable: true),
                    LastName = table.Column<string>(type: "longtext", nullable: false),
                    Suffix = table.Column<string>(type: "longtext", nullable: true),
                    ContactNumber = table.Column<string>(type: "longtext", nullable: true),
                    Address = table.Column<string>(type: "longtext", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.UserID);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RevenueTypes",
                columns: table => new
                {
                    TypeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CategoryID = table.Column<string>(type: "varchar(255)", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false),
                    BaseRate = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsRecurring = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OrdinanceSection = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueTypes", x => x.TypeID);
                    table.ForeignKey(
                        name: "FK_RevenueTypes_RevenueCategories_CategoryID",
                        column: x => x.CategoryID,
                        principalTable: "RevenueCategories",
                        principalColumn: "CategoryID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaymentRecords",
                columns: table => new
                {
                    PaymentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OfficialReceipt = table.Column<string>(type: "longtext", nullable: false),
                    PayeeID = table.Column<int>(type: "int", nullable: false),
                    DateIssued = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CollectedBy_UserID = table.Column<int>(type: "int", nullable: false),
                    TotalBaseAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TotalSurcharge = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TotalInterest = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRecords", x => x.PaymentID);
                    table.ForeignKey(
                        name: "FK_PaymentRecords_Payees_PayeeID",
                        column: x => x.PayeeID,
                        principalTable: "Payees",
                        principalColumn: "PayeeID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentRecords_UserAccounts_CollectedBy_UserID",
                        column: x => x.CollectedBy_UserID,
                        principalTable: "UserAccounts",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RevenuePolicies",
                columns: table => new
                {
                    PolicyID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    TypeID = table.Column<int>(type: "int", nullable: false),
                    SurchargeRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Deadline = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenuePolicies", x => x.PolicyID);
                    table.ForeignKey(
                        name: "FK_RevenuePolicies_RevenueTypes_TypeID",
                        column: x => x.TypeID,
                        principalTable: "RevenueTypes",
                        principalColumn: "TypeID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RecordLineItems",
                columns: table => new
                {
                    LineItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PaymentID = table.Column<int>(type: "int", nullable: false),
                    TypeID = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    SurchargeAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    InterestAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordLineItems", x => x.LineItemID);
                    table.ForeignKey(
                        name: "FK_RecordLineItems_PaymentRecords_PaymentID",
                        column: x => x.PaymentID,
                        principalTable: "PaymentRecords",
                        principalColumn: "PaymentID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecordLineItems_RevenueTypes_TypeID",
                        column: x => x.TypeID,
                        principalTable: "RevenueTypes",
                        principalColumn: "TypeID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_CollectedBy_UserID",
                table: "PaymentRecords",
                column: "CollectedBy_UserID");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRecords_PayeeID",
                table: "PaymentRecords",
                column: "PayeeID");

            migrationBuilder.CreateIndex(
                name: "IX_RecordLineItems_PaymentID",
                table: "RecordLineItems",
                column: "PaymentID");

            migrationBuilder.CreateIndex(
                name: "IX_RecordLineItems_TypeID",
                table: "RecordLineItems",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_RevenuePolicies_TypeID",
                table: "RevenuePolicies",
                column: "TypeID");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueTypes_CategoryID",
                table: "RevenueTypes",
                column: "CategoryID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecordLineItems");

            migrationBuilder.DropTable(
                name: "RevenuePolicies");

            migrationBuilder.DropTable(
                name: "PaymentRecords");

            migrationBuilder.DropTable(
                name: "RevenueTypes");

            migrationBuilder.DropTable(
                name: "Payees");

            migrationBuilder.DropTable(
                name: "UserAccounts");

            migrationBuilder.DropTable(
                name: "RevenueCategories");
        }
    }
}
