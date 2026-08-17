using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JerseyOs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HostingPolish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_api_keys",
                schema: "jersey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SecretHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastUsedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_api_keys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_api_keys_OrganizationId_Prefix",
                schema: "jersey",
                table: "integration_api_keys",
                columns: new[] { "OrganizationId", "Prefix" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_api_keys_SecretHash",
                schema: "jersey",
                table: "integration_api_keys",
                column: "SecretHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_api_keys",
                schema: "jersey");
        }
    }
}
