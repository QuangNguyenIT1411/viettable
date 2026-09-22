using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Orders.Api.Migrations;

public partial class AddOrderItemIngredientSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.OrderItemIngredients', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'dbo.OrderItemIngredients', N'IngredientName') IS NULL
                    ALTER TABLE dbo.OrderItemIngredients ADD IngredientName NVARCHAR(200) NULL;

                IF COL_LENGTH(N'dbo.OrderItemIngredients', N'Unit') IS NULL
                    ALTER TABLE dbo.OrderItemIngredients ADD Unit NVARCHAR(50) NULL;

                IF COL_LENGTH(N'dbo.OrderItemIngredients', N'Note') IS NULL
                    ALTER TABLE dbo.OrderItemIngredients ADD Note NVARCHAR(500) NULL;

                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrderItemIngredients_Ingredients' AND parent_object_id = OBJECT_ID(N'dbo.OrderItemIngredients'))
                    ALTER TABLE dbo.OrderItemIngredients DROP CONSTRAINT FK_OrderItemIngredients_Ingredients;

                IF EXISTS
                (
                    SELECT 1
                    FROM sys.foreign_keys fk
                    WHERE fk.name = N'FK_OrderItemIngredients_OrderItems'
                      AND fk.parent_object_id = OBJECT_ID(N'dbo.OrderItemIngredients')
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM sys.foreign_key_columns fkc
                          JOIN sys.columns parent_col
                            ON parent_col.object_id = fkc.parent_object_id
                           AND parent_col.column_id = fkc.parent_column_id
                          JOIN sys.columns ref_col
                            ON ref_col.object_id = fkc.referenced_object_id
                           AND ref_col.column_id = fkc.referenced_column_id
                          WHERE fkc.constraint_object_id = fk.object_id
                            AND parent_col.name = N'OrderItemID'
                            AND fkc.referenced_object_id = OBJECT_ID(N'dbo.OrderItems')
                            AND ref_col.name = N'ItemID'
                      )
                )
                    ALTER TABLE dbo.OrderItemIngredients DROP CONSTRAINT FK_OrderItemIngredients_OrderItems;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
