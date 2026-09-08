using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSaasPlansAndEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saas_plan_versions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_plan_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_saas_subscriptions",
                schema: "app",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_saas_subscriptions", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_organization_saas_subscriptions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_saas_subscriptions_saas_plan_versions_PlanVers~",
                        column: x => x.PlanVersionId,
                        principalSchema: "app",
                        principalTable: "saas_plan_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_saas_subscriptions_users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "saas_plan_entitlements",
                schema: "app",
                columns: table => new
                {
                    PlanVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_plan_entitlements", x => new { x.PlanVersionId, x.Code });
                    table.ForeignKey(
                        name: "FK_saas_plan_entitlements_saas_plan_versions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalSchema: "app",
                        principalTable: "saas_plan_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_saas_subscriptions_AssignedBy",
                schema: "app",
                table: "organization_saas_subscriptions",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_organization_saas_subscriptions_PlanVersionId",
                schema: "app",
                table: "organization_saas_subscriptions",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_saas_plan_versions_Code_Version",
                schema: "app",
                table: "saas_plan_versions",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO app.saas_plan_versions ("Id", "Code", "Name", "Version", "IsAvailable", "CreatedAt")
                VALUES ('e1600000-0000-0000-0000-000000000001', 'complete', 'Completo', 1, TRUE, TIMESTAMPTZ '2026-09-08 00:00:00+00');

                INSERT INTO app.saas_plan_entitlements ("PlanVersionId", "Code") VALUES
                  ('e1600000-0000-0000-0000-000000000001', 'business.access'),
                  ('e1600000-0000-0000-0000-000000000001', 'attendance'),
                  ('e1600000-0000-0000-0000-000000000001', 'catalog'),
                  ('e1600000-0000-0000-0000-000000000001', 'commerce'),
                  ('e1600000-0000-0000-0000-000000000001', 'logistics'),
                  ('e1600000-0000-0000-0000-000000000001', 'operations');

                INSERT INTO app.organization_saas_subscriptions
                  ("OrganizationId", "PlanVersionId", "AssignedAt", "AssignedBy", "Version")
                SELECT organization."Id", 'e1600000-0000-0000-0000-000000000001',
                       TIMESTAMPTZ '2026-09-08 00:00:00+00', actor."UserId", 1
                FROM app.organizations organization
                CROSS JOIN LATERAL (
                  SELECT membership."UserId"
                  FROM app.organization_memberships membership
                  WHERE membership."OrganizationId" = organization."Id" AND membership."IsActive" = TRUE
                  ORDER BY CASE WHEN membership."Role" = 1 THEN 0 ELSE 1 END, membership."UserId"
                  LIMIT 1
                ) actor
                WHERE organization."LifecycleStatus" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_saas_subscriptions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "saas_plan_entitlements",
                schema: "app");

            migrationBuilder.DropTable(
                name: "saas_plan_versions",
                schema: "app");
        }
    }
}
