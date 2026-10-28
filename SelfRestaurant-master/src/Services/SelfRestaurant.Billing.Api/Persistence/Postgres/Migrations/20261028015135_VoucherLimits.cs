using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfRestaurant.Billing.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class VoucherLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxTotalUses",
                schema: "billing",
                table: "Vouchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsesPerCustomer",
                schema: "billing",
                table: "Vouchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PromotionDeclined",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutQuotes_Voucher_Status",
                schema: "billing",
                table: "CheckoutQuotes",
                columns: new[] { "VoucherId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CheckoutQuotes_Voucher_Status",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "MaxTotalUses",
                schema: "billing",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "MaxUsesPerCustomer",
                schema: "billing",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "PromotionDeclined",
                schema: "billing",
                table: "CheckoutQuotes");
        }
    }
}
