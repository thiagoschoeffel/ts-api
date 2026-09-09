using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDraftFinancialTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DraftDeliveryFee",
                schema: "app",
                table: "orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DraftDiscountAmount",
                schema: "app",
                table: "orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DraftDiscountReason",
                schema: "app",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCondition",
                schema: "app",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "cash");

            migrationBuilder.AddColumn<DateOnly>(
                name: "PaymentDueDate",
                schema: "app",
                table: "orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                schema: "app",
                table: "orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "pix");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DraftDeliveryFee",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DraftDiscountAmount",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DraftDiscountReason",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaymentCondition",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaymentDueDate",
                schema: "app",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                schema: "app",
                table: "orders");
        }
    }
}
