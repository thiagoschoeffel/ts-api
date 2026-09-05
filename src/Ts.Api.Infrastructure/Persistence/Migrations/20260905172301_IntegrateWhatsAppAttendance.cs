using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateWhatsAppAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "whatsapp_conversations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPhoneNumberId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BusinessPhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    AssignedTo = table.Column<Guid>(type: "uuid", nullable: true),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_conversations", x => x.Id);
                    table.UniqueConstraint("AK_whatsapp_conversations_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_whatsapp_conversations_customers_OrganizationId_CustomerId",
                        columns: x => new { x.OrganizationId, x.CustomerId },
                        principalSchema: "app",
                        principalTable: "customers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_conversations_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_quota_periods",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessPhoneNumberId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BusinessPhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    FreeLimit = table.Column<int>(type: "integer", nullable: false),
                    AutomationPauseAt = table.Column<int>(type: "integer", nullable: false),
                    Delivered = table.Column<int>(type: "integer", nullable: false),
                    Reserved = table.Column<int>(type: "integer", nullable: false),
                    PaidMessagesEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_quota_periods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_messages",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    PlatformTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessingStatus = table.Column<int>(type: "integer", nullable: false),
                    DeliveryStatus = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_whatsapp_messages_whatsapp_conversations_OrganizationId_Con~",
                        columns: x => new { x.OrganizationId, x.ConversationId },
                        principalSchema: "app",
                        principalTable: "whatsapp_conversations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_conversations_OrganizationId_BusinessPhoneNumberId~",
                schema: "app",
                table: "whatsapp_conversations",
                columns: new[] { "OrganizationId", "BusinessPhoneNumberId", "CustomerPhone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_conversations_OrganizationId_CustomerId",
                schema: "app",
                table: "whatsapp_conversations",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_conversations_OrganizationId_OrderId",
                schema: "app",
                table: "whatsapp_conversations",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_messages_OrganizationId_ConversationId_Sequence",
                schema: "app",
                table: "whatsapp_messages",
                columns: new[] { "OrganizationId", "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_messages_OrganizationId_ExternalId",
                schema: "app",
                table: "whatsapp_messages",
                columns: new[] { "OrganizationId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_messages_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "whatsapp_messages",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_quota_periods_OrganizationId_BusinessPhoneNumberId~",
                schema: "app",
                table: "whatsapp_quota_periods",
                columns: new[] { "OrganizationId", "BusinessPhoneNumberId", "PeriodStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "whatsapp_messages",
                schema: "app");

            migrationBuilder.DropTable(
                name: "whatsapp_quota_periods",
                schema: "app");

            migrationBuilder.DropTable(
                name: "whatsapp_conversations",
                schema: "app");
        }
    }
}
