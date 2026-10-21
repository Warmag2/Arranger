using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faemiyah.Arranger.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConversionLoss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LossPercent",
                table: "ExchangeRates",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LossPercent",
                table: "ExchangeRates");
        }
    }
}
