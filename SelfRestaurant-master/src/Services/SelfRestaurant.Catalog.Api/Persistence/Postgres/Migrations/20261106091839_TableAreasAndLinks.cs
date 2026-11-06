using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Catalog.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class TableAreasAndLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                schema: "catalog",
                table: "DiningTables",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiningTableLinks",
                schema: "catalog",
                columns: table => new
                {
                    TableId = table.Column<int>(type: "integer", nullable: false),
                    LinkedTableId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningTableLinks", x => new { x.TableId, x.LinkedTableId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiningTableLinks_Linked",
                schema: "catalog",
                table: "DiningTableLinks",
                column: "LinkedTableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiningTableLinks",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "Area",
                schema: "catalog",
                table: "DiningTables");
        }
    }
}
