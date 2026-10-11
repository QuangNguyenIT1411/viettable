using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SelfRestaurant.Customers.Api.Persistence.Entities;

namespace SelfRestaurant.Customers.Api.Persistence;

public static class CustomersDbBootstrapper
{
    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var contextTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Customers", "context-resolution");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        contextTiming?.Dispose();

        using var providerTiming = SelfRestaurant.CloudCompatibility.CloudHosting.Measure("Customers", "context-provider-init");
        var isPostgres = db.Database.IsNpgsql();
        providerTiming?.Dispose();
        if (isPostgres)
        {
            await SelfRestaurant.CloudCompatibility.PostgresBootstrap.ValidateOrMigrateAsync(db,
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(), cancellationToken);
            return;
        }

        await WaitForDatabaseAsync(db, logger, cancellationToken);
        await EnsureInboxTableAsync(db, cancellationToken);
        await EnsureReadyNotificationsTableAsync(db, cancellationToken);
        await EnsureReservationTablesAsync(db, cancellationToken);
        await EnsureB7TablesAsync(db, cancellationToken);
    }

    private static async Task EnsureB7TablesAsync(CustomersDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.CustomerFavorites', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CustomerFavorites(CustomerFavoriteId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerFavorites PRIMARY KEY,
                    CustomerId INT NOT NULL, DishId INT NOT NULL, DishNameSnapshot NVARCHAR(200) NOT NULL, CreatedAtUtc DATETIME2 NOT NULL);
                CREATE UNIQUE INDEX UX_CustomerFavorites_Customer_Dish ON dbo.CustomerFavorites(CustomerId,DishId);
            END;
            IF OBJECT_ID(N'dbo.CustomerReviews', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CustomerReviews(CustomerReviewId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerReviews PRIMARY KEY,
                    CustomerId INT NOT NULL, InvoiceId INT NOT NULL, DiningSessionId INT NOT NULL, OverallRating INT NOT NULL,
                    FoodRating INT NOT NULL, ServiceRating INT NOT NULL, SpaceRating INT NOT NULL, Comment NVARCHAR(2000) NULL,
                    ImageUrlsJson NVARCHAR(MAX) NOT NULL, CreateIdempotencyKey VARCHAR(140) NOT NULL, RequestHash VARCHAR(64) NOT NULL,
                    CreatedAtUtc DATETIME2 NOT NULL, UpdatedAtUtc DATETIME2 NOT NULL);
                CREATE UNIQUE INDEX UX_CustomerReviews_InvoiceId ON dbo.CustomerReviews(InvoiceId);
                CREATE UNIQUE INDEX UX_CustomerReviews_CreateIdempotencyKey ON dbo.CustomerReviews(CreateIdempotencyKey);
                CREATE INDEX IX_CustomerReviews_CustomerId ON dbo.CustomerReviews(CustomerId);
            END;
            IF OBJECT_ID(N'dbo.SupportRequests', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SupportRequests(SupportRequestId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SupportRequests PRIMARY KEY,
                    RequestCode VARCHAR(50) NOT NULL, CustomerId INT NOT NULL, Category VARCHAR(30) NOT NULL,
                    Subject NVARCHAR(200) NOT NULL, Message NVARCHAR(3000) NOT NULL, OrderId INT NULL, ReservationId INT NULL,
                    PaymentId INT NULL, Status VARCHAR(30) NOT NULL, IdempotencyKey VARCHAR(140) NOT NULL, RequestHash VARCHAR(64) NOT NULL,
                    CreatedAtUtc DATETIME2 NOT NULL, UpdatedAtUtc DATETIME2 NOT NULL);
                CREATE UNIQUE INDEX UX_SupportRequests_RequestCode ON dbo.SupportRequests(RequestCode);
                CREATE UNIQUE INDEX UX_SupportRequests_IdempotencyKey ON dbo.SupportRequests(IdempotencyKey);
                CREATE INDEX IX_SupportRequests_CustomerId ON dbo.SupportRequests(CustomerId);
            END;
            """, cancellationToken);
    }

    private static async Task WaitForDatabaseAsync(CustomersDbContext db, ILogger logger, CancellationToken cancellationToken)
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

    private static async Task EnsureInboxTableAsync(CustomersDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
                           IF OBJECT_ID(N'dbo.InboxEvents', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.InboxEvents
                               (
                                   InboxEventId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                   Source VARCHAR(100) NOT NULL,
                                   SourceEventId BIGINT NOT NULL,
                                   EventName VARCHAR(200) NOT NULL,
                                   CorrelationId VARCHAR(100) NULL,
                                   PayloadJson NVARCHAR(MAX) NOT NULL,
                                   Status VARCHAR(30) NOT NULL CONSTRAINT DF_CustomersInboxEvents_Status DEFAULT ('PROCESSED'),
                                   RetryCount INT NOT NULL CONSTRAINT DF_CustomersInboxEvents_RetryCount DEFAULT (0),
                                   ReceivedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CustomersInboxEvents_ReceivedAtUtc DEFAULT (SYSUTCDATETIME()),
                                   NextRetryAtUtc DATETIME2 NULL,
                                   ProcessedAtUtc DATETIME2 NULL,
                                   Error NVARCHAR(MAX) NULL
                               );
                               CREATE UNIQUE INDEX UX_InboxEvents_Source_SourceEventId ON dbo.InboxEvents(Source, SourceEventId);
                               CREATE INDEX IX_InboxEvents_ReceivedAtUtc ON dbo.InboxEvents(ReceivedAtUtc);
                           END
                           """;
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureReadyNotificationsTableAsync(CustomersDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
                           IF OBJECT_ID(N'dbo.ReadyDishNotifications', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.ReadyDishNotifications
                               (
                                   ReadyDishNotificationId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                   OrderId INT NOT NULL,
                                   OrderItemId INT NULL,
                                   DishId INT NULL,
                                   DishName NVARCHAR(200) NULL,
                                   CustomerId INT NULL,
                                   TableId INT NULL,
                                   EventName VARCHAR(100) NOT NULL CONSTRAINT DF_ReadyDishNotifications_EventName DEFAULT ('order.status-ready.v1'),
                                   Message NVARCHAR(500) NOT NULL,
                                   Status VARCHAR(30) NOT NULL CONSTRAINT DF_ReadyDishNotifications_Status DEFAULT ('OPEN'),
                                   CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ReadyDishNotifications_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                                   ResolvedAtUtc DATETIME2 NULL
                               );
                               CREATE INDEX IX_ReadyDishNotifications_Order_Item_Event ON dbo.ReadyDishNotifications(OrderId, OrderItemId, EventName);
                           END

                           IF COL_LENGTH('dbo.ReadyDishNotifications', 'OrderItemId') IS NULL
                           BEGIN
                               ALTER TABLE dbo.ReadyDishNotifications ADD OrderItemId INT NULL;
                           END

                           IF COL_LENGTH('dbo.ReadyDishNotifications', 'DishId') IS NULL
                           BEGIN
                               ALTER TABLE dbo.ReadyDishNotifications ADD DishId INT NULL;
                           END

                           IF COL_LENGTH('dbo.ReadyDishNotifications', 'DishName') IS NULL
                           BEGIN
                               ALTER TABLE dbo.ReadyDishNotifications ADD DishName NVARCHAR(200) NULL;
                           END

                           IF EXISTS (
                               SELECT 1
                               FROM sys.indexes
                               WHERE object_id = OBJECT_ID(N'dbo.ReadyDishNotifications')
                                 AND name = N'IX_ReadyDishNotifications_Order_Event'
                           )
                           BEGIN
                               DROP INDEX IX_ReadyDishNotifications_Order_Event ON dbo.ReadyDishNotifications;
                           END

                           IF NOT EXISTS (
                               SELECT 1
                               FROM sys.indexes
                               WHERE object_id = OBJECT_ID(N'dbo.ReadyDishNotifications')
                                 AND name = N'IX_ReadyDishNotifications_Order_Item_Event'
                           )
                           BEGIN
                               CREATE INDEX IX_ReadyDishNotifications_Order_Item_Event
                                   ON dbo.ReadyDishNotifications(OrderId, OrderItemId, EventName);
                           END
                           """;
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureReservationTablesAsync(CustomersDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
                           IF OBJECT_ID(N'dbo.Reservations', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.Reservations
                               (
                                   ReservationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                   ReservationCode VARCHAR(40) NOT NULL,
                                   CustomerId INT NULL,
                                   CustomerName NVARCHAR(200) NOT NULL,
                                   PhoneNumber NVARCHAR(30) NOT NULL,
                                   BranchId INT NOT NULL,
                                   TableId INT NULL,
                                   PartySize INT NOT NULL,
                                   ReservedAt DATETIME2 NOT NULL,
                                   ArrivalWindowMinutes INT NOT NULL CONSTRAINT DF_Reservations_ArrivalWindowMinutes DEFAULT (30),
                                   Status VARCHAR(30) NOT NULL CONSTRAINT DF_Reservations_Status DEFAULT ('Pending'),
                                   Note NVARCHAR(1000) NULL,
                                   ConvertedOrderId INT NULL,
                                   DiningSessionCode VARCHAR(64) NULL,
                                   CheckInStartedAtUtc DATETIME2 NULL,
                                   CheckInIdempotencyKey VARCHAR(100) NULL,
                                   CheckedInAtUtc DATETIME2 NULL,
                                   CheckedInByEmployeeId INT NULL,
                                   CancelledAtUtc DATETIME2 NULL,
                                   CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_Reservations_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                                   UpdatedAtUtc DATETIME2 NULL,
                                   IdempotencyKey VARCHAR(100) NULL
                               );
                           END

                           IF OBJECT_ID(N'dbo.ReservationPreOrderItems', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.ReservationPreOrderItems
                               (
                                   ReservationItemId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                   ReservationId INT NOT NULL,
                                   DishId INT NOT NULL,
                                   DishNameSnapshot NVARCHAR(200) NOT NULL,
                                   UnitPriceSnapshot DECIMAL(18,2) NOT NULL,
                                   Quantity INT NOT NULL,
                                   Note NVARCHAR(1000) NULL,
                                   Status VARCHAR(30) NOT NULL CONSTRAINT DF_ReservationPreOrderItems_Status DEFAULT ('Pending'),
                                   CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ReservationPreOrderItems_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                                   UpdatedAtUtc DATETIME2 NULL,
                                   ConvertedAtUtc DATETIME2 NULL,
                                   CONSTRAINT FK_ReservationPreOrderItems_Reservations_ReservationId
                                       FOREIGN KEY (ReservationId) REFERENCES dbo.Reservations(ReservationId) ON DELETE CASCADE
                               );
                           END

                           IF OBJECT_ID(N'dbo.ReservationTables', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.ReservationTables
                               (
                                   ReservationTableId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                   ReservationId INT NOT NULL,
                                   TableId INT NOT NULL,
                                   IsPrimary BIT NOT NULL,
                                   CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ReservationTables_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                                   CONSTRAINT FK_ReservationTables_Reservations_ReservationId
                                       FOREIGN KEY (ReservationId) REFERENCES dbo.Reservations(ReservationId) ON DELETE CASCADE
                               );
                           END

                           IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Reservations') AND name = N'UX_Reservations_ReservationCode')
                           BEGIN
                               CREATE UNIQUE INDEX UX_Reservations_ReservationCode ON dbo.Reservations(ReservationCode);
                           END

                           IF COL_LENGTH('dbo.Reservations', 'CheckInStartedAtUtc') IS NULL
                           BEGIN
                               ALTER TABLE dbo.Reservations ADD CheckInStartedAtUtc DATETIME2 NULL;
                           END

                           IF COL_LENGTH('dbo.Reservations', 'CheckInIdempotencyKey') IS NULL
                           BEGIN
                               ALTER TABLE dbo.Reservations ADD CheckInIdempotencyKey VARCHAR(100) NULL;
                           END

                           IF COL_LENGTH('dbo.Reservations', 'RequestHash') IS NULL
                               ALTER TABLE dbo.Reservations ADD RequestHash VARCHAR(64) NULL;

                           IF COL_LENGTH('dbo.Reservations', 'LastMutationKey') IS NULL
                               ALTER TABLE dbo.Reservations ADD LastMutationKey VARCHAR(140) NULL;

                           IF COL_LENGTH('dbo.Reservations', 'LastMutationHash') IS NULL
                               ALTER TABLE dbo.Reservations ADD LastMutationHash VARCHAR(64) NULL;

                           IF COL_LENGTH('dbo.Reservations', 'PreOrderSubmittedAtUtc') IS NULL
                               ALTER TABLE dbo.Reservations ADD PreOrderSubmittedAtUtc DATETIME2 NULL;

                           IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Reservations') AND name = N'IX_Reservations_Branch_ReservedAt_Status')
                           BEGIN
                               CREATE INDEX IX_Reservations_Branch_ReservedAt_Status ON dbo.Reservations(BranchId, ReservedAt, Status);
                           END

                           IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Reservations') AND name = N'IX_Reservations_PhoneNumber')
                           BEGIN
                               CREATE INDEX IX_Reservations_PhoneNumber ON dbo.Reservations(PhoneNumber);
                           END

                           IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Reservations') AND name = N'UX_Reservations_IdempotencyKey')
                           BEGIN
                               CREATE UNIQUE INDEX UX_Reservations_IdempotencyKey ON dbo.Reservations(IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
                           END

                           IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ReservationPreOrderItems') AND name = N'IX_ReservationPreOrderItems_Reservation_Status')
                           BEGIN
                               CREATE INDEX IX_ReservationPreOrderItems_Reservation_Status ON dbo.ReservationPreOrderItems(ReservationId, Status);
                           END

                           IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ReservationTables') AND name = N'UX_ReservationTables_Reservation_Table')
                           BEGIN
                               CREATE UNIQUE INDEX UX_ReservationTables_Reservation_Table ON dbo.ReservationTables(ReservationId, TableId);
                           END

                           IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ReservationTables') AND name = N'IX_ReservationTables_TableId')
                           BEGIN
                               CREATE INDEX IX_ReservationTables_TableId ON dbo.ReservationTables(TableId);
                           END

                           INSERT INTO dbo.ReservationTables (ReservationId, TableId, IsPrimary, CreatedAtUtc)
                           SELECT r.ReservationId, r.TableId, CAST(1 AS bit), SYSUTCDATETIME()
                           FROM dbo.Reservations r
                           WHERE r.TableId IS NOT NULL
                             AND r.TableId > 0
                             AND NOT EXISTS
                             (
                                 SELECT 1
                                 FROM dbo.ReservationTables rt
                                 WHERE rt.ReservationId = r.ReservationId
                                   AND rt.TableId = r.TableId
                             );
                           """;
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
