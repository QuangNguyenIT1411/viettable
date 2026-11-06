using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Catalog.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class BranchCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                schema: "catalog",
                table: "Branches",
                type: "numeric(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                schema: "catalog",
                table: "Branches",
                type: "numeric(9,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "catalog",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "catalog",
                table: "Branches");
        }
    }
}
