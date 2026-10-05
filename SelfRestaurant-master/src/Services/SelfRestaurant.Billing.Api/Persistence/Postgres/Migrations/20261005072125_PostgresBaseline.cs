using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfRestaurant.Billing.Api.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class PostgresBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "Bills",
                schema: "billing",
                columns: table => new
                {
                    BillID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderID = table.Column<int>(type: "integer", nullable: false),
                    DiningSessionCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    CheckoutIdempotencyKey = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    BillCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderCodeSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TableIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    TableNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BranchIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    BranchNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BillTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PointsDiscount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PointsUsed = table.Column<int>(type: "integer", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ChangeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    EmployeeID = table.Column<int>(type: "integer", nullable: true),
                    CustomerID = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.BillID);
                });

            migrationBuilder.CreateTable(
                name: "BusinessAuditLogs",
                schema: "billing",
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
                name: "CheckoutCommands",
                schema: "billing",
                columns: table => new
                {
                    CheckoutCommandId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    DiningSessionCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    BillId = table.Column<int>(type: "integer", nullable: true),
                    BillCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ChangeAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PointsUsed = table.Column<int>(type: "integer", nullable: true),
                    PointsEarned = table.Column<int>(type: "integer", nullable: true),
                    CustomerPoints = table.Column<int>(type: "integer", nullable: true),
                    PointsBefore = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "PENDING"),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutCommands", x => x.CheckoutCommandId);
                });

            migrationBuilder.CreateTable(
                name: "CheckoutQuotes",
                schema: "billing",
                columns: table => new
                {
                    CheckoutQuoteId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuoteCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    DiningSessionId = table.Column<int>(type: "integer", nullable: false),
                    DiningSessionCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    VoucherId = table.Column<int>(type: "integer", nullable: true),
                    VoucherDiscount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PointsApplied = table.Column<int>(type: "integer", nullable: false),
                    PointsDiscount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(140)", unicode: false, maxLength: 140, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutQuotes", x => x.CheckoutQuoteId);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "billing",
                columns: table => new
                {
                    InvoiceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    CheckoutQuoteId = table.Column<int>(type: "integer", nullable: false),
                    PaymentId = table.Column<int>(type: "integer", nullable: false),
                    DiningSessionId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ItemsJson = table.Column<string>(type: "text", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettlementCompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceId);
                });

            migrationBuilder.CreateTable(
                name: "MobilePayments",
                schema: "billing",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    CheckoutQuoteId = table.Column<int>(type: "integer", nullable: false),
                    DiningSessionId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    Location = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(140)", unicode: false, maxLength: 140, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: true),
                    ProviderTransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderPaymentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", unicode: false, maxLength: 3, nullable: false),
                    ProviderExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedByEmployeeId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobilePayments", x => x.PaymentId);
                });

            migrationBuilder.CreateTable(
                name: "OrderContextSnapshots",
                schema: "billing",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RefreshedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderContextSnapshots", x => x.OrderId);
                });

            migrationBuilder.CreateTable(
                name: "OutboxEvents",
                schema: "billing",
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
                name: "SplitBills",
                schema: "billing",
                columns: table => new
                {
                    SplitBillId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CheckoutQuoteId = table.Column<int>(type: "integer", nullable: false),
                    DiningSessionId = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", unicode: false, maxLength: 30, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(140)", unicode: false, maxLength: 140, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SplitBills", x => x.SplitBillId);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                schema: "billing",
                columns: table => new
                {
                    VoucherId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DiscountType = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MinSubtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxDiscount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.VoucherId);
                });

            migrationBuilder.CreateTable(
                name: "SplitBillParts",
                schema: "billing",
                columns: table => new
                {
                    SplitBillPartId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SplitBillId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    PaymentId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SplitBillParts", x => x.SplitBillPartId);
                    table.ForeignKey(
                        name: "FK_SplitBillParts_SplitBills_SplitBillId",
                        column: x => x.SplitBillId,
                        principalSchema: "billing",
                        principalTable: "SplitBills",
                        principalColumn: "SplitBillId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_BillTime",
                schema: "billing",
                table: "Bills",
                column: "BillTime");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_OrderID",
                schema: "billing",
                table: "Bills",
                column: "OrderID",
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "UX_Bills_CheckoutIdempotencyKey",
                schema: "billing",
                table: "Bills",
                column: "CheckoutIdempotencyKey",
                unique: true,
                filter: "\"CheckoutIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_BillId",
                schema: "billing",
                table: "BusinessAuditLogs",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_CreatedAtUtc",
                schema: "billing",
                table: "BusinessAuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_Entity",
                schema: "billing",
                table: "BusinessAuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditLogs_OrderId",
                schema: "billing",
                table: "BusinessAuditLogs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutCommands_CreatedAtUtc",
                schema: "billing",
                table: "CheckoutCommands",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_CheckoutCommands_IdempotencyKey",
                schema: "billing",
                table: "CheckoutCommands",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutQuotes_DiningSessionId",
                schema: "billing",
                table: "CheckoutQuotes",
                column: "DiningSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutQuotes_IdempotencyKey",
                schema: "billing",
                table: "CheckoutQuotes",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutQuotes_QuoteCode",
                schema: "billing",
                table: "CheckoutQuotes",
                column: "QuoteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CheckoutQuoteId",
                schema: "billing",
                table: "Invoices",
                column: "CheckoutQuoteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DiningSessionId",
                schema: "billing",
                table: "Invoices",
                column: "DiningSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceCode",
                schema: "billing",
                table: "Invoices",
                column: "InvoiceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobilePayments_CheckoutQuoteId",
                schema: "billing",
                table: "MobilePayments",
                column: "CheckoutQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilePayments_IdempotencyKey",
                schema: "billing",
                table: "MobilePayments",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobilePayments_PaymentCode",
                schema: "billing",
                table: "MobilePayments",
                column: "PaymentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobilePayments_ProviderReference",
                schema: "billing",
                table: "MobilePayments",
                column: "ProviderReference",
                unique: true,
                filter: "\"ProviderReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderContextSnapshots_BranchId",
                schema: "billing",
                table: "OrderContextSnapshots",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderContextSnapshots_RefreshedAtUtc",
                schema: "billing",
                table: "OrderContextSnapshots",
                column: "RefreshedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_CreatedAtUtc",
                schema: "billing",
                table: "OutboxEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxEvents_Status",
                schema: "billing",
                table: "OutboxEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SplitBillParts_SplitBillId",
                schema: "billing",
                table: "SplitBillParts",
                column: "SplitBillId");

            migrationBuilder.CreateIndex(
                name: "IX_SplitBills_IdempotencyKey",
                schema: "billing",
                table: "SplitBills",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Code",
                schema: "billing",
                table: "Vouchers",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bills",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "BusinessAuditLogs",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "CheckoutCommands",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "CheckoutQuotes",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "MobilePayments",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "OrderContextSnapshots",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "OutboxEvents",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "SplitBillParts",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "Vouchers",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "SplitBills",
                schema: "billing");
        }
    }
}
