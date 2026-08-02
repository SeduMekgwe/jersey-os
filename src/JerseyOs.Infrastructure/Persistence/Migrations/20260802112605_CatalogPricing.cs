using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JerseyOs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultCurrency",
                schema: "jersey",
                table: "Organizations",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "ZAR");

            migrationBuilder.AddColumn<decimal>(
                name: "PriceAmount",
                schema: "jersey",
                table: "catalog_product_variants",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCurrency",
                schema: "jersey",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "PriceAmount",
                schema: "jersey",
                table: "catalog_product_variants");
        }
    }
}
