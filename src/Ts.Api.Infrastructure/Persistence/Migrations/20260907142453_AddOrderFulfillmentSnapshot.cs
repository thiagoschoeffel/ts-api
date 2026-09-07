using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderFulfillmentSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryWindow",
                schema: "app",
                table: "orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentAddressLabel",
                schema: "app",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentCity",
                schema: "app",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentComplement",
                schema: "app",
                table: "orders",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentContactName",
                schema: "app",
                table: "orders",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FulfillmentFrozenAt",
                schema: "app",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentNeighborhood",
                schema: "app",
                table: "orders",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentNumber",
                schema: "app",
                table: "orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentPhone",
                schema: "app",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentPostalCode",
                schema: "app",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentReference",
                schema: "app",
                table: "orders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentState",
                schema: "app",
                table: "orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentStreet",
                schema: "app",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FulfillmentType",
                schema: "app",
                table: "orders",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryWindow",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentAddressLabel",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentCity",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentComplement",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentContactName",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentFrozenAt",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentNeighborhood",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentNumber",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentPhone",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentPostalCode",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentReference",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentState",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentStreet",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentType",
                schema: "app",
                table: "orders");
        }
    }
}
