using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthoritativeCatalogAndMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "app",
                table: "producible_items",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Preparação");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "app",
                table: "producible_items",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeasurementUnit",
                schema: "app",
                table: "producible_items",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "un");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "app",
                table: "producible_components",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Ingredient");

            migrationBuilder.AddColumn<Guid>(
                name: "ReferencedProducibleItemId",
                schema: "app",
                table: "producible_components",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                schema: "app",
                table: "catalog_offers",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "app",
                table: "catalog_offers",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresMenuChoice",
                schema: "app",
                table: "catalog_offers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "catalog_addons",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ProducibleItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationalQuantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    MeasurementUnit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_addons", x => x.Id);
                    table.UniqueConstraint("AK_catalog_addons_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_catalog_addons_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_catalog_addons_producible_items_OrganizationId_ProducibleIt~",
                        columns: x => new { x.OrganizationId, x.ProducibleItemId },
                        principalSchema: "app",
                        principalTable: "producible_items",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "catalog_offer_versions",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_offer_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_offer_versions_catalog_offers_OrganizationId_OfferId",
                        columns: x => new { x.OrganizationId, x.OfferId },
                        principalSchema: "app",
                        principalTable: "catalog_offers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "component_types",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_component_types", x => x.Id);
                    table.UniqueConstraint("AK_component_types_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_component_types_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "daily_menus",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_menus", x => x.Id);
                    table.UniqueConstraint("AK_daily_menus_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_daily_menus_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "weekly_menu_plans",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    DaysJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekly_menu_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_weekly_menu_plans_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "daily_menu_offers",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyMenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectivePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Availability = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_menu_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_daily_menu_offers_catalog_offers_OrganizationId_OfferId",
                        columns: x => new { x.OrganizationId, x.OfferId },
                        principalSchema: "app",
                        principalTable: "catalog_offers",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_daily_menu_offers_daily_menus_OrganizationId_DailyMenuId",
                        columns: x => new { x.OrganizationId, x.DailyMenuId },
                        principalSchema: "app",
                        principalTable: "daily_menus",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_menu_options",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyMenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProducibleItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Availability = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_menu_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_daily_menu_options_daily_menus_OrganizationId_DailyMenuId",
                        columns: x => new { x.OrganizationId, x.DailyMenuId },
                        principalSchema: "app",
                        principalTable: "daily_menus",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_daily_menu_options_producible_items_OrganizationId_Producib~",
                        columns: x => new { x.OrganizationId, x.ProducibleItemId },
                        principalSchema: "app",
                        principalTable: "producible_items",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_producible_components_OrganizationId_ReferencedProducibleIt~",
                schema: "app",
                table: "producible_components",
                columns: new[] { "OrganizationId", "ReferencedProducibleItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_addons_OrganizationId_NormalizedName",
                schema: "app",
                table: "catalog_addons",
                columns: new[] { "OrganizationId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_addons_OrganizationId_ProducibleItemId",
                schema: "app",
                table: "catalog_addons",
                columns: new[] { "OrganizationId", "ProducibleItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_offer_versions_OrganizationId_OfferId_Version",
                schema: "app",
                table: "catalog_offer_versions",
                columns: new[] { "OrganizationId", "OfferId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_component_types_OrganizationId_NormalizedName",
                schema: "app",
                table: "component_types",
                columns: new[] { "OrganizationId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_menu_offers_OrganizationId_DailyMenuId_DisplayOrder",
                schema: "app",
                table: "daily_menu_offers",
                columns: new[] { "OrganizationId", "DailyMenuId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_menu_offers_OrganizationId_DailyMenuId_OfferId",
                schema: "app",
                table: "daily_menu_offers",
                columns: new[] { "OrganizationId", "DailyMenuId", "OfferId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_menu_offers_OrganizationId_OfferId",
                schema: "app",
                table: "daily_menu_offers",
                columns: new[] { "OrganizationId", "OfferId" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_menu_options_OrganizationId_DailyMenuId_Category",
                schema: "app",
                table: "daily_menu_options",
                columns: new[] { "OrganizationId", "DailyMenuId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_menu_options_OrganizationId_ProducibleItemId",
                schema: "app",
                table: "daily_menu_options",
                columns: new[] { "OrganizationId", "ProducibleItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_menus_OrganizationId_Date",
                schema: "app",
                table: "daily_menus",
                columns: new[] { "OrganizationId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_weekly_menu_plans_OrganizationId_WeekStart",
                schema: "app",
                table: "weekly_menu_plans",
                columns: new[] { "OrganizationId", "WeekStart" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_producible_components_producible_items_OrganizationId_Refer~",
                schema: "app",
                table: "producible_components",
                columns: new[] { "OrganizationId", "ReferencedProducibleItemId" },
                principalSchema: "app",
                principalTable: "producible_items",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_producible_components_producible_items_OrganizationId_Refer~",
                schema: "app",
                table: "producible_components");

            migrationBuilder.DropTable(
                name: "catalog_addons",
                schema: "app");

            migrationBuilder.DropTable(
                name: "catalog_offer_versions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "component_types",
                schema: "app");

            migrationBuilder.DropTable(
                name: "daily_menu_offers",
                schema: "app");

            migrationBuilder.DropTable(
                name: "daily_menu_options",
                schema: "app");

            migrationBuilder.DropTable(
                name: "weekly_menu_plans",
                schema: "app");

            migrationBuilder.DropTable(
                name: "daily_menus",
                schema: "app");

            migrationBuilder.DropIndex(
                name: "IX_producible_components_OrganizationId_ReferencedProducibleIt~",
                schema: "app",
                table: "producible_components");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "app",
                table: "producible_items");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "app",
                table: "producible_items");

            migrationBuilder.DropColumn(
                name: "MeasurementUnit",
                schema: "app",
                table: "producible_items");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "app",
                table: "producible_components");

            migrationBuilder.DropColumn(
                name: "ReferencedProducibleItemId",
                schema: "app",
                table: "producible_components");

            migrationBuilder.DropColumn(
                name: "BasePrice",
                schema: "app",
                table: "catalog_offers");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "app",
                table: "catalog_offers");

            migrationBuilder.DropColumn(
                name: "RequiresMenuChoice",
                schema: "app",
                table: "catalog_offers");
        }
    }
}
