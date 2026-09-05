using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteOrderConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProducibleItemId",
                schema: "app",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_dietary_restrictions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Marker = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_dietary_restrictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_dietary_restrictions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_credit_movements",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_credit_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_credit_movements_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_credit_movements_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_confirmation_audits",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PlanCreditCoveredAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DiscountReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeliveryFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    FinancialCreditApplied = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_confirmation_audits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_confirmation_audits_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_item_components",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompositionVersion = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    QuantityPerUnit = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    TotalQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    MeasurementUnit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DietaryMarkers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_item_components", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_item_components_order_items_OrganizationId_OrderItemId",
                        columns: x => new { x.OrganizationId, x.OrderItemId },
                        principalSchema: "app",
                        principalTable: "order_items",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_item_components_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_acquisitions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EligibleOfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BenefitAmountPerCredit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    AcquiredOn = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_acquisitions", x => x.Id);
                    table.UniqueConstraint("AK_plan_acquisitions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_plan_acquisitions_catalog_offers_OrganizationId_EligibleOff~",
                        columns: x => new { x.OrganizationId, x.EligibleOfferId },
                        principalSchema: "app",
                        principalTable: "catalog_offers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plan_acquisitions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "producible_compositions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducibleItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_producible_compositions", x => x.Id);
                    table.UniqueConstraint("AK_producible_compositions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_producible_compositions_producible_items_OrganizationId_Pro~",
                        columns: x => new { x.OrganizationId, x.ProducibleItemId },
                        principalSchema: "app",
                        principalTable: "producible_items",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_plan_credit_allocations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcquisitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CoveredAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_plan_credit_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_plan_credit_allocations_order_items_OrganizationId_Or~",
                        columns: x => new { x.OrganizationId, x.OrderItemId },
                        principalSchema: "app",
                        principalTable: "order_items",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_plan_credit_allocations_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_plan_credit_allocations_plan_acquisitions_Organizatio~",
                        columns: x => new { x.OrganizationId, x.AcquisitionId },
                        principalSchema: "app",
                        principalTable: "plan_acquisitions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plan_credit_movements",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcquisitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_credit_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plan_credit_movements_plan_acquisitions_OrganizationId_Acqu~",
                        columns: x => new { x.OrganizationId, x.AcquisitionId },
                        principalSchema: "app",
                        principalTable: "plan_acquisitions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "producible_components",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    MeasurementUnit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DietaryMarkers = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_producible_components", x => x.Id);
                    table.ForeignKey(
                        name: "FK_producible_components_producible_compositions_OrganizationI~",
                        columns: x => new { x.OrganizationId, x.CompositionId },
                        principalSchema: "app",
                        principalTable: "producible_compositions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_OrganizationId_ProducibleItemId",
                schema: "app",
                table: "order_items",
                columns: new[] { "OrganizationId", "ProducibleItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_dietary_restrictions_OrganizationId_CustomerId_Mar~",
                schema: "app",
                table: "customer_dietary_restrictions",
                columns: new[] { "OrganizationId", "CustomerId", "Marker" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_credit_movements_OrganizationId_CustomerId_Occurr~",
                schema: "app",
                table: "financial_credit_movements",
                columns: new[] { "OrganizationId", "CustomerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_credit_movements_OrganizationId_OrderId",
                schema: "app",
                table: "financial_credit_movements",
                columns: new[] { "OrganizationId", "OrderId" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL AND \"Type\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_order_confirmation_audits_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "order_confirmation_audits",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_confirmation_audits_OrganizationId_OrderId",
                schema: "app",
                table: "order_confirmation_audits",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_item_components_OrganizationId_OrderId",
                schema: "app",
                table: "order_item_components",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_item_components_OrganizationId_OrderItemId",
                schema: "app",
                table: "order_item_components",
                columns: new[] { "OrganizationId", "OrderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_plan_credit_allocations_OrganizationId_AcquisitionId",
                schema: "app",
                table: "order_plan_credit_allocations",
                columns: new[] { "OrganizationId", "AcquisitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_plan_credit_allocations_OrganizationId_OrderId",
                schema: "app",
                table: "order_plan_credit_allocations",
                columns: new[] { "OrganizationId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_order_plan_credit_allocations_OrganizationId_OrderItemId",
                schema: "app",
                table: "order_plan_credit_allocations",
                columns: new[] { "OrganizationId", "OrderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_acquisitions_OrganizationId_EligibleOfferId",
                schema: "app",
                table: "plan_acquisitions",
                columns: new[] { "OrganizationId", "EligibleOfferId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_credit_movements_OrganizationId_AcquisitionId",
                schema: "app",
                table: "plan_credit_movements",
                columns: new[] { "OrganizationId", "AcquisitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_credit_movements_OrganizationId_OrderId_OrderItemId",
                schema: "app",
                table: "plan_credit_movements",
                columns: new[] { "OrganizationId", "OrderId", "OrderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_producible_components_OrganizationId_CompositionId",
                schema: "app",
                table: "producible_components",
                columns: new[] { "OrganizationId", "CompositionId" });

            migrationBuilder.CreateIndex(
                name: "IX_producible_compositions_OrganizationId_ProducibleItemId_Ver~",
                schema: "app",
                table: "producible_compositions",
                columns: new[] { "OrganizationId", "ProducibleItemId", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_producible_items_OrganizationId_ProducibleItemId",
                schema: "app",
                table: "order_items",
                columns: new[] { "OrganizationId", "ProducibleItemId" },
                principalSchema: "app",
                principalTable: "producible_items",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_items_producible_items_OrganizationId_ProducibleItemId",
                schema: "app",
                table: "order_items");

            migrationBuilder.DropTable(
                name: "customer_dietary_restrictions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "financial_credit_movements",
                schema: "app");

            migrationBuilder.DropTable(
                name: "order_confirmation_audits",
                schema: "app");

            migrationBuilder.DropTable(
                name: "order_item_components",
                schema: "app");

            migrationBuilder.DropTable(
                name: "order_plan_credit_allocations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "plan_credit_movements",
                schema: "app");

            migrationBuilder.DropTable(
                name: "producible_components",
                schema: "app");

            migrationBuilder.DropTable(
                name: "plan_acquisitions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "producible_compositions",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_order_items_OrganizationId_ProducibleItemId",
                schema: "app",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "ProducibleItemId",
                schema: "app",
                table: "order_items");
        }
    }
}
