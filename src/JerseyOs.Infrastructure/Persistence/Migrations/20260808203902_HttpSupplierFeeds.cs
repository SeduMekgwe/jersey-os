using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JerseyOs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HttpSupplierFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeedBearerToken",
                schema: "jersey",
                table: "import_suppliers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedFormat",
                schema: "jersey",
                table: "import_suppliers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "csv");

            migrationBuilder.AddColumn<string>(
                name: "FeedKind",
                schema: "jersey",
                table: "import_suppliers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "upload");

            migrationBuilder.AddColumn<string>(
                name: "FeedUrl",
                schema: "jersey",
                table: "import_suppliers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncAtUtc",
                schema: "jersey",
                table: "import_suppliers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSyncError",
                schema: "jersey",
                table: "import_suppliers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSyncStatus",
                schema: "jersey",
                table: "import_suppliers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SyncCron",
                schema: "jersey",
                table: "import_suppliers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedBearerToken",
                schema: "jersey",
                table: "import_suppliers");

            migrationBuilder.DropColumn(
                name: "FeedFormat",
                schema: "jersey",
                table: "import_suppliers");

            migrationBuilder.DropColumn(
                name: "FeedKind",
                schema: "jersey",
                table: "import_suppliers");

            migrationBuilder.DropColumn(
                name: "FeedUrl",
                schema: "jersey",
                table: "import_suppliers");

            migrationBuilder.DropColumn(
                name: "LastSyncAtUtc",
                schema: "jersey",
                table: "import_suppliers");

            migrationBuilder.DropColumn(
                name: "LastSyncError",
                schema: "jersey",
                table: "import_suppliers");

            migrationBuilder.DropColumn(
                name: "LastSyncStatus",
                schema: "jersey",
                table: "import_suppliers");

            migrationBuilder.DropColumn(
                name: "SyncCron",
                schema: "jersey",
                table: "import_suppliers");
        }
    }
}
