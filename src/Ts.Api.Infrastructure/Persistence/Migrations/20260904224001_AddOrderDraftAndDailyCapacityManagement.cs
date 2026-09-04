using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDraftAndDailyCapacityManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreationIdempotencyKey",
                schema: "app",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModificationIdempotencyKey",
                schema: "app",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrozenPresentation",
                schema: "app",
                table: "order_items",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfferName",
                schema: "app",
                table: "order_items",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProducibleItemName",
                schema: "app",
                table: "order_items",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastConfigurationIdempotencyKey",
                schema: "app",
                table: "daily_capacities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "app",
                table: "daily_capacities",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE app.orders
                SET "CreationIdempotencyKey" = 'legacy-' || "Id"::text;

                UPDATE app.order_items AS item
                SET "OfferName" = offer."Name"
                FROM app.catalog_offers AS offer
                WHERE offer."OrganizationId" = item."OrganizationId"
                  AND offer."Id" = item."OfferId";

                UPDATE app.order_items AS item
                SET "FrozenPresentation" = configuration."Presentation",
                    "ProducibleItemName" = producible."Name"
                FROM app.frozen_configurations AS configuration
                JOIN app.producible_items AS producible
                  ON producible."OrganizationId" = configuration."OrganizationId"
                 AND producible."Id" = configuration."ProducibleItemId"
                WHERE configuration."OrganizationId" = item."OrganizationId"
                  AND configuration."Id" = item."FrozenConfigurationId";

                UPDATE app.daily_capacities
                SET "LastConfigurationIdempotencyKey" = 'legacy-' || "Id"::text;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CreationIdempotencyKey",
                schema: "app",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OfferName",
                schema: "app",
                table: "order_items",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastConfigurationIdempotencyKey",
                schema: "app",
                table: "daily_capacities",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrganizationId_CreationIdempotencyKey",
                schema: "app",
                table: "orders",
                columns: new[] { "OrganizationId", "CreationIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_OrganizationId_LastModificationIdempotencyKey",
                schema: "app",
                table: "orders",
                columns: new[] { "OrganizationId", "LastModificationIdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_capacities_OrganizationId_LastConfigurationIdempotenc~",
                schema: "app",
                table: "daily_capacities",
                columns: new[] { "OrganizationId", "LastConfigurationIdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_OrganizationId_CreationIdempotencyKey",
                schema: "app",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_OrganizationId_LastModificationIdempotencyKey",
                schema: "app",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_daily_capacities_OrganizationId_LastConfigurationIdempotenc~",
                schema: "app",
                table: "daily_capacities");

            migrationBuilder.DropColumn(
                name: "CreationIdempotencyKey",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "LastModificationIdempotencyKey",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FrozenPresentation",
                schema: "app",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "OfferName",
                schema: "app",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "ProducibleItemName",
                schema: "app",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "LastConfigurationIdempotencyKey",
                schema: "app",
                table: "daily_capacities");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "app",
                table: "daily_capacities");
        }
    }
}
