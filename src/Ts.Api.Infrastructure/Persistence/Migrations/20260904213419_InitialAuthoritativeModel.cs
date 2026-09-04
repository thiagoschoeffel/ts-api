using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuthoritativeModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "catalog_offers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    FulfillmentMode = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_offers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "producible_items",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_producible_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "frozen_configurations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProducibleItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Presentation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    QuantityPerUnit = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    MeasurementUnit = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_frozen_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_frozen_configurations_catalog_offers_OfferId",
                        column: x => x.OfferId,
                        principalSchema: "app",
                        principalTable: "catalog_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_frozen_configurations_producible_items_ProducibleItemId",
                        column: x => x.ProducibleItemId,
                        principalSchema: "app",
                        principalTable: "producible_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "frozen_lots",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FrozenConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManufacturedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ProducedQuantity = table.Column<int>(type: "integer", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_frozen_lots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_frozen_lots_frozen_configurations_FrozenConfigurationId",
                        column: x => x.FrozenConfigurationId,
                        principalSchema: "app",
                        principalTable: "frozen_configurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "frozen_stock_movements",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FrozenLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_frozen_stock_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_frozen_stock_movements_frozen_lots_FrozenLotId",
                        column: x => x.FrozenLotId,
                        principalSchema: "app",
                        principalTable: "frozen_lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_offers_NormalizedName",
                schema: "app",
                table: "catalog_offers",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_frozen_configurations_OfferId_ProducibleItemId_Presentation",
                schema: "app",
                table: "frozen_configurations",
                columns: new[] { "OfferId", "ProducibleItemId", "Presentation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_frozen_configurations_ProducibleItemId",
                schema: "app",
                table: "frozen_configurations",
                column: "ProducibleItemId");

            migrationBuilder.CreateIndex(
                name: "IX_frozen_lots_ExpiresOn_ManufacturedOn_Id",
                schema: "app",
                table: "frozen_lots",
                columns: new[] { "ExpiresOn", "ManufacturedOn", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_frozen_lots_FrozenConfigurationId",
                schema: "app",
                table: "frozen_lots",
                column: "FrozenConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_frozen_lots_IdempotencyKey",
                schema: "app",
                table: "frozen_lots",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_frozen_stock_movements_FrozenLotId_OccurredAt",
                schema: "app",
                table: "frozen_stock_movements",
                columns: new[] { "FrozenLotId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_producible_items_NormalizedName",
                schema: "app",
                table: "producible_items",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "frozen_stock_movements",
                schema: "app");

            migrationBuilder.DropTable(
                name: "frozen_lots",
                schema: "app");

            migrationBuilder.DropTable(
                name: "frozen_configurations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "catalog_offers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "producible_items",
                schema: "app");
        }
    }
}
