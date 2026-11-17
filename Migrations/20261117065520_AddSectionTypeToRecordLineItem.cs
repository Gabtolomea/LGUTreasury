using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGUTreasury.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionTypeToRecordLineItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SectionType",
                table: "RecordLineItems",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SectionType",
                table: "RecordLineItems");
        }
    }
}
