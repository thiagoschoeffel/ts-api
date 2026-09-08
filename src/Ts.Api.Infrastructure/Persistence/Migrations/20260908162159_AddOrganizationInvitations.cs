using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "app",
                table: "users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "app",
                table: "organization_memberships",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "organization_invitations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmailSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_invitations_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_invitations_users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_invitations_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                schema: "app",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_AcceptedByUserId",
                schema: "app",
                table: "organization_invitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_CreatedBy",
                schema: "app",
                table: "organization_invitations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_OrganizationId_NormalizedEmail",
                schema: "app",
                table: "organization_invitations",
                columns: new[] { "OrganizationId", "NormalizedEmail" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL AND \"AcceptedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_TokenHash",
                schema: "app",
                table: "organization_invitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_invitations",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_users_Email",
                schema: "app",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "app",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "app",
                table: "organization_memberships");
        }
    }
}
