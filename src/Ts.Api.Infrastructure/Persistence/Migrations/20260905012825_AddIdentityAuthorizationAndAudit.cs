using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAuthorizationAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "app",
                table: "users",
                columns: new[] { "Id", "ExternalSubject", "DisplayName", "IsActive" },
                values: new object[]
                {
                    new Guid("d483c64a-fb9b-4123-a182-9074d53c25df"),
                    "36f163b5-6e91-4e88-95c9-72830e445672",
                    "Administrador Sabor Santè",
                    true,
                });

            migrationBuilder.InsertData(
                schema: "app",
                table: "organization_memberships",
                columns: new[] { "OrganizationId", "UserId", "Role", "IsActive" },
                values: new object[]
                {
                    new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"),
                    new Guid("d483c64a-fb9b-4123-a182-9074d53c25df"),
                    1,
                    true,
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_events_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_events_users_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_ActorId",
                schema: "app",
                table: "audit_events",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_OrganizationId_CorrelationId",
                schema: "app",
                table: "audit_events",
                columns: new[] { "OrganizationId", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_OrganizationId_OccurredAt",
                schema: "app",
                table: "audit_events",
                columns: new[] { "OrganizationId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "app");

            migrationBuilder.DeleteData(
                schema: "app",
                table: "organization_memberships",
                keyColumns: new[] { "OrganizationId", "UserId" },
                keyValues: new object[]
                {
                    new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"),
                    new Guid("d483c64a-fb9b-4123-a182-9074d53c25df"),
                });

            migrationBuilder.DeleteData(
                schema: "app",
                table: "users",
                keyColumn: "Id",
                keyValue: new Guid("d483c64a-fb9b-4123-a182-9074d53c25df"));
        }
    }
}
