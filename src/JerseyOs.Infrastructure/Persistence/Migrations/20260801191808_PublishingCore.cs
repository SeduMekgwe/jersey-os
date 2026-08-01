using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JerseyOs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublishingCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "publish_sales_channels",
                schema: "jersey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publish_sales_channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "publish_external_ids",
                schema: "jersey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LocalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publish_external_ids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_publish_external_ids_publish_sales_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "jersey",
                        principalTable: "publish_sales_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "publish_runs",
                schema: "jersey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publish_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_publish_runs_publish_sales_channels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "jersey",
                        principalTable: "publish_sales_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_publish_external_ids_ChannelId",
                schema: "jersey",
                table: "publish_external_ids",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_publish_external_ids_OrganizationId_ChannelId_EntityType_LocalId",
                schema: "jersey",
                table: "publish_external_ids",
                columns: new[] { "OrganizationId", "ChannelId", "EntityType", "LocalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_publish_external_ids_OrganizationId_ChannelId_ExternalId",
                schema: "jersey",
                table: "publish_external_ids",
                columns: new[] { "OrganizationId", "ChannelId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_publish_runs_ChannelId",
                schema: "jersey",
                table: "publish_runs",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_publish_runs_OrganizationId_ChannelId_ProductId",
                schema: "jersey",
                table: "publish_runs",
                columns: new[] { "OrganizationId", "ChannelId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_publish_sales_channels_OrganizationId_Code",
                schema: "jersey",
                table: "publish_sales_channels",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "publish_external_ids",
                schema: "jersey");

            migrationBuilder.DropTable(
                name: "publish_runs",
                schema: "jersey");

            migrationBuilder.DropTable(
                name: "publish_sales_channels",
                schema: "jersey");
        }
    }
}
