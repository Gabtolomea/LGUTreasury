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
            migrationBuilder.Sql(
                "ALTER TABLE `Payees` CHANGE COLUMN `Middlename` `MiddleName` LONGTEXT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE `Payees` CHANGE COLUMN `Lastname` `LastName` LONGTEXT NOT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE `Payees` CHANGE COLUMN `Firstname` `FirstName` LONGTEXT NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE `Payees` CHANGE COLUMN `MiddleName` `Middlename` LONGTEXT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE `Payees` CHANGE COLUMN `LastName` `Lastname` LONGTEXT NOT NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE `Payees` CHANGE COLUMN `FirstName` `Firstname` LONGTEXT NOT NULL;");
        }
    }
}
