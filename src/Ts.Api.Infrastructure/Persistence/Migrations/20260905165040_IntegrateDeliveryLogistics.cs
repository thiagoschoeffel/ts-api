using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateDeliveryLogistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_drivers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Identification = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_drivers", x => x.Id);
                    table.UniqueConstraint("AK_delivery_drivers_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "delivery_reschedules",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviousWindow = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NewDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NewWindow = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_reschedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_delivery_reschedules_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_routes",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DeliveryWindow = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverNameSnapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_routes", x => x.Id);
                    table.UniqueConstraint("AK_delivery_routes_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_delivery_routes_delivery_drivers_OrganizationId_DriverId",
                        columns: x => new { x.OrganizationId, x.DriverId },
                        principalSchema: "app",
                        principalTable: "delivery_drivers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_route_stops",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    CustomerNameSnapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CustomerPhoneSnapshot = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AddressSnapshot = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_route_stops", x => x.Id);
                    table.UniqueConstraint("AK_delivery_route_stops_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_delivery_route_stops_delivery_routes_OrganizationId_RouteId",
                        columns: x => new { x.OrganizationId, x.RouteId },
                        principalSchema: "app",
                        principalTable: "delivery_routes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_delivery_route_stops_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_attempts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteStopId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverNameSnapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReceivedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_delivery_attempts_delivery_route_stops_OrganizationId_Route~",
                        columns: x => new { x.OrganizationId, x.RouteStopId },
                        principalSchema: "app",
                        principalTable: "delivery_route_stops",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_attempts_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "delivery_attempts",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_attempts_OrganizationId_RouteStopId",
                schema: "app",
                table: "delivery_attempts",
                columns: new[] { "OrganizationId", "RouteStopId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_drivers_OrganizationId_Identification",
                schema: "app",
                table: "delivery_drivers",
                columns: new[] { "OrganizationId", "Identification" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_reschedules_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "delivery_reschedules",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_reschedules_OrganizationId_OrderId_OccurredAt",
                schema: "app",
                table: "delivery_reschedules",
                columns: new[] { "OrganizationId", "OrderId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_route_stops_OrganizationId_OrderId",
                schema: "app",
                table: "delivery_route_stops",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_route_stops_OrganizationId_RouteId_OrderId",
                schema: "app",
                table: "delivery_route_stops",
                columns: new[] { "OrganizationId", "RouteId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_route_stops_OrganizationId_RouteId_Position",
                schema: "app",
                table: "delivery_route_stops",
                columns: new[] { "OrganizationId", "RouteId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_delivery_routes_OrganizationId_Date_Status",
                schema: "app",
                table: "delivery_routes",
                columns: new[] { "OrganizationId", "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_routes_OrganizationId_DriverId",
                schema: "app",
                table: "delivery_routes",
                columns: new[] { "OrganizationId", "DriverId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_attempts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "delivery_reschedules",
                schema: "app");

            migrationBuilder.DropTable(
                name: "delivery_route_stops",
                schema: "app");

            migrationBuilder.DropTable(
                name: "delivery_routes",
                schema: "app");

            migrationBuilder.DropTable(
                name: "delivery_drivers",
                schema: "app");
        }
    }
}
