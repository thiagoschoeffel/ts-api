using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDraftOrderItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_items_orders_OrganizationId_OrderId",
                schema: "app",
                table: "order_items");

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_orders_OrganizationId_OrderId",
                schema: "app",
                table: "order_items",
                columns: new[] { "OrganizationId", "OrderId" },
                principalSchema: "app",
                principalTable: "orders",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_items_orders_OrganizationId_OrderId",
                schema: "app",
                table: "order_items");

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_orders_OrganizationId_OrderId",
                schema: "app",
                table: "order_items",
                columns: new[] { "OrganizationId", "OrderId" },
                principalSchema: "app",
                principalTable: "orders",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
