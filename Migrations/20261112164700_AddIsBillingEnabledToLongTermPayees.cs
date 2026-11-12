using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGUTreasury.Migrations
{
    public partial class AddIsBillingEnabledToLongTermPayees : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBillingEnabled",
                table: "LongTermPayees",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBillingEnabled",
                table: "LongTermPayees");
        }
    }
}
