using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Customers.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ReviewUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorNameSnapshot",
                schema: "customers",
                table: "CustomerReviews",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                schema: "customers",
                table: "CustomerReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenAtUtc",
                schema: "customers",
                table: "CustomerReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HiddenByEmployeeId",
                schema: "customers",
                table: "CustomerReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenByName",
                schema: "customers",
                table: "CustomerReviews",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenReason",
                schema: "customers",
                table: "CustomerReviews",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymous",
                schema: "customers",
                table: "CustomerReviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                schema: "customers",
                table: "CustomerReviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepliedAtUtc",
                schema: "customers",
                table: "CustomerReviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepliedByEmployeeId",
                schema: "customers",
                table: "CustomerReviews",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepliedByName",
                schema: "customers",
                table: "CustomerReviews",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyText",
                schema: "customers",
                table: "CustomerReviews",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                schema: "customers",
                table: "CustomerReviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "CustomerReviewDishRatings",
                schema: "customers",
                columns: table => new
                {
                    CustomerReviewDishRatingId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerReviewId = table.Column<int>(type: "integer", nullable: false),
                    DishId = table.Column<int>(type: "integer", nullable: false),
                    DishNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReviewDishRatings", x => x.CustomerReviewDishRatingId);
                    table.ForeignKey(
                        name: "FK_CustomerReviewDishRatings_CustomerReviews_CustomerReviewId",
                        column: x => x.CustomerReviewId,
                        principalSchema: "customers",
                        principalTable: "CustomerReviews",
                        principalColumn: "CustomerReviewId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReviews_Branch_Hidden_Created",
                schema: "customers",
                table: "CustomerReviews",
                columns: new[] { "BranchId", "IsHidden", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReviewDishRatings_DishId",
                schema: "customers",
                table: "CustomerReviewDishRatings",
                column: "DishId");

            migrationBuilder.CreateIndex(
                name: "UX_CustomerReviewDishRatings_Review_Dish",
                schema: "customers",
                table: "CustomerReviewDishRatings",
                columns: new[] { "CustomerReviewId", "DishId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerReviewDishRatings",
                schema: "customers");

            migrationBuilder.DropIndex(
                name: "IX_CustomerReviews_Branch_Hidden_Created",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "AuthorNameSnapshot",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "BranchId",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "HiddenAtUtc",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "HiddenByEmployeeId",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "HiddenByName",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "HiddenReason",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "IsAnonymous",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "RepliedAtUtc",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "RepliedByEmployeeId",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "RepliedByName",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "ReplyText",
                schema: "customers",
                table: "CustomerReviews");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                schema: "customers",
                table: "CustomerReviews");
        }
    }
}
