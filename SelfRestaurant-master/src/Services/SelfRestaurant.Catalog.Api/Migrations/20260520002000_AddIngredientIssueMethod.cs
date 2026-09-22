using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Catalog.Api.Migrations;

public partial class AddIngredientIssueMethod : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "IssueMethod",
            table: "Ingredients",
            type: "varchar(10)",
            unicode: false,
            maxLength: 10,
            nullable: false,
            defaultValue: "FEFO");

        migrationBuilder.Sql(
            """
            UPDATE dbo.Ingredients
            SET IssueMethod = 'FIFO'
            WHERE Name IN
            (
                N'Gạo',
                N'Đường',
                N'Muối',
                N'Tiêu',
                N'Dầu ăn',
                N'Nước mắm',
                N'Cà phê',
                N'Trà',
                N'Sữa đặc',
                N'Gia vị cà ri',
                N'Khoai tây',
                N'Bánh đa nem'
            );

            UPDATE dbo.Ingredients
            SET IssueMethod = 'FEFO'
            WHERE Name IN
            (
                N'Rau cải',
                N'Xà lách',
                N'Rau xà lách',
                N'Rau thơm',
                N'Thịt gà',
                N'Thịt bò',
                N'Thịt heo',
                N'Cá',
                N'Tôm',
                N'Trứng',
                N'Sữa tươi',
                N'Bún',
                N'Bánh phở',
                N'Nước dùng',
                N'Dưa hấu',
                N'Bơ',
                N'Cam tươi'
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IssueMethod",
            table: "Ingredients");
    }
}
