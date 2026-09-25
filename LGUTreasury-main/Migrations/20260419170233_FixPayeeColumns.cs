using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGUTreasury.Migrations
{
    /// <inheritdoc />
    public partial class FixPayeeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Middlename",
                table: "Payees",
                newName: "MiddleName");

            migrationBuilder.RenameColumn(
                name: "Lastname",
                table: "Payees",
                newName: "LastName");

            migrationBuilder.RenameColumn(
                name: "Firstname",
                table: "Payees",
                newName: "FirstName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MiddleName",
                table: "Payees",
                newName: "Middlename");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "Payees",
                newName: "Lastname");

            migrationBuilder.RenameColumn(
                name: "FirstName",
                table: "Payees",
                newName: "Firstname");
        }
    }
}
