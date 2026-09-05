using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionPackingAndLabelPrints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerNameSnapshot",
                schema: "app",
                table: "orders",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE app.orders
                SET "CustomerNameSnapshot" = 'Cliente ' || upper(substring(replace("CustomerId"::text, '-', '') from 1 for 8))
                WHERE "CustomerNameSnapshot" = '';

                ALTER TABLE app.orders ALTER COLUMN "CustomerNameSnapshot" DROP DEFAULT;
                """);

            migrationBuilder.CreateTable(
                name: "packing_records",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PackedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_packing_records", x => x.Id);
                    table.UniqueConstraint("AK_packing_records_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_packing_records_orders_OrganizationId_OrderId",
                        columns: x => new { x.OrganizationId, x.OrderId },
                        principalSchema: "app",
                        principalTable: "orders",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_packing_records_users_PackedBy",
                        column: x => x.PackedBy,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "label_print_attempts",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackingRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SelectionJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_print_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_label_print_attempts_packing_records_OrganizationId_Packing~",
                        columns: x => new { x.OrganizationId, x.PackingRecordId },
                        principalSchema: "app",
                        principalTable: "packing_records",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_label_print_attempts_users_AttemptedBy",
                        column: x => x.AttemptedBy,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_label_print_attempts_AttemptedBy",
                schema: "app",
                table: "label_print_attempts",
                column: "AttemptedBy");

            migrationBuilder.CreateIndex(
                name: "IX_label_print_attempts_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "label_print_attempts",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_label_print_attempts_OrganizationId_PackingRecordId_Attempt~",
                schema: "app",
                table: "label_print_attempts",
                columns: new[] { "OrganizationId", "PackingRecordId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_packing_records_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "packing_records",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_packing_records_OrganizationId_OrderId",
                schema: "app",
                table: "packing_records",
                columns: new[] { "OrganizationId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_packing_records_PackedBy",
                schema: "app",
                table: "packing_records",
                column: "PackedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "label_print_attempts",
                schema: "app");

            migrationBuilder.DropTable(
                name: "packing_records",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "CustomerNameSnapshot",
                schema: "app",
                table: "orders");
        }
    }
}
