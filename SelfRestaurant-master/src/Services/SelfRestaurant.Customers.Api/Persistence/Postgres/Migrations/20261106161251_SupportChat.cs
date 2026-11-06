using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Customers.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class SupportChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportBranchHours",
                schema: "customers",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    OpenFrom = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: false),
                    OpenTo = table.Column<string>(type: "character varying(5)", unicode: false, maxLength: 5, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportBranchHours", x => x.BranchId);
                });

            migrationBuilder.CreateTable(
                name: "SupportConversations",
                schema: "customers",
                columns: table => new
                {
                    SupportConversationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    CustomerNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Topic = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    ContextType = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "NONE"),
                    ContextId = table.Column<int>(type: "integer", nullable: true),
                    ContextLabel = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "Waiting"),
                    WaitingSinceUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CustomerLastReadMessageId = table.Column<long>(type: "bigint", nullable: false),
                    StaffLastReadMessageId = table.Column<long>(type: "bigint", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportConversations", x => x.SupportConversationId);
                });

            migrationBuilder.CreateTable(
                name: "SupportMessages",
                schema: "customers",
                columns: table => new
                {
                    SupportMessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupportConversationId = table.Column<int>(type: "integer", nullable: false),
                    SenderType = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    SenderEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    SenderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportMessages", x => x.SupportMessageId);
                });

            migrationBuilder.CreateTable(
                name: "SupportQuickReplies",
                schema: "customers",
                columns: table => new
                {
                    SupportQuickReplyId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportQuickReplies", x => x.SupportQuickReplyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_Branch_Status_Waiting",
                schema: "customers",
                table: "SupportConversations",
                columns: new[] { "BranchId", "Status", "WaitingSinceUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportConversations_Customer_Last",
                schema: "customers",
                table: "SupportConversations",
                columns: new[] { "CustomerId", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_Conversation_Id",
                schema: "customers",
                table: "SupportMessages",
                columns: new[] { "SupportConversationId", "SupportMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportQuickReplies_Branch",
                schema: "customers",
                table: "SupportQuickReplies",
                columns: new[] { "BranchId", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportBranchHours",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "SupportConversations",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "SupportMessages",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "SupportQuickReplies",
                schema: "customers");
        }
    }
}
