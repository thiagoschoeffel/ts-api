using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationExternalIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "external_integration_secrets",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    ProtectedValue = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_integration_secrets", x => x.Id);
                    table.UniqueConstraint("AK_external_integration_secrets_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_external_integration_secrets_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_integration_connections",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssetLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccessTokenSecretId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppSecretSecretId = table.Column<Guid>(type: "uuid", nullable: false),
                    WebhookVerifyTokenSecretId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreeServiceMessageLimit = table.Column<int>(type: "integer", nullable: false),
                    AutomationPauseAt = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Health = table.Column<int>(type: "integer", nullable: false),
                    LastHealthCheckAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastHealthError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_integration_connections", x => x.Id);
                    table.UniqueConstraint("AK_external_integration_connections_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_external_integration_connections_external_integration_secre~",
                        columns: x => new { x.OrganizationId, x.AccessTokenSecretId },
                        principalSchema: "app",
                        principalTable: "external_integration_secrets",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_integration_connections_external_integration_secr~1",
                        columns: x => new { x.OrganizationId, x.AppSecretSecretId },
                        principalSchema: "app",
                        principalTable: "external_integration_secrets",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_integration_connections_external_integration_secr~2",
                        columns: x => new { x.OrganizationId, x.WebhookVerifyTokenSecretId },
                        principalSchema: "app",
                        principalTable: "external_integration_secrets",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_external_integration_connections_organizations_Organization~",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_integration_webhook_receipts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalEventId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_integration_webhook_receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_external_integration_webhook_receipts_external_integration_~",
                        columns: x => new { x.OrganizationId, x.ConnectionId },
                        principalSchema: "app",
                        principalTable: "external_integration_connections",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_external_integration_connections_OrganizationId_AccessToken~",
                schema: "app",
                table: "external_integration_connections",
                columns: new[] { "OrganizationId", "AccessTokenSecretId" });

            migrationBuilder.CreateIndex(
                name: "IX_external_integration_connections_OrganizationId_AppSecretSe~",
                schema: "app",
                table: "external_integration_connections",
                columns: new[] { "OrganizationId", "AppSecretSecretId" });

            migrationBuilder.CreateIndex(
                name: "IX_external_integration_connections_OrganizationId_Provider",
                schema: "app",
                table: "external_integration_connections",
                columns: new[] { "OrganizationId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_integration_connections_OrganizationId_WebhookVeri~",
                schema: "app",
                table: "external_integration_connections",
                columns: new[] { "OrganizationId", "WebhookVerifyTokenSecretId" });

            migrationBuilder.CreateIndex(
                name: "IX_external_integration_connections_Provider_AssetId",
                schema: "app",
                table: "external_integration_connections",
                columns: new[] { "Provider", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_integration_webhook_receipts_ConnectionId_External~",
                schema: "app",
                table: "external_integration_webhook_receipts",
                columns: new[] { "ConnectionId", "ExternalEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_integration_webhook_receipts_OrganizationId_Connec~",
                schema: "app",
                table: "external_integration_webhook_receipts",
                columns: new[] { "OrganizationId", "ConnectionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_integration_webhook_receipts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "external_integration_connections",
                schema: "app");

            migrationBuilder.DropTable(
                name: "external_integration_secrets",
                schema: "app");
        }
    }
}
