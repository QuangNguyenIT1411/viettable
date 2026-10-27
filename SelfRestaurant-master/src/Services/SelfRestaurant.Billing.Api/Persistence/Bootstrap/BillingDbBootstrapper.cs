using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SelfRestaurant.Billing.Api.Persistence;

public static class BillingDbBootstrapper
{
    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var contextTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Billing", "context-resolution");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        contextTiming?.Dispose();

        using var providerTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Billing", "context-provider-init");
        var isPostgres = db.Database.IsNpgsql();
        providerTiming?.Dispose();
        if (isPostgres)
        {
            await SelfRestaurant.CloudCompatibility.PostgresBootstrap.ValidateOrMigrateAsync(db,
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(), cancellationToken);
            return;
        }

        await WaitForDatabaseAsync(db, logger, cancellationToken);
        await EnsureOutboxTableAsync(db, cancellationToken);
        await EnsureOwnedBillsAsync(db, cancellationToken);
        await EnsureBusinessAuditTableAsync(db, cancellationToken);
        await EnsureOrderContextSnapshotTableAsync(db, cancellationToken);
        await EnsureCheckoutCommandTableAsync(db, cancellationToken);
        await EnsureMobileCheckoutTablesAsync(db, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.MobilePayments', 'ConfirmedByEmployeeId') IS NULL
                ALTER TABLE dbo.MobilePayments ADD ConfirmedByEmployeeId INT NULL;
            IF COL_LENGTH('dbo.Invoices', 'SettlementCompletedAtUtc') IS NULL
            BEGIN
                ALTER TABLE dbo.Invoices ADD SettlementCompletedAtUtc DATETIME2 NULL;
                EXEC('UPDATE dbo.Invoices SET SettlementCompletedAtUtc = IssuedAtUtc');
            END;
            IF COL_LENGTH('dbo.Invoices', 'RecoveryAttemptCount') IS NULL
                ALTER TABLE dbo.Invoices ADD RecoveryAttemptCount INT NOT NULL CONSTRAINT DF_Invoices_RecoveryAttemptCount DEFAULT (0) WITH VALUES;
            IF COL_LENGTH('dbo.Invoices', 'RecoveryLastAttemptAtUtc') IS NULL
                ALTER TABLE dbo.Invoices ADD RecoveryLastAttemptAtUtc DATETIME2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Invoices') AND name=N'IX_Invoices_RecoveryPending')
                CREATE INDEX IX_Invoices_RecoveryPending ON dbo.Invoices(SettlementCompletedAtUtc, RecoveryLastAttemptAtUtc, InvoiceId);
            IF OBJECT_ID(N'dbo.BankTransferEvents', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.BankTransferEvents(
                    BankTransferEventId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BankTransferEvents PRIMARY KEY,
                    Provider VARCHAR(20) NOT NULL, ProviderTransactionId VARCHAR(100) NOT NULL, ReferenceCode VARCHAR(100) NULL,
                    AccountNumber VARCHAR(40) NOT NULL, SubAccount VARCHAR(40) NULL, Amount DECIMAL(18,2) NOT NULL,
                    Content NVARCHAR(500) NOT NULL, TransactionDate VARCHAR(40) NULL, Outcome VARCHAR(30) NOT NULL,
                    Overpaid BIT NOT NULL, BranchId INT NULL, PaymentId INT NULL, ReceivedAtUtc DATETIME2 NOT NULL,
                    ProcessedAtUtc DATETIME2 NULL, ResolvedAtUtc DATETIME2 NULL, ResolvedByEmployeeId INT NULL);
                CREATE UNIQUE INDEX UX_BankTransferEvents_ProviderTransaction ON dbo.BankTransferEvents(Provider, ProviderTransactionId);
                CREATE INDEX IX_BankTransferEvents_BranchOutcome ON dbo.BankTransferEvents(BranchId, Outcome, ResolvedAtUtc);
            END;
            """, cancellationToken);
        // Reservation deposits (docs/DEPOSIT_PLAN.md); PostgreSQL gets them from migration ReservationDeposits.
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.CheckoutQuotes', 'DepositAmount') IS NULL
                ALTER TABLE dbo.CheckoutQuotes ADD DepositAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CheckoutQuotes_DepositAmount DEFAULT (0) WITH VALUES;
            IF COL_LENGTH('dbo.CheckoutQuotes', 'DepositApplied') IS NULL
                ALTER TABLE dbo.CheckoutQuotes ADD DepositApplied DECIMAL(18,2) NOT NULL CONSTRAINT DF_CheckoutQuotes_DepositApplied DEFAULT (0) WITH VALUES;
            IF COL_LENGTH('dbo.CheckoutQuotes', 'ReservationDepositId') IS NULL
                ALTER TABLE dbo.CheckoutQuotes ADD ReservationDepositId INT NULL;
            IF COL_LENGTH('dbo.Invoices', 'DepositApplied') IS NULL
                ALTER TABLE dbo.Invoices ADD DepositApplied DECIMAL(18,2) NOT NULL CONSTRAINT DF_Invoices_DepositApplied DEFAULT (0) WITH VALUES;
            IF OBJECT_ID(N'dbo.ReservationDeposits', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ReservationDeposits(
                    ReservationDepositId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReservationDeposits PRIMARY KEY,
                    ReservationId INT NOT NULL, CustomerId INT NOT NULL, BranchId INT NOT NULL, PartySize INT NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL, Status VARCHAR(30) NOT NULL, ReservedAtUtc DATETIME2 NOT NULL, DueAtUtc DATETIME2 NOT NULL,
                    PaidAmount DECIMAL(18,2) NULL, PaidAtUtc DATETIME2 NULL, ProviderTransactionId VARCHAR(100) NULL,
                    AppliedCheckoutQuoteId INT NULL, AppliedAmount DECIMAL(18,2) NULL, AppliedAtUtc DATETIME2 NULL,
                    ClosedReason NVARCHAR(300) NULL, RefundedAtUtc DATETIME2 NULL, RefundedByEmployeeId INT NULL,
                    CreatedAtUtc DATETIME2 NOT NULL, UpdatedAtUtc DATETIME2 NOT NULL);
                CREATE UNIQUE INDEX UX_ReservationDeposits_ReservationId ON dbo.ReservationDeposits(ReservationId);
                CREATE INDEX IX_ReservationDeposits_BranchStatus ON dbo.ReservationDeposits(BranchId, Status);
            END;
            """, cancellationToken);
        // Promotions (docs/PROMO_PLAN.md); PostgreSQL gets them from migration Promotions.
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.CheckoutQuotes', 'PromotionId') IS NULL ALTER TABLE dbo.CheckoutQuotes ADD PromotionId INT NULL;
            IF COL_LENGTH('dbo.CheckoutQuotes', 'PromotionName') IS NULL ALTER TABLE dbo.CheckoutQuotes ADD PromotionName NVARCHAR(200) NULL;
            IF COL_LENGTH('dbo.CheckoutQuotes', 'PromotionOfferDiscount') IS NULL
                ALTER TABLE dbo.CheckoutQuotes ADD PromotionOfferDiscount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CheckoutQuotes_PromotionOffer DEFAULT (0) WITH VALUES;
            IF COL_LENGTH('dbo.CheckoutQuotes', 'PromotionDiscount') IS NULL
                ALTER TABLE dbo.CheckoutQuotes ADD PromotionDiscount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CheckoutQuotes_PromotionDiscount DEFAULT (0) WITH VALUES;
            IF COL_LENGTH('dbo.CheckoutQuotes', 'VoucherOfferDiscount') IS NULL
                ALTER TABLE dbo.CheckoutQuotes ADD VoucherOfferDiscount DECIMAL(18,2) NOT NULL CONSTRAINT DF_CheckoutQuotes_VoucherOffer DEFAULT (0) WITH VALUES;
            IF COL_LENGTH('dbo.CheckoutQuotes', 'DiscountNote') IS NULL ALTER TABLE dbo.CheckoutQuotes ADD DiscountNote NVARCHAR(300) NULL;
            IF COL_LENGTH('dbo.Invoices', 'PromotionDiscount') IS NULL
                ALTER TABLE dbo.Invoices ADD PromotionDiscount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Invoices_PromotionDiscount DEFAULT (0) WITH VALUES;
            IF COL_LENGTH('dbo.Bills', 'PromotionId') IS NULL ALTER TABLE dbo.Bills ADD PromotionId INT NULL;
            IF COL_LENGTH('dbo.Bills', 'PromotionDiscount') IS NULL
                ALTER TABLE dbo.Bills ADD PromotionDiscount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Bills_PromotionDiscount DEFAULT (0) WITH VALUES;
            IF OBJECT_ID(N'dbo.Promotions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Promotions(
                    PromotionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Promotions PRIMARY KEY,
                    Name NVARCHAR(200) NOT NULL, Description NVARCHAR(1000) NULL, ImageUrl NVARCHAR(1000) NULL,
                    DiscountType VARCHAR(20) NOT NULL, DiscountValue DECIMAL(18,2) NOT NULL, MaxDiscount DECIMAL(18,2) NULL,
                    MinSubtotal DECIMAL(18,2) NOT NULL, BranchIds VARCHAR(500) NULL, StartsAtUtc DATETIME2 NOT NULL, EndsAtUtc DATETIME2 NOT NULL,
                    IsActive BIT NOT NULL, NotifiedAtUtc DATETIME2 NULL, CreatedAtUtc DATETIME2 NOT NULL, UpdatedAtUtc DATETIME2 NOT NULL);
                CREATE INDEX IX_Promotions_Running ON dbo.Promotions(IsActive, StartsAtUtc, EndsAtUtc);
            END;
            """, cancellationToken);
        await SeedReferenceDataAsync(db, logger, cancellationToken);
    }

    private static async Task EnsureMobileCheckoutTablesAsync(BillingDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.CheckoutQuotes', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CheckoutQuotes(
                    CheckoutQuoteId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CheckoutQuotes PRIMARY KEY,
                    QuoteCode VARCHAR(50) NOT NULL, DiningSessionId INT NOT NULL, DiningSessionCode VARCHAR(64) NOT NULL,
                    CustomerId INT NOT NULL, BranchId INT NOT NULL, TableId INT NOT NULL, Status VARCHAR(30) NOT NULL,
                    Subtotal DECIMAL(18,2) NOT NULL, VoucherId INT NULL, VoucherDiscount DECIMAL(18,2) NOT NULL,
                    PointsApplied INT NOT NULL, PointsDiscount DECIMAL(18,2) NOT NULL, TotalAmount DECIMAL(18,2) NOT NULL,
                    IdempotencyKey VARCHAR(140) NOT NULL, RequestHash VARCHAR(64) NOT NULL, ItemsJson NVARCHAR(MAX) NOT NULL,
                    ExpiresAtUtc DATETIME2 NOT NULL, CreatedAtUtc DATETIME2 NOT NULL, UpdatedAtUtc DATETIME2 NOT NULL);
                CREATE UNIQUE INDEX UX_CheckoutQuotes_QuoteCode ON dbo.CheckoutQuotes(QuoteCode);
                CREATE UNIQUE INDEX UX_CheckoutQuotes_IdempotencyKey ON dbo.CheckoutQuotes(IdempotencyKey);
                CREATE INDEX IX_CheckoutQuotes_DiningSessionId ON dbo.CheckoutQuotes(DiningSessionId);
            END;
            IF OBJECT_ID(N'dbo.Vouchers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Vouchers(
                    VoucherId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vouchers PRIMARY KEY, Code VARCHAR(50) NOT NULL,
                    Name NVARCHAR(200) NOT NULL, DiscountType VARCHAR(20) NOT NULL, DiscountValue DECIMAL(18,2) NOT NULL,
                    MinSubtotal DECIMAL(18,2) NOT NULL, MaxDiscount DECIMAL(18,2) NULL, IsActive BIT NOT NULL,
                    StartsAtUtc DATETIME2 NOT NULL, EndsAtUtc DATETIME2 NOT NULL);
                CREATE UNIQUE INDEX UX_Vouchers_Code ON dbo.Vouchers(Code);
            END;
            IF OBJECT_ID(N'dbo.MobilePayments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MobilePayments(
                    PaymentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY, PaymentCode VARCHAR(50) NOT NULL,
                    CheckoutQuoteId INT NOT NULL, DiningSessionId INT NOT NULL, CustomerId INT NOT NULL, Amount DECIMAL(18,2) NOT NULL,
                    Channel VARCHAR(20) NOT NULL, Method VARCHAR(20) NOT NULL, Location VARCHAR(20) NULL, Status VARCHAR(30) NOT NULL,
                    AttemptNumber INT NOT NULL, IdempotencyKey VARCHAR(140) NOT NULL, RequestHash VARCHAR(64) NOT NULL,
                    ProviderReference NVARCHAR(100) NULL, ProviderName VARCHAR(30) NULL, ProviderTransactionId NVARCHAR(100) NULL,
                    ProviderPaymentUrl NVARCHAR(1000) NULL, Currency VARCHAR(3) NOT NULL CONSTRAINT DF_MobilePayments_Currency DEFAULT ('VND'),
                    ProviderExpiresAtUtc DATETIME2 NULL, FailureCode VARCHAR(100) NULL, CreatedAtUtc DATETIME2 NOT NULL,
                    UpdatedAtUtc DATETIME2 NOT NULL, CompletedAtUtc DATETIME2 NULL);
                CREATE UNIQUE INDEX UX_MobilePayments_PaymentCode ON dbo.MobilePayments(PaymentCode);
                CREATE UNIQUE INDEX UX_MobilePayments_IdempotencyKey ON dbo.MobilePayments(IdempotencyKey);
                CREATE INDEX IX_MobilePayments_CheckoutQuoteId ON dbo.MobilePayments(CheckoutQuoteId);
            END;
            IF COL_LENGTH('dbo.MobilePayments','ProviderName') IS NULL ALTER TABLE dbo.MobilePayments ADD ProviderName VARCHAR(30) NULL;
            IF COL_LENGTH('dbo.MobilePayments','ProviderTransactionId') IS NULL ALTER TABLE dbo.MobilePayments ADD ProviderTransactionId NVARCHAR(100) NULL;
            IF COL_LENGTH('dbo.MobilePayments','ProviderPaymentUrl') IS NULL ALTER TABLE dbo.MobilePayments ADD ProviderPaymentUrl NVARCHAR(1000) NULL;
            IF COL_LENGTH('dbo.MobilePayments','Currency') IS NULL ALTER TABLE dbo.MobilePayments ADD Currency VARCHAR(3) NOT NULL CONSTRAINT DF_MobilePayments_Currency DEFAULT ('VND');
            IF COL_LENGTH('dbo.MobilePayments','ProviderExpiresAtUtc') IS NULL ALTER TABLE dbo.MobilePayments ADD ProviderExpiresAtUtc DATETIME2 NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MobilePayments') AND name=N'UX_MobilePayments_ProviderReference')
                CREATE UNIQUE INDEX UX_MobilePayments_ProviderReference ON dbo.MobilePayments(ProviderReference) WHERE ProviderReference IS NOT NULL;
            IF OBJECT_ID(N'dbo.Invoices', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Invoices(
                    InvoiceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoices PRIMARY KEY, InvoiceCode VARCHAR(50) NOT NULL,
                    CheckoutQuoteId INT NOT NULL, PaymentId INT NOT NULL, DiningSessionId INT NOT NULL, CustomerId INT NOT NULL,
                    Subtotal DECIMAL(18,2) NOT NULL, Discount DECIMAL(18,2) NOT NULL, TotalAmount DECIMAL(18,2) NOT NULL,
                    ItemsJson NVARCHAR(MAX) NOT NULL, IssuedAtUtc DATETIME2 NOT NULL);
                CREATE UNIQUE INDEX UX_Invoices_InvoiceCode ON dbo.Invoices(InvoiceCode);
                CREATE UNIQUE INDEX UX_Invoices_CheckoutQuoteId ON dbo.Invoices(CheckoutQuoteId);
                CREATE UNIQUE INDEX UX_Invoices_DiningSessionId ON dbo.Invoices(DiningSessionId);
            END;
            IF OBJECT_ID(N'dbo.SplitBills', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SplitBills(
                    SplitBillId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SplitBills PRIMARY KEY, CheckoutQuoteId INT NOT NULL,
                    DiningSessionId INT NOT NULL, CustomerId INT NOT NULL, TotalAmount DECIMAL(18,2) NOT NULL, Status VARCHAR(30) NOT NULL,
                    IdempotencyKey VARCHAR(140) NOT NULL, RequestHash VARCHAR(64) NOT NULL, CreatedAtUtc DATETIME2 NOT NULL, UpdatedAtUtc DATETIME2 NOT NULL);
                CREATE UNIQUE INDEX UX_SplitBills_IdempotencyKey ON dbo.SplitBills(IdempotencyKey);
            END;
            IF OBJECT_ID(N'dbo.SplitBillParts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SplitBillParts(
                    SplitBillPartId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SplitBillParts PRIMARY KEY,
                    SplitBillId INT NOT NULL, Label NVARCHAR(100) NOT NULL, Amount DECIMAL(18,2) NOT NULL,
                    Status VARCHAR(20) NOT NULL, PaymentId INT NULL,
                    CONSTRAINT FK_SplitBillParts_SplitBills FOREIGN KEY(SplitBillId) REFERENCES dbo.SplitBills(SplitBillId) ON DELETE CASCADE);
                CREATE INDEX IX_SplitBillParts_SplitBillId ON dbo.SplitBillParts(SplitBillId);
            END;
            IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE Code = 'WELCOME10')
                INSERT dbo.Vouchers(Code,Name,DiscountType,DiscountValue,MinSubtotal,MaxDiscount,IsActive,StartsAtUtc,EndsAtUtc)
                VALUES('WELCOME10',N'Giảm 10% đơn đầu','PERCENT',10,10000,50000,1,DATEADD(year,-1,SYSUTCDATETIME()),DATEADD(year,10,SYSUTCDATETIME()));
            IF NOT EXISTS (SELECT 1 FROM dbo.Vouchers WHERE Code = 'VIETTABLE50')
                INSERT dbo.Vouchers(Code,Name,DiscountType,DiscountValue,MinSubtotal,MaxDiscount,IsActive,StartsAtUtc,EndsAtUtc)
                VALUES('VIETTABLE50',N'Giảm 50.000đ','FIXED',50000,300000,NULL,1,DATEADD(year,-1,SYSUTCDATETIME()),DATEADD(year,10,SYSUTCDATETIME()));
            """, cancellationToken);
    }

    private static async Task WaitForDatabaseAsync(BillingDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 1; attempt <= 60; attempt++)
        {
            try
            {
                await db.Database.OpenConnectionAsync(cancellationToken);
                try
                {
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = "SELECT 1";
                    command.CommandType = CommandType.Text;
                    _ = await command.ExecuteScalarAsync(cancellationToken);
                }
                finally
                {
                    try
                    {
                        await db.Database.CloseConnectionAsync();
                    }
                    catch
                    {
                        // ignored
                    }
                }

                return;
            }
            catch (Exception ex) when (attempt < 60)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}/60). Waiting {Delay}...", attempt, delay);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 10));
            }
        }
    }

    private static Task SeedReferenceDataAsync(BillingDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("Billing reference seed skipped; service no longer owns OrderStatus/TableStatus.");
        return Task.CompletedTask;
    }

    private static async Task EnsureBusinessAuditTableAsync(BillingDbContext db, CancellationToken cancellationToken)
    {
        const string createSql = """
                                 IF OBJECT_ID(N'dbo.BusinessAuditLogs', N'U') IS NULL
                                 BEGIN
                                     CREATE TABLE dbo.BusinessAuditLogs
                                     (
                                         BusinessAuditLogId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                         CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_BillingBusinessAuditLogs_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                                         ActionType VARCHAR(100) NOT NULL,
                                         EntityType VARCHAR(50) NOT NULL,
                                         EntityId NVARCHAR(100) NOT NULL,
                                         ActorType VARCHAR(30) NULL,
                                         ActorId INT NULL,
                                         ActorCode NVARCHAR(100) NULL,
                                         ActorName NVARCHAR(200) NULL,
                                         ActorRoleCode VARCHAR(50) NULL,
                                         TableId INT NULL,
                                         OrderId INT NULL,
                                         OrderItemId INT NULL,
                                         DishId INT NULL,
                                         BillId INT NULL,
                                         DiningSessionCode VARCHAR(64) NULL,
                                         CorrelationId VARCHAR(100) NULL,
                                         IdempotencyKey VARCHAR(100) NULL,
                                         Notes NVARCHAR(500) NULL,
                                         BeforeState NVARCHAR(MAX) NULL,
                                         AfterState NVARCHAR(MAX) NULL
                                     );
                                 END
                                 """;

        const string indexSql = """
                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_CreatedAtUtc')
                                    CREATE INDEX IX_BusinessAuditLogs_CreatedAtUtc ON dbo.BusinessAuditLogs(CreatedAtUtc);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_Entity')
                                    CREATE INDEX IX_BusinessAuditLogs_Entity ON dbo.BusinessAuditLogs(EntityType, EntityId);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_OrderId')
                                    CREATE INDEX IX_BusinessAuditLogs_OrderId ON dbo.BusinessAuditLogs(OrderId);

                                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_BillId')
                                    CREATE INDEX IX_BusinessAuditLogs_BillId ON dbo.BusinessAuditLogs(BillId);
                                """;

        await db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(indexSql, cancellationToken);
    }

    private static async Task EnsureOutboxTableAsync(BillingDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.OutboxEvents', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.OutboxEvents
                (
                    OutboxEventId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    EventName VARCHAR(200) NOT NULL,
                    OccurredAtUtc DATETIME2 NOT NULL,
                    Source VARCHAR(100) NOT NULL,
                    CorrelationId VARCHAR(100) NULL,
                    PayloadJson NVARCHAR(MAX) NOT NULL,
                    Status VARCHAR(30) NOT NULL CONSTRAINT DF_OutboxEvents_Status DEFAULT ('PENDING'),
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_OutboxEvents_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                    ProcessedAtUtc DATETIME2 NULL,
                    Error NVARCHAR(MAX) NULL
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OutboxEvents') AND name = N'IX_OutboxEvents_Status')
                CREATE INDEX IX_OutboxEvents_Status ON dbo.OutboxEvents(Status);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OutboxEvents') AND name = N'IX_OutboxEvents_CreatedAtUtc')
                CREATE INDEX IX_OutboxEvents_CreatedAtUtc ON dbo.OutboxEvents(CreatedAtUtc);
            """, cancellationToken);
    }

    private static async Task EnsureOwnedBillsAsync(BillingDbContext db, CancellationToken cancellationToken)
    {
        using var stream = typeof(BillingDbBootstrapper).Assembly.GetManifestResourceStream("Billing.OwnedTables.sql")
            ?? throw new InvalidOperationException("Billing owned schema resource missing");
        using var reader = new StreamReader(stream);
        await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync(cancellationToken), cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            IF EXISTS (SELECT 1 FROM sys.synonyms WHERE name = 'Bills')
               AND NOT EXISTS (SELECT 1 FROM billing.OwnershipMigrations WHERE MigrationId = 'billing-bills-v1')
                THROW 51000, 'Run explicit Billing migration before application cutover.', 1;
            """, cancellationToken);
    }

    private static async Task EnsureCheckoutCommandTableAsync(BillingDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.CheckoutCommands', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CheckoutCommands
                (
                    CheckoutCommandId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    IdempotencyKey VARCHAR(100) NOT NULL,
                    OrderId INT NOT NULL,
                    DiningSessionCode VARCHAR(64) NULL,
                    BillId INT NULL,
                    BillCode NVARCHAR(50) NULL,
                    TotalAmount DECIMAL(18, 2) NULL,
                    ChangeAmount DECIMAL(18, 2) NULL,
                    PointsUsed INT NULL,
                    PointsEarned INT NULL,
                    CustomerPoints INT NULL,
                    PointsBefore INT NULL,
                    CustomerName NVARCHAR(200) NULL,
                    Status VARCHAR(20) NOT NULL CONSTRAINT DF_CheckoutCommands_Status DEFAULT ('PENDING'),
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CheckoutCommands_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                    CompletedAtUtc DATETIME2 NULL,
                    Error NVARCHAR(MAX) NULL
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'dbo.CheckoutCommands', 'BillCode') IS NULL
                ALTER TABLE dbo.CheckoutCommands ADD BillCode NVARCHAR(50) NULL;
            IF COL_LENGTH(N'dbo.CheckoutCommands', 'TotalAmount') IS NULL
                ALTER TABLE dbo.CheckoutCommands ADD TotalAmount DECIMAL(18, 2) NULL;
            IF COL_LENGTH(N'dbo.CheckoutCommands', 'ChangeAmount') IS NULL
                ALTER TABLE dbo.CheckoutCommands ADD ChangeAmount DECIMAL(18, 2) NULL;
            IF COL_LENGTH(N'dbo.CheckoutCommands', 'PointsUsed') IS NULL
                ALTER TABLE dbo.CheckoutCommands ADD PointsUsed INT NULL;
            IF COL_LENGTH(N'dbo.CheckoutCommands', 'PointsEarned') IS NULL
                ALTER TABLE dbo.CheckoutCommands ADD PointsEarned INT NULL;
            IF COL_LENGTH(N'dbo.CheckoutCommands', 'CustomerPoints') IS NULL
                ALTER TABLE dbo.CheckoutCommands ADD CustomerPoints INT NULL;
            IF COL_LENGTH(N'dbo.CheckoutCommands', 'PointsBefore') IS NULL
                ALTER TABLE dbo.CheckoutCommands ADD PointsBefore INT NULL;
            IF COL_LENGTH(N'dbo.CheckoutCommands', 'CustomerName') IS NULL
                ALTER TABLE dbo.CheckoutCommands ADD CustomerName NVARCHAR(200) NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CheckoutCommands') AND name = N'UX_CheckoutCommands_IdempotencyKey')
                CREATE UNIQUE INDEX UX_CheckoutCommands_IdempotencyKey ON dbo.CheckoutCommands(IdempotencyKey);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.CheckoutCommands') AND name = N'IX_CheckoutCommands_CreatedAtUtc')
                CREATE INDEX IX_CheckoutCommands_CreatedAtUtc ON dbo.CheckoutCommands(CreatedAtUtc);
            """, cancellationToken);
    }


    private static async Task EnsureOrderContextSnapshotTableAsync(BillingDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.OrderContextSnapshots', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.OrderContextSnapshots
                (
                    OrderId INT NOT NULL PRIMARY KEY,
                    OrderCode NVARCHAR(50) NULL,
                    TableId INT NULL,
                    TableName NVARCHAR(200) NULL,
                    BranchId INT NULL,
                    BranchName NVARCHAR(200) NULL,
                    RefreshedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_OrderContextSnapshots_RefreshedAtUtc DEFAULT SYSUTCDATETIME()
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderContextSnapshots') AND name = N'IX_OrderContextSnapshots_BranchId')
                CREATE INDEX IX_OrderContextSnapshots_BranchId ON dbo.OrderContextSnapshots(BranchId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderContextSnapshots') AND name = N'IX_OrderContextSnapshots_RefreshedAtUtc')
                CREATE INDEX IX_OrderContextSnapshots_RefreshedAtUtc ON dbo.OrderContextSnapshots(RefreshedAtUtc);
            """, cancellationToken);
    }
}
