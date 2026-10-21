using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faemiyah.Arranger.Data.Migrations
{
    /// <inheritdoc />
    public partial class Tabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsPollClosed",
                table: "Arrangements",
                newName: "IsScheduleDisabled");

            migrationBuilder.AddColumn<bool>(
                name: "IsExpensesDisabled",
                table: "Arrangements",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExpensesDisabled",
                table: "Arrangements");

            migrationBuilder.RenameColumn(
                name: "IsScheduleDisabled",
                table: "Arrangements",
                newName: "IsPollClosed");
        }
    }
}
