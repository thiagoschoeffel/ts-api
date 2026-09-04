using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiTenantFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_frozen_configurations_catalog_offers_OfferId",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropForeignKey(
                name: "FK_frozen_configurations_producible_items_ProducibleItemId",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropForeignKey(
                name: "FK_frozen_lots_frozen_configurations_FrozenConfigurationId",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropForeignKey(
                name: "FK_frozen_stock_movements_frozen_lots_FrozenLotId",
                schema: "app",
                table: "frozen_stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_producible_items_NormalizedName",
                schema: "app",
                table: "producible_items");

            migrationBuilder.DropIndex(
                name: "IX_frozen_stock_movements_FrozenLotId_OccurredAt",
                schema: "app",
                table: "frozen_stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_frozen_lots_ExpiresOn_ManufacturedOn_Id",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropIndex(
                name: "IX_frozen_lots_FrozenConfigurationId",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropIndex(
                name: "IX_frozen_lots_IdempotencyKey",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropIndex(
                name: "IX_frozen_configurations_OfferId_ProducibleItemId_Presentation",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropIndex(
                name: "IX_frozen_configurations_ProducibleItemId",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropIndex(
                name: "IX_catalog_offers_NormalizedName",
                schema: "app",
                table: "catalog_offers");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                schema: "app",
                table: "producible_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                schema: "app",
                table: "frozen_stock_movements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                schema: "app",
                table: "frozen_lots",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                schema: "app",
                table: "frozen_configurations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                schema: "app",
                table: "catalog_offers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"));

            migrationBuilder.AddUniqueConstraint(
                name: "AK_producible_items_OrganizationId_Id",
                schema: "app",
                table: "producible_items",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_frozen_lots_OrganizationId_Id",
                schema: "app",
                table: "frozen_lots",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_frozen_configurations_OrganizationId_Id",
                schema: "app",
                table: "frozen_configurations",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_catalog_offers_OrganizationId_Id",
                schema: "app",
                table: "catalog_offers",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "app",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalSubject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "app",
                table: "organizations",
                columns: new[] { "Id", "Name", "Slug", "IsActive" },
                values: new object[]
                {
                    new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"),
                    "Sabor Santè",
                    "sabor-sante",
                    true,
                });

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "app",
                table: "producible_items",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"));

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "app",
                table: "frozen_stock_movements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"));

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "app",
                table: "frozen_lots",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"));

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "app",
                table: "frozen_configurations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"));

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "app",
                table: "catalog_offers",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("5f6c1eb9-6164-49e8-9349-e7d5f22b1f51"));

            migrationBuilder.CreateTable(
                name: "organization_memberships",
                schema: "app",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_memberships", x => new { x.OrganizationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_organization_memberships_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_memberships_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_producible_items_OrganizationId_NormalizedName",
                schema: "app",
                table: "producible_items",
                columns: new[] { "OrganizationId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_frozen_stock_movements_OrganizationId_FrozenLotId_OccurredAt",
                schema: "app",
                table: "frozen_stock_movements",
                columns: new[] { "OrganizationId", "FrozenLotId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_frozen_lots_OrganizationId_ExpiresOn_ManufacturedOn_Id",
                schema: "app",
                table: "frozen_lots",
                columns: new[] { "OrganizationId", "ExpiresOn", "ManufacturedOn", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_frozen_lots_OrganizationId_FrozenConfigurationId",
                schema: "app",
                table: "frozen_lots",
                columns: new[] { "OrganizationId", "FrozenConfigurationId" });

            migrationBuilder.CreateIndex(
                name: "IX_frozen_lots_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "frozen_lots",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_frozen_configurations_OrganizationId_OfferId_ProducibleItem~",
                schema: "app",
                table: "frozen_configurations",
                columns: new[] { "OrganizationId", "OfferId", "ProducibleItemId", "Presentation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_frozen_configurations_OrganizationId_ProducibleItemId",
                schema: "app",
                table: "frozen_configurations",
                columns: new[] { "OrganizationId", "ProducibleItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_offers_OrganizationId_NormalizedName",
                schema: "app",
                table: "catalog_offers",
                columns: new[] { "OrganizationId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_UserId",
                schema: "app",
                table: "organization_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Slug",
                schema: "app",
                table: "organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_ExternalSubject",
                schema: "app",
                table: "users",
                column: "ExternalSubject",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_offers_organizations_OrganizationId",
                schema: "app",
                table: "catalog_offers",
                column: "OrganizationId",
                principalSchema: "app",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_configurations_catalog_offers_OrganizationId_OfferId",
                schema: "app",
                table: "frozen_configurations",
                columns: new[] { "OrganizationId", "OfferId" },
                principalSchema: "app",
                principalTable: "catalog_offers",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_configurations_organizations_OrganizationId",
                schema: "app",
                table: "frozen_configurations",
                column: "OrganizationId",
                principalSchema: "app",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_configurations_producible_items_OrganizationId_Produ~",
                schema: "app",
                table: "frozen_configurations",
                columns: new[] { "OrganizationId", "ProducibleItemId" },
                principalSchema: "app",
                principalTable: "producible_items",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_lots_frozen_configurations_OrganizationId_FrozenConf~",
                schema: "app",
                table: "frozen_lots",
                columns: new[] { "OrganizationId", "FrozenConfigurationId" },
                principalSchema: "app",
                principalTable: "frozen_configurations",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_lots_organizations_OrganizationId",
                schema: "app",
                table: "frozen_lots",
                column: "OrganizationId",
                principalSchema: "app",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_stock_movements_frozen_lots_OrganizationId_FrozenLot~",
                schema: "app",
                table: "frozen_stock_movements",
                columns: new[] { "OrganizationId", "FrozenLotId" },
                principalSchema: "app",
                principalTable: "frozen_lots",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_stock_movements_organizations_OrganizationId",
                schema: "app",
                table: "frozen_stock_movements",
                column: "OrganizationId",
                principalSchema: "app",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_producible_items_organizations_OrganizationId",
                schema: "app",
                table: "producible_items",
                column: "OrganizationId",
                principalSchema: "app",
                principalTable: "organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_catalog_offers_organizations_OrganizationId",
                schema: "app",
                table: "catalog_offers");

            migrationBuilder.DropForeignKey(
                name: "FK_frozen_configurations_catalog_offers_OrganizationId_OfferId",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropForeignKey(
                name: "FK_frozen_configurations_organizations_OrganizationId",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropForeignKey(
                name: "FK_frozen_configurations_producible_items_OrganizationId_Produ~",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropForeignKey(
                name: "FK_frozen_lots_frozen_configurations_OrganizationId_FrozenConf~",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropForeignKey(
                name: "FK_frozen_lots_organizations_OrganizationId",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropForeignKey(
                name: "FK_frozen_stock_movements_frozen_lots_OrganizationId_FrozenLot~",
                schema: "app",
                table: "frozen_stock_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_frozen_stock_movements_organizations_OrganizationId",
                schema: "app",
                table: "frozen_stock_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_producible_items_organizations_OrganizationId",
                schema: "app",
                table: "producible_items");

            migrationBuilder.DropTable(
                name: "organization_memberships",
                schema: "app");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "users",
                schema: "app");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_producible_items_OrganizationId_Id",
                schema: "app",
                table: "producible_items");

            migrationBuilder.DropIndex(
                name: "IX_producible_items_OrganizationId_NormalizedName",
                schema: "app",
                table: "producible_items");

            migrationBuilder.DropIndex(
                name: "IX_frozen_stock_movements_OrganizationId_FrozenLotId_OccurredAt",
                schema: "app",
                table: "frozen_stock_movements");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_frozen_lots_OrganizationId_Id",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropIndex(
                name: "IX_frozen_lots_OrganizationId_ExpiresOn_ManufacturedOn_Id",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropIndex(
                name: "IX_frozen_lots_OrganizationId_FrozenConfigurationId",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropIndex(
                name: "IX_frozen_lots_OrganizationId_IdempotencyKey",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_frozen_configurations_OrganizationId_Id",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropIndex(
                name: "IX_frozen_configurations_OrganizationId_OfferId_ProducibleItem~",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropIndex(
                name: "IX_frozen_configurations_OrganizationId_ProducibleItemId",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_catalog_offers_OrganizationId_Id",
                schema: "app",
                table: "catalog_offers");

            migrationBuilder.DropIndex(
                name: "IX_catalog_offers_OrganizationId_NormalizedName",
                schema: "app",
                table: "catalog_offers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                schema: "app",
                table: "producible_items");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                schema: "app",
                table: "frozen_stock_movements");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                schema: "app",
                table: "frozen_configurations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                schema: "app",
                table: "catalog_offers");

            migrationBuilder.CreateIndex(
                name: "IX_producible_items_NormalizedName",
                schema: "app",
                table: "producible_items",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_frozen_stock_movements_FrozenLotId_OccurredAt",
                schema: "app",
                table: "frozen_stock_movements",
                columns: new[] { "FrozenLotId", "OccurredAt" });

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
                name: "IX_catalog_offers_NormalizedName",
                schema: "app",
                table: "catalog_offers",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_configurations_catalog_offers_OfferId",
                schema: "app",
                table: "frozen_configurations",
                column: "OfferId",
                principalSchema: "app",
                principalTable: "catalog_offers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_configurations_producible_items_ProducibleItemId",
                schema: "app",
                table: "frozen_configurations",
                column: "ProducibleItemId",
                principalSchema: "app",
                principalTable: "producible_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_lots_frozen_configurations_FrozenConfigurationId",
                schema: "app",
                table: "frozen_lots",
                column: "FrozenConfigurationId",
                principalSchema: "app",
                principalTable: "frozen_configurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_frozen_stock_movements_frozen_lots_FrozenLotId",
                schema: "app",
                table: "frozen_stock_movements",
                column: "FrozenLotId",
                principalSchema: "app",
                principalTable: "frozen_lots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
