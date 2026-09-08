using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LifecycleStatus",
                schema: "app",
                table: "organizations",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "app",
                table: "organizations",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                schema: "app",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "app",
                table: "organizations");
        }
    }
}
