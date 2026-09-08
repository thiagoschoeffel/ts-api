using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurablePlatformOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_onboardings",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerInvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_onboardings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_onboardings_organization_invitations_OwnerInvitati~",
                        column: x => x.OwnerInvitationId,
                        principalSchema: "app",
                        principalTable: "organization_invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_onboardings_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_provisioning_operations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OnboardingId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentStep = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_provisioning_operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_provisioning_operations_platform_onboardings_Onboa~",
                        column: x => x.OnboardingId,
                        principalSchema: "app",
                        principalTable: "platform_onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_outbox_messages",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_outbox_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_outbox_messages_organization_invitations_Invitatio~",
                        column: x => x.InvitationId,
                        principalSchema: "app",
                        principalTable: "organization_invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_platform_outbox_messages_platform_provisioning_operations_O~",
                        column: x => x.OperationId,
                        principalSchema: "app",
                        principalTable: "platform_provisioning_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_onboardings_OrganizationId",
                schema: "app",
                table: "platform_onboardings",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_onboardings_OwnerInvitationId",
                schema: "app",
                table: "platform_onboardings",
                column: "OwnerInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_platform_onboardings_UpdatedAt_Id",
                schema: "app",
                table: "platform_onboardings",
                columns: new[] { "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_outbox_messages_InvitationId",
                schema: "app",
                table: "platform_outbox_messages",
                column: "InvitationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_outbox_messages_OperationId",
                schema: "app",
                table: "platform_outbox_messages",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_outbox_messages_Status_CreatedAt",
                schema: "app",
                table: "platform_outbox_messages",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_provisioning_operations_IdempotencyKey",
                schema: "app",
                table: "platform_provisioning_operations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_provisioning_operations_OnboardingId",
                schema: "app",
                table: "platform_provisioning_operations",
                column: "OnboardingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_provisioning_operations_Status_NextAttemptAt_Lease~",
                schema: "app",
                table: "platform_provisioning_operations",
                columns: new[] { "Status", "NextAttemptAt", "LeaseUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_outbox_messages",
                schema: "app");

            migrationBuilder.DropTable(
                name: "platform_provisioning_operations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "platform_onboardings",
                schema: "app");
        }
    }
}
