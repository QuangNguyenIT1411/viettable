using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Orders.Api.Migrations;

public partial class ReplaceOrderItemIngredientsSynonym : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS
            (
                SELECT 1
                FROM sys.synonyms
                WHERE schema_id = SCHEMA_ID(N'dbo')
                  AND name = N'OrderItemIngredients'
            )
            BEGIN
                DROP SYNONYM dbo.OrderItemIngredients;
            END

            IF OBJECT_ID(N'dbo.OrderItemIngredients', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.OrderItemIngredients
                (
                    OrderItemIngredientID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItemIngredients PRIMARY KEY,
                    OrderItemID INT NOT NULL,
                    IngredientID INT NOT NULL,
                    IngredientName NVARCHAR(200) NULL,
                    Unit NVARCHAR(50) NULL,
                    Quantity DECIMAL(18,3) NOT NULL,
                    Note NVARCHAR(500) NULL,
                    IsRemoved BIT NOT NULL CONSTRAINT DF_OrderItemIngredients_IsRemoved DEFAULT(0),
                    CreatedAt DATETIME NULL CONSTRAINT DF_OrderItemIngredients_CreatedAt DEFAULT(GETDATE()),
                    UpdatedAt DATETIME NULL
                );
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderItemIngredients') AND name = N'IX_OrderItemIngredients_OrderItemID')
                CREATE INDEX IX_OrderItemIngredients_OrderItemID ON dbo.OrderItemIngredients(OrderItemID);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderItemIngredients') AND name = N'UQ_OrderItemIngredients_OrderItem_Ingredient')
                CREATE UNIQUE INDEX UQ_OrderItemIngredients_OrderItem_Ingredient ON dbo.OrderItemIngredients(OrderItemID, IngredientID);

            IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrderItemIngredients_OrderItems' AND parent_object_id = OBJECT_ID(N'dbo.OrderItemIngredients'))
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM dbo.OrderItemIngredients ingredient
                   WHERE NOT EXISTS
                   (
                       SELECT 1
                       FROM dbo.OrderItems item
                       WHERE item.ItemID = ingredient.OrderItemID
                   )
               )
            BEGIN
                ALTER TABLE dbo.OrderItemIngredients WITH CHECK
                ADD CONSTRAINT FK_OrderItemIngredients_OrderItems FOREIGN KEY (OrderItemID) REFERENCES dbo.OrderItems(ItemID);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
