using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLifecycleAndReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                schema: "app",
                table: "order_charges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledBy",
                schema: "app",
                table: "order_charges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "app",
                table: "order_charges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "order_lifecycle_events",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: false),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    PreviousOperationalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NewOperationalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviousVersion = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FrozenDisposition = table.Column<int>(type: "integer", nullable: false),
                    CommercialDisposition = table.Column<int>(type: "integer", nullable: false),
                    HumanInspectionPerformed = table.Column<bool>(type: "boolean", nullable: false),
                    PackagingIntact = table.Column<bool>(type: "boolean", nullable: false),
                    TemperatureControlled = table.Column<bool>(type: "boolean", nullable: false),
                    TraceabilityIntact = table.Column<bool>(type: "boolean", nullable: false),
                    CapacityUnitsReleased = table.Column<int>(type: "integer", nullable: false),
                    PlanCreditsReversed = table.Column<int>(type: "integer", nullable: false),
                    FinancialCreditReversed = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ChargesCancelled = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_lifecycle_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_lifecycle_events_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_lifecycle_events_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "order_lifecycle_events",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_lifecycle_events_OrganizationId_OrderId_OccurredAt",
                schema: "app",
                table: "order_lifecycle_events",
                columns: new[] { "OrganizationId", "OrderId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_lifecycle_events",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "app",
                table: "order_charges");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                schema: "app",
                table: "order_charges");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "app",
                table: "order_charges");
        }
    }
}
