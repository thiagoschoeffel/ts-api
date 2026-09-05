using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFrozenStockManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "app",
                table: "frozen_stock_movements",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_frozen_stock_movements_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "frozen_stock_movements",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_frozen_stock_movements_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "frozen_stock_movements");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "app",
                table: "frozen_stock_movements");
        }
    }
}
