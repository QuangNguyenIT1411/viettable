using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Catalog.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class DishPlanningAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Course",
                schema: "catalog",
                table: "Dishes",
                type: "character varying(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSpicy",
                schema: "catalog",
                table: "Dishes",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Servings",
                schema: "catalog",
                table: "Dishes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Course",
                schema: "catalog",
                table: "Dishes");

            migrationBuilder.DropColumn(
                name: "IsSpicy",
                schema: "catalog",
                table: "Dishes");

            migrationBuilder.DropColumn(
                name: "Servings",
                schema: "catalog",
                table: "Dishes");
        }
    }
}
