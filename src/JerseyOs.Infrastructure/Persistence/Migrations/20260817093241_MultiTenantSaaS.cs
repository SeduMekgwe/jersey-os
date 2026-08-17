using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JerseyOs.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenantSaaS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_invitations",
                schema: "jersey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_invitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_quotas",
                schema: "jersey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaxProducts = table.Column<int>(type: "int", nullable: false),
                    MaxMembers = table.Column<int>(type: "int", nullable: false),
                    MaxImportBatchesPerDay = table.Column<int>(type: "int", nullable: false),
                    MaxAiGenerationsPerDay = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_quotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_settings",
                schema: "jersey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_OrganizationId_Email",
                schema: "jersey",
                table: "organization_invitations",
                columns: new[] { "OrganizationId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_TokenHash",
                schema: "jersey",
                table: "organization_invitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_quotas_OrganizationId",
                schema: "jersey",
                table: "organization_quotas",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_settings_OrganizationId_Key",
                schema: "jersey",
                table: "organization_settings",
                columns: new[] { "OrganizationId", "Key" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO jersey.organization_quotas
                    (Id, OrganizationId, MaxProducts, MaxMembers, MaxImportBatchesPerDay, MaxAiGenerationsPerDay, CreatedAtUtc, CreatedBy, ModifiedAtUtc, ModifiedBy)
                SELECT NEWID(), o.Id, 5000, 25, 50, 200, SYSUTCDATETIME(), N'system', SYSUTCDATETIME(), N'system'
                FROM jersey.Organizations AS o
                WHERE NOT EXISTS (
                    SELECT 1 FROM jersey.organization_quotas AS q WHERE q.OrganizationId = o.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_invitations",
                schema: "jersey");

            migrationBuilder.DropTable(
                name: "organization_quotas",
                schema: "jersey");

            migrationBuilder.DropTable(
                name: "organization_settings",
                schema: "jersey");
        }
    }
}
