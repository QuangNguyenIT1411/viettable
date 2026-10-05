using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Orders.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class PostgresBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "orders");

            migrationBuilder.CreateTable(
                name: "BusinessAuditLogs",
                schema: "orders",
                columns: table => new
                {
                    BusinessAuditLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ActionType = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: true),
                    ActorId = table.Column<int>(type: "integer", nullable: true),
                    ActorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ActorRoleCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: true),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    OrderItemId = table.Column<int>(type: "integer", nullable: true),
                    DishId = table.Column<int>(type: "integer", nullable: true),
                    BillId = table.Column<int>(type: "integer", nullable: true),
                    DiningSessionCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BeforeState = table.Column<string>(type: "text", nullable: true),
                    AfterState = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAuditLogs", x => x.BusinessAuditLogId);
                });

            migrationBuilder.CreateTable(
                name: "CatalogBranchSnapshots",
                schema: "orders",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogBranchSnapshots", x => x.BranchId);
                });

            migrationBuilder.CreateTable(
                name: "CatalogDishSnapshots",
                schema: "orders",
                columns: table => new
                {
                    DishId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Image = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Available = table.Column<bool>(type: "boolean", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogDishSnapshots", x => x.DishId);
                });

            migrationBuilder.CreateTable(
                name: "CatalogTableSnapshots",
                schema: "orders",
                columns: table => new
                {
                    TableId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    TableNumber = table.Column<int>(type: "integer", nullable: false),
                    QrCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    StatusCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StatusName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RefreshedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogTableSnapshots", x => x.TableId);
                });

            migrationBuilder.CreateTable(
                name: "DiningSessions",
                schema: "orders",
                columns: table => new
                {
                    DiningSessionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: false),
                    ReservationId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    CheckInIdempotencyKey = table.Column<string>(type: "character varying(140)", unicode: false, maxLength: 140, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    ConvertedOrderId = table.Column<int>(type: "integer", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentRequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckoutQuoteId = table.Column<int>(type: "integer", nullable: true),
                    PaymentId = table.Column<int>(type: "integer", nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningSessions", x => x.DiningSessionId);
                });

            migrationBuilder.CreateTable(
                name: "DiningSessionTables",
                schema: "orders",
                columns: table => new
                {
                    DiningSessionTableID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiningSessionCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    TableID = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningSessionTables", x => x.DiningSessionTableID);
                });

            migrationBuilder.CreateTable(
                name: "InboxEvents",
                schema: "orders",
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
                name: "OrderItemIngredients",
                schema: "orders",
                columns: table => new
                {
                    OrderItemIngredientID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderItemID = table.Column<int>(type: "integer", nullable: false),
                    IngredientID = table.Column<int>(type: "integer", nullable: false),
                    IngredientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemIngredients", x => x.OrderItemIngredientID);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                schema: "orders",
                columns: table => new
                {
                    ItemID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StatusCode = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    OrderID = table.Column<int>(type: "integer", nullable: false),
                    DishID = table.Column<int>(type: "integer", nullable: false),
                    ChefId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.ItemID);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatus",
                schema: "orders",
                columns: table => new
                {
                    StatusID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StatusCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StatusName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatus", x => x.StatusID);
                });

            migrationBuilder.CreateTable(
                name: "OutboxEvents",
                schema: "orders",
                columns: table => new
                {
                    OutboxEventId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventName = table.Column<string>(type: "character varying(200)", unicode: false, maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false, defaultValue: "PENDING"),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxEvents", x => x.OutboxEventId);
                });

            migrationBuilder.CreateTable(
                name: "ReservationPreOrders",
                schema: "orders",
                columns: table => new
                {
                    PreOrderId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReservationId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    SubmitIdempotencyKey = table.Column<string>(type: "character varying(140)", unicode: false, maxLength: 140, nullable: true),
                    SubmitRequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConvertedOrderId = table.Column<int>(type: "integer", nullable: true),
                    ConvertedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationPreOrders", x => x.PreOrderId);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRequests",
                schema: "orders",
                columns: table => new
                {
                    ServiceRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiningSessionId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: false),
                    RequestType = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(140)", unicode: false, maxLength: 140, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequests", x => x.ServiceRequestId);
                });

            migrationBuilder.CreateTable(
                name: "SubmitCommands",
                schema: "orders",
                columns: table => new
                {
                    SubmitCommandId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: false),
                    DiningSessionCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmitCommands", x => x.SubmitCommandId);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "orders",
                columns: table => new
                {
                    OrderID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DiningSessionCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    SubmitIdempotencyKey = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    SubmitRequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    OrderCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: true),
                    OrderTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletedTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    TableID = table.Column<int>(type: "integer", nullable: true),
                    CustomerID = table.Column<int>(type: "integer", nullable: true),
                    StatusID = table.Column<int>(type: "integer", nullable: false),
                    CashierID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.OrderID);
                    table.ForeignKey(
                        name: "FK_Orders_OrderStatus_StatusID",
                        column: x => x.StatusID,
                        principalSchema: "orders",
                        principalTable: "OrderStatus",
                        principalColumn: "StatusID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReservationPreOrderItems",
                schema: "orders",
                columns: table => new
                {
                    PreOrderItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PreOrderId = table.Column<int>(type: "integer", nullable: false),
                    DishId = table.Column<int>(type: "integer", nullable: false),
                    DishNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationPreOrderItems", x => x.PreOrderItemId);
                    table.ForeignKey(
                        name: "FK_ReservationPreOrderItems_ReservationPreOrders_PreOrderId",
                        column: x => x.PreOrderId,
                        principalSchema: "orders",
                        principalTable: "ReservationPreOrders",
                        principalColumn: "PreOrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_CreatedAtUtc",
                schema: "orders",
                table: "BusinessAuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_Entity",
                schema: "orders",
                table: "BusinessAuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_OrderId",
                schema: "orders",
                table: "BusinessAuditLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_TableId",
                schema: "orders",
                table: "BusinessAuditLogs",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_DiningSessions_CustomerId",
                schema: "orders",
                table: "DiningSessions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "UX_DiningSessions_ActiveTable",
                schema: "orders",
                table: "DiningSessions",
                column: "TableId",
                unique: true,
                filter: "\"EndedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_DiningSessions_CheckInIdempotencyKey",
                schema: "orders",
                table: "DiningSessions",
                column: "CheckInIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_DiningSessions_ReservationId",
                schema: "orders",
                table: "DiningSessions",
                column: "ReservationId",
                unique: true,
                filter: "\"ReservationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_DiningSessions_SessionCode",
                schema: "orders",
                table: "DiningSessions",
                column: "SessionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiningSessionTables_TableID",
                schema: "orders",
                table: "DiningSessionTables",
                column: "TableID");

            migrationBuilder.CreateIndex(
                name: "UX_DiningSessionTables_Session_Table",
                schema: "orders",
                table: "DiningSessionTables",
                columns: new[] { "DiningSessionCode", "TableID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxEvents_ReceivedAtUtc",
                schema: "orders",
                table: "InboxEvents",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_InboxEvents_Source_SourceEventId",
                schema: "orders",
                table: "InboxEvents",
                columns: new[] { "Source", "SourceEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemIngredients_OrderItemID",
                schema: "orders",
                table: "OrderItemIngredients",
                column: "OrderItemID");

            migrationBuilder.CreateIndex(
                name: "UQ_OrderItemIngredients_OrderItem_Ingredient",
                schema: "orders",
                table: "OrderItemIngredients",
                columns: new[] { "OrderItemID", "IngredientID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_orderitems_dish",
                schema: "orders",
                table: "OrderItems",
                column: "DishID");

            migrationBuilder.CreateIndex(
                name: "idx_orderitems_order",
                schema: "orders",
                table: "OrderItems",
                column: "OrderID");

            migrationBuilder.CreateIndex(
                name: "idx_orders_session",
                schema: "orders",
                table: "Orders",
                column: "DiningSessionCode");

            migrationBuilder.CreateIndex(
                name: "idx_orders_status",
                schema: "orders",
                table: "Orders",
                column: "StatusID");

            migrationBuilder.CreateIndex(
                name: "idx_orders_table",
                schema: "orders",
                table: "Orders",
                column: "TableID");

            migrationBuilder.CreateIndex(
                name: "idx_orders_time",
                schema: "orders",
                table: "Orders",
                column: "OrderTime");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderCode",
                schema: "orders",
                table: "Orders",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Orders_SubmitIdempotencyKey",
                schema: "orders",
                table: "Orders",
                column: "SubmitIdempotencyKey",
                unique: true,
                filter: "\"SubmitIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_CreatedAtUtc",
                schema: "orders",
                table: "OutboxEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_Status",
                schema: "orders",
                table: "OutboxEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationPreOrderItems_PreOrderId",
                schema: "orders",
                table: "ReservationPreOrderItems",
                column: "PreOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationPreOrders_CustomerId",
                schema: "orders",
                table: "ReservationPreOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "UX_ReservationPreOrders_ReservationId",
                schema: "orders",
                table: "ReservationPreOrders",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ReservationPreOrders_SubmitIdempotencyKey",
                schema: "orders",
                table: "ReservationPreOrders",
                column: "SubmitIdempotencyKey",
                unique: true,
                filter: "\"SubmitIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_CustomerId",
                schema: "orders",
                table: "ServiceRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_DiningSessionId",
                schema: "orders",
                table: "ServiceRequests",
                column: "DiningSessionId");

            migrationBuilder.CreateIndex(
                name: "UX_ServiceRequests_IdempotencyKey",
                schema: "orders",
                table: "ServiceRequests",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmitCommands_CreatedAtUtc",
                schema: "orders",
                table: "SubmitCommands",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_SubmitCommands_IdempotencyKey",
                schema: "orders",
                table: "SubmitCommands",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessAuditLogs",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "CatalogBranchSnapshots",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "CatalogDishSnapshots",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "CatalogTableSnapshots",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "DiningSessions",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "DiningSessionTables",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "InboxEvents",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "OrderItemIngredients",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "OrderItems",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "OutboxEvents",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "ReservationPreOrderItems",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "ServiceRequests",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "SubmitCommands",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "OrderStatus",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "ReservationPreOrders",
                schema: "orders");
        }
    }
}
