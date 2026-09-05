using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateCustomersPlansAndFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BenefitDescriptionSnapshot",
                schema: "app",
                table: "plan_acquisitions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompatibleOfferIds",
                schema: "app",
                table: "plan_acquisitions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "app",
                table: "plan_acquisitions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CustomerNameSnapshot",
                schema: "app",
                table: "plan_acquisitions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiresOn",
                schema: "app",
                table: "plan_acquisitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                schema: "app",
                table: "plan_acquisitions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                schema: "app",
                table: "plan_acquisitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                schema: "app",
                table: "financial_credit_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_order_charges_OrganizationId_Id",
                schema: "app",
                table: "order_charges",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "commercial_plans",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    BenefitDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DefaultCredits = table.Column<int>(type: "integer", nullable: false),
                    DefaultPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ValidityDays = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_plans", x => x.Id);
                    table.UniqueConstraint("AK_commercial_plans_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_commercial_plans_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PreferredDeliveryDriverId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreferredPaymentCondition = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PreferredPaymentMethod = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.UniqueConstraint("AK_customers_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_customers_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commercial_plan_offers",
                schema: "app",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commercial_plan_offers", x => new { x.OrganizationId, x.PlanId, x.OfferId });
                    table.ForeignKey(
                        name: "FK_commercial_plan_offers_catalog_offers_OrganizationId_OfferId",
                        columns: x => new { x.OrganizationId, x.OfferId },
                        principalSchema: "app",
                        principalTable: "catalog_offers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_commercial_plan_offers_commercial_plans_OrganizationId_Plan~",
                        columns: x => new { x.OrganizationId, x.PlanId },
                        principalSchema: "app",
                        principalTable: "commercial_plans",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_addresses",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Complement = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Neighborhood = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReferencePoint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_addresses_customers_OrganizationId_CustomerId",
                        columns: x => new { x.OrganizationId, x.CustomerId },
                        principalSchema: "app",
                        principalTable: "customers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_preferences",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_preferences_customers_OrganizationId_CustomerId",
                        columns: x => new { x.OrganizationId, x.CustomerId },
                        principalSchema: "app",
                        principalTable: "customers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerNameSnapshot = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.UniqueConstraint("AK_payments_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_payments_customers_OrganizationId_CustomerId",
                        columns: x => new { x.OrganizationId, x.CustomerId },
                        principalSchema: "app",
                        principalTable: "customers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_users_RecordedBy",
                        column: x => x.RecordedBy,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_allocations_order_charges_OrganizationId_ChargeId",
                        columns: x => new { x.OrganizationId, x.ChargeId },
                        principalSchema: "app",
                        principalTable: "order_charges",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_allocations_payments_OrganizationId_PaymentId",
                        columns: x => new { x.OrganizationId, x.PaymentId },
                        principalSchema: "app",
                        principalTable: "payments",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plan_acquisitions_OrganizationId_CustomerId",
                schema: "app",
                table: "plan_acquisitions",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_plan_acquisitions_OrganizationId_PlanId",
                schema: "app",
                table: "plan_acquisitions",
                columns: new[] { "OrganizationId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrganizationId_CustomerId",
                schema: "app",
                table: "orders",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_credit_movements_OrganizationId_PaymentId",
                schema: "app",
                table: "financial_credit_movements",
                columns: new[] { "OrganizationId", "PaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_plan_offers_OrganizationId_OfferId",
                schema: "app",
                table: "commercial_plan_offers",
                columns: new[] { "OrganizationId", "OfferId" });

            migrationBuilder.CreateIndex(
                name: "IX_commercial_plans_OrganizationId_Name",
                schema: "app",
                table: "commercial_plans",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_addresses_OrganizationId_CustomerId",
                schema: "app",
                table: "customer_addresses",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customer_preferences_OrganizationId_CustomerId",
                schema: "app",
                table: "customer_preferences",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_OrganizationId_Phone",
                schema: "app",
                table: "customers",
                columns: new[] { "OrganizationId", "Phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_OrganizationId_ChargeId",
                schema: "app",
                table: "payment_allocations",
                columns: new[] { "OrganizationId", "ChargeId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_OrganizationId_PaymentId_ChargeId",
                schema: "app",
                table: "payment_allocations",
                columns: new[] { "OrganizationId", "PaymentId", "ChargeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrganizationId_CustomerId",
                schema: "app",
                table: "payments",
                columns: new[] { "OrganizationId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "payments",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_RecordedBy",
                schema: "app",
                table: "payments",
                column: "RecordedBy");

            migrationBuilder.Sql("""
                WITH refs AS (
                    SELECT "OrganizationId", "CustomerId", "CustomerNameSnapshot" AS name FROM app.orders
                    UNION ALL SELECT "OrganizationId", "CustomerId", NULL FROM app.customer_dietary_restrictions
                    UNION ALL SELECT "OrganizationId", "CustomerId", NULL FROM app.plan_acquisitions
                    UNION ALL SELECT "OrganizationId", "CustomerId", NULL FROM app.financial_credit_movements
                ), grouped AS (
                    SELECT "OrganizationId", "CustomerId", max(name) AS name
                    FROM refs GROUP BY "OrganizationId", "CustomerId"
                ), ranked AS (
                    SELECT *, row_number() OVER (PARTITION BY "OrganizationId" ORDER BY "CustomerId") AS n
                    FROM grouped
                )
                INSERT INTO app.customers ("Id", "OrganizationId", "Name", "Phone", "IsActive", "Version")
                SELECT "CustomerId", "OrganizationId",
                    coalesce(nullif(name, ''), 'Cliente ' || upper(left(replace("CustomerId"::text, '-', ''), 8))),
                    '119' || lpad(n::text, 8, '0'), true, 1
                FROM ranked;

                UPDATE app.plan_acquisitions a
                SET "CustomerNameSnapshot" = c."Name",
                    "BenefitDescriptionSnapshot" = a."PlanName",
                    "CompatibleOfferIds" = a."EligibleOfferId"::text,
                    "CreatedAt" = (a."AcquiredOn"::timestamp AT TIME ZONE 'UTC'),
                    "PaidAmount" = a."BenefitAmountPerCredit" * (
                        SELECT coalesce(sum(m."Quantity"), 0)
                        FROM app.plan_credit_movements m
                        WHERE m."OrganizationId" = a."OrganizationId" AND m."AcquisitionId" = a."Id" AND m."Type" = 0)
                FROM app.customers c
                WHERE c."OrganizationId" = a."OrganizationId" AND c."Id" = a."CustomerId";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_customer_dietary_restrictions_customers_OrganizationId_Cust~",
                schema: "app",
                table: "customer_dietary_restrictions",
                columns: new[] { "OrganizationId", "CustomerId" },
                principalSchema: "app",
                principalTable: "customers",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_financial_credit_movements_customers_OrganizationId_Custome~",
                schema: "app",
                table: "financial_credit_movements",
                columns: new[] { "OrganizationId", "CustomerId" },
                principalSchema: "app",
                principalTable: "customers",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_financial_credit_movements_payments_OrganizationId_PaymentId",
                schema: "app",
                table: "financial_credit_movements",
                columns: new[] { "OrganizationId", "PaymentId" },
                principalSchema: "app",
                principalTable: "payments",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_customers_OrganizationId_CustomerId",
                schema: "app",
                table: "orders",
                columns: new[] { "OrganizationId", "CustomerId" },
                principalSchema: "app",
                principalTable: "customers",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_plan_acquisitions_commercial_plans_OrganizationId_PlanId",
                schema: "app",
                table: "plan_acquisitions",
                columns: new[] { "OrganizationId", "PlanId" },
                principalSchema: "app",
                principalTable: "commercial_plans",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_plan_acquisitions_customers_OrganizationId_CustomerId",
                schema: "app",
                table: "plan_acquisitions",
                columns: new[] { "OrganizationId", "CustomerId" },
                principalSchema: "app",
                principalTable: "customers",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customer_dietary_restrictions_customers_OrganizationId_Cust~",
                schema: "app",
                table: "customer_dietary_restrictions");

            migrationBuilder.DropForeignKey(
                name: "FK_financial_credit_movements_customers_OrganizationId_Custome~",
                schema: "app",
                table: "financial_credit_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_financial_credit_movements_payments_OrganizationId_PaymentId",
                schema: "app",
                table: "financial_credit_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_customers_OrganizationId_CustomerId",
                schema: "app",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_plan_acquisitions_commercial_plans_OrganizationId_PlanId",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_plan_acquisitions_customers_OrganizationId_CustomerId",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropTable(
                name: "commercial_plan_offers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "customer_addresses",
                schema: "app");

            migrationBuilder.DropTable(
                name: "customer_preferences",
                schema: "app");

            migrationBuilder.DropTable(
                name: "payment_allocations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "commercial_plans",
                schema: "app");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_plan_acquisitions_OrganizationId_CustomerId",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropIndex(
                name: "IX_plan_acquisitions_OrganizationId_PlanId",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropIndex(
                name: "IX_orders_OrganizationId_CustomerId",
                schema: "app",
                table: "orders");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_order_charges_OrganizationId_Id",
                schema: "app",
                table: "order_charges");

            migrationBuilder.DropIndex(
                name: "IX_financial_credit_movements_OrganizationId_PaymentId",
                schema: "app",
                table: "financial_credit_movements");

            migrationBuilder.DropColumn(
                name: "BenefitDescriptionSnapshot",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropColumn(
                name: "CompatibleOfferIds",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropColumn(
                name: "CustomerNameSnapshot",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropColumn(
                name: "ExpiresOn",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropColumn(
                name: "PlanId",
                schema: "app",
                table: "plan_acquisitions");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                schema: "app",
                table: "financial_credit_movements");
        }
    }
}
