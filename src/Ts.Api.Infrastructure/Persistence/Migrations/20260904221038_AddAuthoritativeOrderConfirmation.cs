using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthoritativeOrderConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_capacities",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalUnits = table.Column<int>(type: "integer", nullable: false),
                    ReservedUnits = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_capacities", x => x.Id);
                    table.UniqueConstraint("AK_daily_capacities_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_daily_capacities_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConfirmedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmationIdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                    table.UniqueConstraint("AK_orders_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_orders_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_charges",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_charges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_charges_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    FulfillmentMode = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    FrozenConfigurationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                    table.UniqueConstraint("AK_order_items_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_order_items_catalog_offers_OrganizationId_OfferId",
                        columns: x => new { x.OrganizationId, x.OfferId },
                        principalSchema: "app",
                        principalTable: "catalog_offers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_frozen_configurations_OrganizationId_FrozenConf~",
                        columns: x => new { x.OrganizationId, x.FrozenConfigurationId },
                        principalSchema: "app",
                        principalTable: "frozen_configurations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "frozen_stock_allocations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrozenConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrozenLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_frozen_stock_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_frozen_stock_allocations_frozen_configurations_Organization~",
                        columns: x => new { x.OrganizationId, x.FrozenConfigurationId },
                        principalSchema: "app",
                        principalTable: "frozen_configurations",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_frozen_stock_allocations_frozen_lots_OrganizationId_FrozenL~",
                        columns: x => new { x.OrganizationId, x.FrozenLotId },
                        principalSchema: "app",
                        principalTable: "frozen_lots",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_frozen_stock_allocations_order_items_OrganizationId_OrderIt~",
                        columns: x => new { x.OrganizationId, x.OrderItemId },
                        principalSchema: "app",
                        principalTable: "order_items",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_frozen_stock_allocations_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_capacities_OrganizationId_OperationalDate",
                schema: "app",
                table: "daily_capacities",
                columns: new[] { "OrganizationId", "OperationalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_frozen_stock_allocations_OrganizationId_FrozenConfiguration~",
                schema: "app",
                table: "frozen_stock_allocations",
                columns: new[] { "OrganizationId", "FrozenConfigurationId" });

            migrationBuilder.CreateIndex(
                name: "IX_frozen_stock_allocations_OrganizationId_FrozenLotId",
                schema: "app",
                table: "frozen_stock_allocations",
                columns: new[] { "OrganizationId", "FrozenLotId" });

            migrationBuilder.CreateIndex(
                name: "IX_frozen_stock_allocations_OrganizationId_OrderId",
                schema: "app",
                table: "frozen_stock_allocations",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_frozen_stock_allocations_OrganizationId_OrderItemId_FrozenL~",
                schema: "app",
                table: "frozen_stock_allocations",
                columns: new[] { "OrganizationId", "OrderItemId", "FrozenLotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_charges_OrganizationId_OrderId",
                schema: "app",
                table: "order_charges",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrganizationId_FrozenConfigurationId",
                schema: "app",
                table: "order_items",
                columns: new[] { "OrganizationId", "FrozenConfigurationId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrganizationId_OfferId",
                schema: "app",
                table: "order_items",
                columns: new[] { "OrganizationId", "OfferId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrganizationId_OrderId",
                schema: "app",
                table: "order_items",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrganizationId_ConfirmationIdempotencyKey",
                schema: "app",
                table: "orders",
                columns: new[] { "OrganizationId", "ConfirmationIdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_capacities",
                schema: "app");

            migrationBuilder.DropTable(
                name: "frozen_stock_allocations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "order_charges",
                schema: "app");

            migrationBuilder.DropTable(
                name: "order_items",
                schema: "app");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "app");
        }
    }
}
