using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Billing.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Promotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PromotionDiscount",
                schema: "billing",
                table: "Invoices",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DiscountNote",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PromotionDiscount",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PromotionId",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionName",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PromotionOfferDiscount",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VoucherOfferDiscount",
                schema: "billing",
                table: "CheckoutQuotes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PromotionDiscount",
                schema: "billing",
                table: "Bills",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PromotionId",
                schema: "billing",
                table: "Bills",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Promotions",
                schema: "billing",
                columns: table => new
                {
                    PromotionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DiscountType = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxDiscount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    MinSubtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BranchIds = table.Column<string>(type: "character varying(500)", unicode: false, maxLength: 500, nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NotifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.PromotionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_Running",
                schema: "billing",
                table: "Promotions",
                columns: new[] { "IsActive", "StartsAtUtc", "EndsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Promotions",
                schema: "billing");

            migrationBuilder.DropColumn(
                name: "PromotionDiscount",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountNote",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "PromotionDiscount",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "PromotionName",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "PromotionOfferDiscount",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "VoucherOfferDiscount",
                schema: "billing",
                table: "CheckoutQuotes");

            migrationBuilder.DropColumn(
                name: "PromotionDiscount",
                schema: "billing",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                schema: "billing",
                table: "Bills");
        }
    }
}
