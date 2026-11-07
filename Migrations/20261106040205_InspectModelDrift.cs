using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGUTreasury.Migrations
{
    public partial class InspectModelDrift : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // OfficialReceipt columns already exist in the database.
            // No schema changes are needed.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve the existing OfficialReceipt columns and their data.
        }
    }
}