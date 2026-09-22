using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Orders.Api.Migrations;

public partial class AddOrderItemIngredients : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrderItemIngredients",
            columns: table => new
            {
                OrderItemIngredientID = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                OrderItemID = table.Column<int>(type: "int", nullable: false),
                IngredientID = table.Column<int>(type: "int", nullable: false),
                IngredientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IsRemoved = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                CreatedAt = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                UpdatedAt = table.Column<DateTime>(type: "datetime", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrderItemIngredients", x => x.OrderItemIngredientID);
                table.ForeignKey(
                    name: "FK_OrderItemIngredients_OrderItems",
                    column: x => x.OrderItemID,
                    principalTable: "OrderItems",
                    principalColumn: "ItemID",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrderItemIngredients_OrderItemID",
            table: "OrderItemIngredients",
            column: "OrderItemID");

        migrationBuilder.CreateIndex(
            name: "UQ_OrderItemIngredients_OrderItem_Ingredient",
            table: "OrderItemIngredients",
            columns: new[] { "OrderItemID", "IngredientID" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OrderItemIngredients");
    }
}
