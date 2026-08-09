using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JerseyOs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplierScrapeFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScrapePassword",
                schema: "jersey",
                table: "import_suppliers",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScrapeProfileJson",
                schema: "jersey",
                table: "import_suppliers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScrapeUsername",
                schema: "jersey",
                table: "import_suppliers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "import_supplier_scrape_runs",
                schema: "jersey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProductsScraped = table.Column<int>(type: "int", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_supplier_scrape_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_supplier_scrape_runs_import_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "jersey",
                        principalTable: "import_suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_supplier_scrape_runs_OrganizationId_SupplierId_CreatedAtUtc",
                schema: "jersey",
                table: "import_supplier_scrape_runs",
                columns: new[] { "OrganizationId", "SupplierId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_import_supplier_scrape_runs_SupplierId",
                schema: "jersey",
                table: "import_supplier_scrape_runs",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_supplier_scrape_runs",
                schema: "jersey");

            migrationBuilder.DropColumn(
                name: "ScrapePassword",
                schema: "jersey",
                table: "import_suppliers");

            migrationBuilder.DropColumn(
                name: "ScrapeProfileJson",
                schema: "jersey",
                table: "import_suppliers");

            migrationBuilder.DropColumn(
                name: "ScrapeUsername",
                schema: "jersey",
                table: "import_suppliers");
        }
    }
}
