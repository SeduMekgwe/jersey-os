using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JerseyOs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShopifyInventoryWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "publish_webhook_deliveries",
                schema: "jersey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WebhookId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publish_webhook_deliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_publish_webhook_deliveries_OrganizationId_CreatedAtUtc",
                schema: "jersey",
                table: "publish_webhook_deliveries",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_publish_webhook_deliveries_OrganizationId_WebhookId",
                schema: "jersey",
                table: "publish_webhook_deliveries",
                columns: new[] { "OrganizationId", "WebhookId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "publish_webhook_deliveries",
                schema: "jersey");
        }
    }
}
