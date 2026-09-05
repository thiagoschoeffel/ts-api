using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFrozenLotLabelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PresentationSnapshot",
                schema: "app",
                table: "frozen_lots",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProducibleNameSnapshot",
                schema: "app",
                table: "frozen_lots",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE app.frozen_lots AS lot
                SET "PresentationSnapshot" = configuration."Presentation",
                    "ProducibleNameSnapshot" = producible."Name"
                FROM app.frozen_configurations AS configuration
                JOIN app.producible_items AS producible
                  ON producible."OrganizationId" = configuration."OrganizationId"
                 AND producible."Id" = configuration."ProducibleItemId"
                WHERE lot."OrganizationId" = configuration."OrganizationId"
                  AND lot."FrozenConfigurationId" = configuration."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresentationSnapshot",
                schema: "app",
                table: "frozen_lots");

            migrationBuilder.DropColumn(
                name: "ProducibleNameSnapshot",
                schema: "app",
                table: "frozen_lots");
        }
    }
}
