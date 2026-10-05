using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Customers.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class PostgresBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customers");

            migrationBuilder.CreateTable(
                name: "CustomerFavorites",
                schema: "customers",
                columns: table => new
                {
                    CustomerFavoriteId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    DishId = table.Column<int>(type: "integer", nullable: false),
                    DishNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFavorites", x => x.CustomerFavoriteId);
                });

            migrationBuilder.CreateTable(
                name: "CustomerReviews",
                schema: "customers",
                columns: table => new
                {
                    CustomerReviewId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    DiningSessionId = table.Column<int>(type: "integer", nullable: false),
                    OverallRating = table.Column<int>(type: "integer", nullable: false),
                    FoodRating = table.Column<int>(type: "integer", nullable: false),
                    ServiceRating = table.Column<int>(type: "integer", nullable: false),
                    SpaceRating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ImageUrlsJson = table.Column<string>(type: "text", nullable: false),
                    CreateIdempotencyKey = table.Column<string>(type: "character varying(140)", unicode: false, maxLength: 140, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReviews", x => x.CustomerReviewId);
                });

            migrationBuilder.CreateTable(
                name: "InboxEvents",
                schema: "customers",
                columns: table => new
                {
                    InboxEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Source = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    SourceEventId = table.Column<long>(type: "bigint", nullable: false),
                    EventName = table.Column<string>(type: "character varying(200)", unicode: false, maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "PROCESSED"),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    NextRetryAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxEvents", x => x.InboxEventId);
                });

            migrationBuilder.CreateTable(
                name: "ReadyDishNotifications",
                schema: "customers",
                columns: table => new
                {
                    ReadyDishNotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    OrderItemId = table.Column<int>(type: "integer", nullable: true),
                    DishId = table.Column<int>(type: "integer", nullable: true),
                    DishName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    EventName = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "OPEN"),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadyDishNotifications", x => x.ReadyDishNotificationId);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                schema: "customers",
                columns: table => new
                {
                    ReservationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReservationCode = table.Column<string>(type: "character varying(40)", unicode: false, maxLength: 40, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArrivalWindowMinutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Pending"),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConvertedOrderId = table.Column<int>(type: "integer", nullable: true),
                    DiningSessionCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    CheckInStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckInIdempotencyKey = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    CheckedInAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedInByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    LastMutationKey = table.Column<string>(type: "character varying(140)", unicode: false, maxLength: 140, nullable: true),
                    LastMutationHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    PreOrderSubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.ReservationId);
                });

            migrationBuilder.CreateTable(
                name: "SupportRequests",
                schema: "customers",
                columns: table => new
                {
                    SupportRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    ReservationId = table.Column<int>(type: "integer", nullable: true),
                    PaymentId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(140)", unicode: false, maxLength: 140, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportRequests", x => x.SupportRequestId);
                });

            migrationBuilder.CreateTable(
                name: "ReservationPreOrderItems",
                schema: "customers",
                columns: table => new
                {
                    ReservationItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReservationId = table.Column<int>(type: "integer", nullable: false),
                    DishId = table.Column<int>(type: "integer", nullable: false),
                    DishNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "Pending"),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConvertedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationPreOrderItems", x => x.ReservationItemId);
                    table.ForeignKey(
                        name: "FK_ReservationPreOrderItems_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalSchema: "customers",
                        principalTable: "Reservations",
                        principalColumn: "ReservationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReservationTables",
                schema: "customers",
                columns: table => new
                {
                    ReservationTableId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReservationId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationTables", x => x.ReservationTableId);
                    table.ForeignKey(
                        name: "FK_ReservationTables_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalSchema: "customers",
                        principalTable: "Reservations",
                        principalColumn: "ReservationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFavorites_CustomerId_DishId",
                schema: "customers",
                table: "CustomerFavorites",
                columns: new[] { "CustomerId", "DishId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReviews_CreateIdempotencyKey",
                schema: "customers",
                table: "CustomerReviews",
                column: "CreateIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReviews_CustomerId",
                schema: "customers",
                table: "CustomerReviews",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReviews_InvoiceId",
                schema: "customers",
                table: "CustomerReviews",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxEvents_ReceivedAtUtc",
                schema: "customers",
                table: "InboxEvents",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_InboxEvents_Source_SourceEventId",
                schema: "customers",
                table: "InboxEvents",
                columns: new[] { "Source", "SourceEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReadyDishNotifications_Order_Item_Event",
                schema: "customers",
                table: "ReadyDishNotifications",
                columns: new[] { "OrderId", "OrderItemId", "EventName" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationPreOrderItems_Reservation_Status",
                schema: "customers",
                table: "ReservationPreOrderItems",
                columns: new[] { "ReservationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Branch_ReservedAt_Status",
                schema: "customers",
                table: "Reservations",
                columns: new[] { "BranchId", "ReservedAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PhoneNumber",
                schema: "customers",
                table: "Reservations",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "UX_Reservations_IdempotencyKey",
                schema: "customers",
                table: "Reservations",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Reservations_ReservationCode",
                schema: "customers",
                table: "Reservations",
                column: "ReservationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationTables_TableId",
                schema: "customers",
                table: "ReservationTables",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "UX_ReservationTables_Reservation_Table",
                schema: "customers",
                table: "ReservationTables",
                columns: new[] { "ReservationId", "TableId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportRequests_CustomerId",
                schema: "customers",
                table: "SupportRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportRequests_IdempotencyKey",
                schema: "customers",
                table: "SupportRequests",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportRequests_RequestCode",
                schema: "customers",
                table: "SupportRequests",
                column: "RequestCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerFavorites",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "CustomerReviews",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "InboxEvents",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "ReadyDishNotifications",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "ReservationPreOrderItems",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "ReservationTables",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "SupportRequests",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "Reservations",
                schema: "customers");
        }
    }
}
