using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGUTreasury.Migrations
{
    public partial class AddMissingPaymentMethodColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "PaymentRecords",
                type: "longtext",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "PaymentRecords");
        }
    }
}
