using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SelfRestaurant.Orders.Api.Persistence.Entities;

namespace SelfRestaurant.Orders.Api.Persistence;

public static class OrdersDbBootstrapper
{
    private static readonly string[] OwnedWriteTables =
    [
        "Orders",
        "OrderItems",
        "OrderItemIngredients",
        "OrderStatus",
        "BusinessAuditLogs",
        "SubmitCommands",
        "DiningSessionTables",
        "InboxEvents",
        "OutboxEvents",
        "ReservationPreOrders",
        "ReservationPreOrderItems",
        "DiningSessions",
        "ServiceRequests"
    ];

    private static readonly string[] OwnedReadModelTables =
    [
        "CatalogBranchSnapshots",
        "CatalogDishSnapshots",
        "CatalogTableSnapshots"
    ];

    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        if (db.Database.IsNpgsql())
        {
            await SelfRestaurant.CloudCompatibility.PostgresBootstrap.ValidateOrMigrateAsync(db,
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(), cancellationToken);
            await OrdersPostgresSeed.RunAsync(db, SelfRestaurant.CloudCompatibility.PostgresBootstrap.SeedProfile(
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()), cancellationToken);
            return;
        }

        await WaitForDatabaseAsync(db, logger, cancellationToken);
        await EnsureOutboxTableAsync(db, cancellationToken);
        await EnsureInboxTableAsync(db, cancellationToken);
        await EnsureCatalogSnapshotTablesAsync(db, cancellationToken);
        await EnsureOrdersDiningSessionColumnsAsync(db, cancellationToken);
        await EnsureDiningSessionTablesAsync(db, cancellationToken);
        await EnsureOrderItemStatusColumnAsync(db, cancellationToken);
        await EnsureOrderItemChefIdColumnAsync(db, cancellationToken);
        await EnsureOrderItemIngredientsTableAsync(db, logger, cancellationToken);
        await EnsureBusinessAuditTableAsync(db, cancellationToken);
        await EnsureSubmitCommandTableAsync(db, cancellationToken);
        await EnsureReservationPreOrderTablesAsync(db, cancellationToken);
        await EnsureDiningSessionsTableAsync(db, cancellationToken);
        await EnsureB5TablesAsync(db, cancellationToken);
        await EnsureB6ColumnsAsync(db, cancellationToken);
        await ValidateOwnedSchemaAsync(db, logger, cancellationToken);
        await SeedReferenceDataAsync(db, logger, cancellationToken);
    }

    private static async Task EnsureB6ColumnsAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.DiningSessions', 'CheckoutQuoteId') IS NULL
                ALTER TABLE dbo.DiningSessions ADD CheckoutQuoteId INT NULL;
            IF COL_LENGTH('dbo.DiningSessions', 'PaymentId') IS NULL
                ALTER TABLE dbo.DiningSessions ADD PaymentId INT NULL;
            """, cancellationToken);
    }

    private static async Task EnsureB5TablesAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
            IF COL_LENGTH('dbo.Orders', 'SubmitRequestHash') IS NULL
                ALTER TABLE dbo.Orders ADD SubmitRequestHash VARCHAR(64) NULL;

            IF OBJECT_ID(N'dbo.ServiceRequests', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ServiceRequests
                (
                    ServiceRequestId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServiceRequests PRIMARY KEY,
                    DiningSessionId INT NOT NULL,
                    CustomerId INT NOT NULL,
                    BranchId INT NOT NULL,
                    TableId INT NOT NULL,
                    RequestType VARCHAR(30) NOT NULL,
                    Note NVARCHAR(500) NULL,
                    Status VARCHAR(30) NOT NULL CONSTRAINT DF_ServiceRequests_Status DEFAULT('OPEN'),
                    IdempotencyKey VARCHAR(140) NOT NULL,
                    RequestHash VARCHAR(64) NOT NULL,
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ServiceRequests_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
                    UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ServiceRequests_UpdatedAtUtc DEFAULT(SYSUTCDATETIME()),
                    CancelledAtUtc DATETIME2 NULL
                );
                CREATE INDEX IX_ServiceRequests_DiningSessionId ON dbo.ServiceRequests(DiningSessionId);
                CREATE INDEX IX_ServiceRequests_CustomerId ON dbo.ServiceRequests(CustomerId);
                CREATE UNIQUE INDEX UX_ServiceRequests_IdempotencyKey ON dbo.ServiceRequests(IdempotencyKey);
            END;
            """;
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureDiningSessionsTableAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.DiningSessions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DiningSessions
                (
                    DiningSessionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiningSessions PRIMARY KEY,
                    SessionCode VARCHAR(64) NOT NULL,
                    CustomerId INT NOT NULL,
                    BranchId INT NOT NULL,
                    TableId INT NOT NULL,
                    ReservationId INT NULL,
                    Status VARCHAR(30) NOT NULL CONSTRAINT DF_DiningSessions_Status DEFAULT('ACTIVE'),
                    CheckInIdempotencyKey VARCHAR(140) NOT NULL,
                    RequestHash VARCHAR(64) NOT NULL,
                    ConvertedOrderId INT NULL,
                    StartedAtUtc DATETIME2 NOT NULL,
                    PaymentRequestedAtUtc DATETIME2 NULL,
                    EndedAtUtc DATETIME2 NULL,
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_DiningSessions_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
                    UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_DiningSessions_UpdatedAtUtc DEFAULT(SYSUTCDATETIME())
                );
                CREATE UNIQUE INDEX UX_DiningSessions_SessionCode ON dbo.DiningSessions(SessionCode);
                CREATE UNIQUE INDEX UX_DiningSessions_CheckInIdempotencyKey ON dbo.DiningSessions(CheckInIdempotencyKey);
                CREATE UNIQUE INDEX UX_DiningSessions_ReservationId ON dbo.DiningSessions(ReservationId) WHERE ReservationId IS NOT NULL;
                CREATE INDEX IX_DiningSessions_CustomerId ON dbo.DiningSessions(CustomerId);
                CREATE UNIQUE INDEX UX_DiningSessions_ActiveTable ON dbo.DiningSessions(TableId) WHERE EndedAtUtc IS NULL;
            END;

            IF COL_LENGTH('dbo.ReservationPreOrders', 'ConvertedOrderId') IS NULL
                ALTER TABLE dbo.ReservationPreOrders ADD ConvertedOrderId INT NULL;
            IF COL_LENGTH('dbo.ReservationPreOrders', 'ConvertedAtUtc') IS NULL
                ALTER TABLE dbo.ReservationPreOrders ADD ConvertedAtUtc DATETIME2 NULL;
            """;
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task EnsureReservationPreOrderTablesAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.ReservationPreOrders', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ReservationPreOrders
                (
                    PreOrderId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReservationPreOrders PRIMARY KEY,
                    ReservationId INT NOT NULL,
                    CustomerId INT NOT NULL,
                    BranchId INT NOT NULL,
                    Status VARCHAR(30) NOT NULL CONSTRAINT DF_ReservationPreOrders_Status DEFAULT('DRAFT'),
                    SubmitIdempotencyKey VARCHAR(140) NULL,
                    SubmitRequestHash VARCHAR(64) NULL,
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ReservationPreOrders_CreatedAtUtc DEFAULT(SYSUTCDATETIME()),
                    UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ReservationPreOrders_UpdatedAtUtc DEFAULT(SYSUTCDATETIME()),
                    SubmittedAtUtc DATETIME2 NULL
                );
                CREATE UNIQUE INDEX UX_ReservationPreOrders_ReservationId ON dbo.ReservationPreOrders(ReservationId);
                CREATE INDEX IX_ReservationPreOrders_CustomerId ON dbo.ReservationPreOrders(CustomerId);
                CREATE UNIQUE INDEX UX_ReservationPreOrders_SubmitIdempotencyKey
                    ON dbo.ReservationPreOrders(SubmitIdempotencyKey) WHERE SubmitIdempotencyKey IS NOT NULL;
            END;

            IF OBJECT_ID(N'dbo.ReservationPreOrderItems', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ReservationPreOrderItems
                (
                    PreOrderItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReservationPreOrderItems PRIMARY KEY,
                    PreOrderId INT NOT NULL,
                    DishId INT NOT NULL,
                    DishNameSnapshot NVARCHAR(200) NOT NULL,
                    UnitPriceSnapshot DECIMAL(18,2) NOT NULL,
                    Quantity INT NOT NULL,
                    Note NVARCHAR(1000) NULL,
                    CONSTRAINT FK_ReservationPreOrderItems_PreOrders FOREIGN KEY(PreOrderId)
                        REFERENCES dbo.ReservationPreOrders(PreOrderId) ON DELETE CASCADE
                );
                CREATE INDEX IX_ReservationPreOrderItems_PreOrderId ON dbo.ReservationPreOrderItems(PreOrderId);
            END;
            """;
        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private enum OrderItemIngredientsStorageState
    {
        Missing,
        PhysicalTable,
        SynonymOrOtherObject
    }

    private static async Task EnsureOrderItemIngredientsTableAsync(OrdersDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var state = await EnsureOrderItemIngredientsTableAsync(db, logger, cancellationToken, logScalarResult: true);

        if (state != OrderItemIngredientsStorageState.PhysicalTable)
        {
            logger.LogWarning(
                "OrderItemIngredients is not a physical table in the Orders database; skipping local column/index/constraint DDL. Existing object will remain intact.");
            return;
        }

        await EnsureOrderItemIngredientsColumnsAsync(db, logger, cancellationToken);
        await EnsureOrderItemIngredientsIndexesAsync(db, logger, cancellationToken);
        await EnsureOrderItemIngredientsConstraintsAsync(db, logger, cancellationToken);
    }

    private static async Task<OrderItemIngredientsStorageState> EnsureOrderItemIngredientsTableAsync(
        OrdersDbContext db,
        ILogger logger,
        CancellationToken cancellationToken,
        bool logScalarResult)
    {
        var physicalTableExists = await OrderItemIngredientsPhysicalTableExistsAsync(db, logger, cancellationToken, logScalarResult);
        if (physicalTableExists)
        {
            logger.LogInformation("OrderItemIngredients table already exists, skipping create.");
            return OrderItemIngredientsStorageState.PhysicalTable;
        }

        if (await OrderItemIngredientsSynonymExistsAsync(db, cancellationToken))
        {
            logger.LogWarning("Dropping dbo.OrderItemIngredients synonym so Orders can own per-order ingredient overrides locally.");
            await db.Database.ExecuteSqlRawAsync("DROP SYNONYM dbo.OrderItemIngredients;", cancellationToken);
            await CreateOrderItemIngredientsTableAsync(db, cancellationToken);
            return OrderItemIngredientsStorageState.PhysicalTable;
        }

        if (await OrderItemIngredientsAnyObjectExistsAsync(db, cancellationToken))
        {
            logger.LogInformation("OrderItemIngredients object already exists but is not a physical table or synonym, skipping create.");
            return OrderItemIngredientsStorageState.SynonymOrOtherObject;
        }

        logger.LogInformation("OrderItemIngredients table missing, creating.");
        await CreateOrderItemIngredientsTableAsync(db, cancellationToken);

        return OrderItemIngredientsStorageState.PhysicalTable;
    }

    private static async Task CreateOrderItemIngredientsTableAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE dbo.OrderItemIngredients
            (
                OrderItemIngredientID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItemIngredients PRIMARY KEY,
                OrderItemID INT NOT NULL,
                IngredientID INT NOT NULL,
                IngredientName NVARCHAR(200) NULL,
                Unit NVARCHAR(50) NULL,
                Quantity DECIMAL(18,3) NOT NULL,
                Note NVARCHAR(500) NULL,
                IsRemoved BIT NOT NULL CONSTRAINT DF_OrderItemIngredients_IsRemoved DEFAULT(0),
                CreatedAt DATETIME NULL CONSTRAINT DF_OrderItemIngredients_CreatedAt DEFAULT(GETDATE()),
                UpdatedAt DATETIME NULL
            );
            """, cancellationToken);
    }

    private static async Task EnsureOrderItemIngredientsColumnsAsync(OrdersDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        await AddColumnIfMissingAsync(db, logger, "OrderItemIngredientID", "ALTER TABLE dbo.OrderItemIngredients ADD OrderItemIngredientID INT IDENTITY(1,1) NOT NULL;", cancellationToken);
        await AddColumnIfMissingAsync(db, logger, "OrderItemID", "ALTER TABLE dbo.OrderItemIngredients ADD OrderItemID INT NOT NULL CONSTRAINT DF_OrderItemIngredients_OrderItemID DEFAULT(0);", cancellationToken);
        await AddColumnIfMissingAsync(db, logger, "IngredientID", "ALTER TABLE dbo.OrderItemIngredients ADD IngredientID INT NOT NULL CONSTRAINT DF_OrderItemIngredients_IngredientID DEFAULT(0);", cancellationToken);
        await AddColumnIfMissingAsync(db, logger, "IngredientName", "ALTER TABLE dbo.OrderItemIngredients ADD IngredientName NVARCHAR(200) NULL;", cancellationToken);
        await AddColumnIfMissingAsync(db, logger, "Unit", "ALTER TABLE dbo.OrderItemIngredients ADD Unit NVARCHAR(50) NULL;", cancellationToken);
        await AddColumnIfMissingAsync(db, logger, "Quantity", "ALTER TABLE dbo.OrderItemIngredients ADD Quantity DECIMAL(18,3) NOT NULL CONSTRAINT DF_OrderItemIngredients_Quantity DEFAULT(0);", cancellationToken);
        await AddColumnIfMissingAsync(db, logger, "Note", "ALTER TABLE dbo.OrderItemIngredients ADD Note NVARCHAR(500) NULL;", cancellationToken);
        await AddColumnIfMissingAsync(db, logger, "IsRemoved", "ALTER TABLE dbo.OrderItemIngredients ADD IsRemoved BIT NOT NULL CONSTRAINT DF_OrderItemIngredients_IsRemoved DEFAULT(0);", cancellationToken);
        await AddColumnIfMissingAsync(db, logger, "CreatedAt", "ALTER TABLE dbo.OrderItemIngredients ADD CreatedAt DATETIME NULL CONSTRAINT DF_OrderItemIngredients_CreatedAt DEFAULT(GETDATE());", cancellationToken);
        await AddColumnIfMissingAsync(db, logger, "UpdatedAt", "ALTER TABLE dbo.OrderItemIngredients ADD UpdatedAt DATETIME NULL;", cancellationToken);
    }

    private static async Task EnsureOrderItemIngredientsIndexesAsync(OrdersDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (!await PrimaryKeyExistsAsync(db, "PK_OrderItemIngredients", cancellationToken))
        {
            logger.LogInformation("Adding missing constraint PK_OrderItemIngredients.");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.OrderItemIngredients ADD CONSTRAINT PK_OrderItemIngredients PRIMARY KEY (OrderItemIngredientID);", cancellationToken);
        }

        if (!await IndexExistsAsync(db, "IX_OrderItemIngredients_OrderItemID", cancellationToken))
        {
            logger.LogInformation("Adding missing index IX_OrderItemIngredients_OrderItemID.");
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IX_OrderItemIngredients_OrderItemID ON dbo.OrderItemIngredients(OrderItemID);", cancellationToken);
        }

        if (!await IndexExistsAsync(db, "UQ_OrderItemIngredients_OrderItem_Ingredient", cancellationToken))
        {
            logger.LogInformation("Adding missing index UQ_OrderItemIngredients_OrderItem_Ingredient.");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX UQ_OrderItemIngredients_OrderItem_Ingredient ON dbo.OrderItemIngredients(OrderItemID, IngredientID);", cancellationToken);
        }
    }

    private static async Task EnsureOrderItemIngredientsConstraintsAsync(OrdersDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (await ForeignKeyExistsAsync(db, "FK_OrderItemIngredients_Ingredients", cancellationToken))
        {
            logger.LogWarning("Dropping incorrect FK_OrderItemIngredients_Ingredients because Orders stores Catalog ingredient references as external IDs.");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.OrderItemIngredients DROP CONSTRAINT FK_OrderItemIngredients_Ingredients;", cancellationToken);
        }

        await db.Database.ExecuteSqlRawAsync("""
            IF EXISTS
            (
                SELECT 1
                FROM sys.foreign_keys fk
                WHERE fk.name = N'FK_OrderItemIngredients_OrderItems'
                  AND fk.parent_object_id = OBJECT_ID(N'dbo.OrderItemIngredients')
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM sys.foreign_key_columns fkc
                      JOIN sys.columns parent_col
                        ON parent_col.object_id = fkc.parent_object_id
                       AND parent_col.column_id = fkc.parent_column_id
                      JOIN sys.columns ref_col
                        ON ref_col.object_id = fkc.referenced_object_id
                       AND ref_col.column_id = fkc.referenced_column_id
                      WHERE fkc.constraint_object_id = fk.object_id
                        AND parent_col.name = N'OrderItemID'
                        AND fkc.referenced_object_id = OBJECT_ID(N'dbo.OrderItems')
                        AND ref_col.name = N'ItemID'
                  )
            )
            BEGIN
                ALTER TABLE dbo.OrderItemIngredients DROP CONSTRAINT FK_OrderItemIngredients_OrderItems;
            END
            """, cancellationToken);

        if (await PhysicalTableExistsAsync(db, "OrderItems", cancellationToken)
            && !await ForeignKeyExistsAsync(db, "FK_OrderItemIngredients_OrderItems", cancellationToken))
        {
            logger.LogInformation("Adding missing constraint FK_OrderItemIngredients_OrderItems.");
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE dbo.OrderItemIngredients WITH NOCHECK
                ADD CONSTRAINT FK_OrderItemIngredients_OrderItems FOREIGN KEY (OrderItemID) REFERENCES dbo.OrderItems(ItemID);
                """, cancellationToken);
        }
    }
    private static async Task WaitForDatabaseAsync(OrdersDbContext db, ILogger logger, CancellationToken cancellationToken)
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

    private static async Task EnsureBusinessAuditTableAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        const string createSql = """
                                 IF OBJECT_ID(N'dbo.BusinessAuditLogs', N'U') IS NULL
                                 BEGIN
                                     CREATE TABLE dbo.BusinessAuditLogs
                                     (
                                         BusinessAuditLogId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                                         CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_OrdersBusinessAuditLogs_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
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

        const string indexesSql = """
                                  IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_CreatedAtUtc')
                                      CREATE INDEX IX_BusinessAuditLogs_CreatedAtUtc ON dbo.BusinessAuditLogs(CreatedAtUtc);

                                  IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_Entity')
                                      CREATE INDEX IX_BusinessAuditLogs_Entity ON dbo.BusinessAuditLogs(EntityType, EntityId);

                                  IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_OrderId')
                                      CREATE INDEX IX_BusinessAuditLogs_OrderId ON dbo.BusinessAuditLogs(OrderId);

                                  IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_TableId')
                                      CREATE INDEX IX_BusinessAuditLogs_TableId ON dbo.BusinessAuditLogs(TableId);
                                  """;

        await db.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
        await db.Database.ExecuteSqlRawAsync(indexesSql, cancellationToken);
    }


    private static async Task ValidateOwnedSchemaAsync(OrdersDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var missingWriteTables = new List<string>();
        foreach (var table in OwnedWriteTables)
        {
            var requirePhysicalTable = !string.Equals(table, "OrderItemIngredients", StringComparison.OrdinalIgnoreCase);
            if (!await ObjectExistsAsync(db, table, requirePhysicalTable, cancellationToken))
            {
                missingWriteTables.Add(table);
            }
        }

        if (missingWriteTables.Count > 0)
        {
            throw new InvalidOperationException(
                "Orders storage is missing owned write tables: " + string.Join(", ", missingWriteTables) +
                ". Complete the Orders DB cutover or materialize owned tables before starting Orders.Api.");
        }

        var missingReadModels = new List<string>();
        foreach (var table in OwnedReadModelTables)
        {
            if (!await ObjectExistsAsync(db, table, requirePhysicalTable: true, cancellationToken))
            {
                missingReadModels.Add(table);
            }
        }

        if (missingReadModels.Count > 0)
        {
            throw new InvalidOperationException(
                "Orders storage is missing local catalog snapshot tables: " + string.Join(", ", missingReadModels) +
                ". Ensure catalog read-model tables are created before starting Orders.Api.");
        }

        logger.LogInformation(
            "Orders write-model validated. Write tables: {WriteTables}. Read-model tables: {ReadModelTables}.",
            string.Join(", ", OwnedWriteTables),
            string.Join(", ", OwnedReadModelTables));
    }

    private static async Task<bool> ObjectExistsAsync(
        OrdersDbContext db,
        string objectName,
        bool requirePhysicalTable,
        CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = requirePhysicalTable
                ? """
                  SELECT 1
                  FROM sys.tables
                  WHERE schema_id = SCHEMA_ID('dbo')
                    AND name = @name
                  """
                : """
                  SELECT 1
                  FROM
                  (
                      SELECT name
                      FROM sys.tables
                      WHERE schema_id = SCHEMA_ID('dbo')

                      UNION ALL

                      SELECT name
                      FROM sys.views
                      WHERE schema_id = SCHEMA_ID('dbo')

                      UNION ALL

                      SELECT name
                      FROM sys.synonyms
                      WHERE schema_id = SCHEMA_ID('dbo')
                  ) AS objects
                  WHERE name = @name
                  """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = objectName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task SeedReferenceDataAsync(OrdersDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(db, "OrderStatus", cancellationToken))
        {
            logger.LogWarning("Missing expected tables; skipping seed.");
            return;
        }

        var changed = false;

        try
        {
            if (!await db.OrderStatus.AnyAsync(cancellationToken))
            {
                db.OrderStatus.AddRange(
                    new OrderStatus { StatusCode = "PENDING", StatusName = "Chờ xác nhận" },
                    new OrderStatus { StatusCode = "CONFIRMED", StatusName = "Đã xác nhận" },
                    new OrderStatus { StatusCode = "PREPARING", StatusName = "Đang chuẩn bị" },
                    new OrderStatus { StatusCode = "READY", StatusName = "Sẵn sàng" },
                    new OrderStatus { StatusCode = "SERVING", StatusName = "Đang phục vụ" },
                    new OrderStatus { StatusCode = "COMPLETED", StatusName = "Hoàn tất" },
                    new OrderStatus { StatusCode = "CANCELLED", StatusName = "Đã huỷ" });
                changed = true;
            }
        }
        catch (Exception ex) when (IsInvalidObjectReference(ex, "OrderStatus"))
        {
            logger.LogWarning(ex, "OrderStatus backing object is unavailable; skipping reference seed.");
            return;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsInvalidObjectReference(Exception ex, string objectName)
    {
        var message = ex.GetBaseException().Message ?? string.Empty;
        return message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
               && message.Contains(objectName, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> OrderItemIngredientsPhysicalTableExistsAsync(
        OrdersDbContext db,
        ILogger logger,
        CancellationToken cancellationToken,
        bool logScalarResult)
    {
        var result = await ExecuteScalarAsync(db, """
            SELECT CASE WHEN OBJECT_ID(N'[dbo].[OrderItemIngredients]', N'U') IS NULL THEN 0 ELSE 1 END
            """, cancellationToken);
        var exists = ConvertScalarToBoolean(result);
        if (logScalarResult)
        {
            logger.LogInformation("OrderItemIngredients physical table existence scalar result: {Result}; parsed exists: {Exists}.", result, exists);
        }

        return exists;
    }

    private static async Task<bool> OrderItemIngredientsAnyObjectExistsAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(db, """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM sys.objects
                WHERE schema_id = SCHEMA_ID('dbo')
                  AND name = N'OrderItemIngredients'

                UNION ALL

                SELECT 1
                FROM sys.synonyms
                WHERE schema_id = SCHEMA_ID('dbo')
                  AND name = N'OrderItemIngredients'
            ) THEN 1 ELSE 0 END
            """, cancellationToken);
        return ConvertScalarToBoolean(result);
    }

    private static async Task<bool> OrderItemIngredientsSynonymExistsAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(db, """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM sys.synonyms
                WHERE schema_id = SCHEMA_ID('dbo')
                  AND name = N'OrderItemIngredients'
            ) THEN 1 ELSE 0 END
            """, cancellationToken);
        return ConvertScalarToBoolean(result);
    }

    private static async Task<bool> PhysicalTableExistsAsync(OrdersDbContext db, string tableName, CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(db, """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM sys.tables
                WHERE schema_id = SCHEMA_ID('dbo')
                  AND name = @name
            ) THEN 1 ELSE 0 END
            """, cancellationToken, ("@name", tableName));
        return ConvertScalarToBoolean(result);
    }

    private static async Task<bool> ColumnExistsAsync(OrdersDbContext db, string columnName, CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(db, """
            SELECT CASE WHEN COL_LENGTH(N'dbo.OrderItemIngredients', @name) IS NULL THEN 0 ELSE 1 END
            """, cancellationToken, ("@name", columnName));
        return ConvertScalarToBoolean(result);
    }

    private static async Task<bool> IndexExistsAsync(OrdersDbContext db, string indexName, CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(db, """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[OrderItemIngredients]', N'U')
                  AND name = @name
            ) THEN 1 ELSE 0 END
            """, cancellationToken, ("@name", indexName));
        return ConvertScalarToBoolean(result);
    }

    private static async Task<bool> PrimaryKeyExistsAsync(OrdersDbContext db, string keyName, CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(db, """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM sys.key_constraints
                WHERE parent_object_id = OBJECT_ID(N'[dbo].[OrderItemIngredients]', N'U')
                  AND name = @name
            ) THEN 1 ELSE 0 END
            """, cancellationToken, ("@name", keyName));
        return ConvertScalarToBoolean(result);
    }

    private static async Task<bool> ForeignKeyExistsAsync(OrdersDbContext db, string keyName, CancellationToken cancellationToken)
    {
        var result = await ExecuteScalarAsync(db, """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM sys.foreign_keys
                WHERE parent_object_id = OBJECT_ID(N'[dbo].[OrderItemIngredients]', N'U')
                  AND name = @name
            ) THEN 1 ELSE 0 END
            """, cancellationToken, ("@name", keyName));
        return ConvertScalarToBoolean(result);
    }

    private static async Task AddColumnIfMissingAsync(
        OrdersDbContext db,
        ILogger logger,
        string columnName,
        string addColumnSql,
        CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(db, columnName, cancellationToken))
        {
            return;
        }

        logger.LogInformation("Adding missing column OrderItemIngredients.{ColumnName}.", columnName);
        await db.Database.ExecuteSqlRawAsync(addColumnSql, cancellationToken);
    }

    private static async Task<object?> ExecuteScalarAsync(
        OrdersDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            return await command.ExecuteScalarAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static bool ConvertScalarToBoolean(object? value)
    {
        return value switch
        {
            null or DBNull => false,
            bool boolean => boolean,
            byte number => number != 0,
            short number => number != 0,
            int number => number != 0,
            long number => number != 0,
            decimal number => number != 0,
            string text when int.TryParse(text, out var number) => number != 0,
            _ => Convert.ToInt32(value) != 0
        };
    }

    private static async Task<bool> TableExistsAsync(OrdersDbContext db, string tableName, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                                 SELECT 1
                                 FROM
                                 (
                                     SELECT name
                                     FROM sys.tables
                                     WHERE schema_id = SCHEMA_ID('dbo')

                                     UNION ALL

                                     SELECT name
                                     FROM sys.views
                                     WHERE schema_id = SCHEMA_ID('dbo')

                                     UNION ALL

                                     SELECT name
                                     FROM sys.synonyms
                                     WHERE schema_id = SCHEMA_ID('dbo')
                                 ) AS objects
                                 WHERE name = @table
                                 """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@table";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureOutboxTableAsync(OrdersDbContext db, CancellationToken cancellationToken)
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

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OutboxEvents') AND name = N'IX_OutboxEvents_Status_EventName_CreatedAtUtc')
                CREATE INDEX IX_OutboxEvents_Status_EventName_CreatedAtUtc ON dbo.OutboxEvents(Status, EventName, CreatedAtUtc);
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE dbo.OutboxEvents
            SET Status = 'PROCESSED',
                ProcessedAtUtc = ISNULL(ProcessedAtUtc, SYSUTCDATETIME()),
                Error = COALESCE(NULLIF(Error, ''), 'AUTO:untracked-event')
            WHERE Status = 'PENDING'
              AND EventName <> 'order.status-ready.v1';
            """, cancellationToken);
    }

    private static async Task EnsureOrdersDiningSessionColumnsAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        // Keep add-column and later references in separate batches; older SQL Server schemas can
        // parse the later index statement before ALTER TABLE takes effect.
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.Orders', 'DiningSessionCode') IS NULL
                EXEC(N'ALTER TABLE dbo.Orders ADD DiningSessionCode VARCHAR(64) NULL;');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'idx_orders_session')
                CREATE INDEX idx_orders_session ON dbo.Orders(DiningSessionCode);
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.Orders', 'SubmitIdempotencyKey') IS NULL
                EXEC(N'ALTER TABLE dbo.Orders ADD SubmitIdempotencyKey VARCHAR(100) NULL;');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'UX_Orders_SubmitIdempotencyKey')
                CREATE UNIQUE INDEX UX_Orders_SubmitIdempotencyKey ON dbo.Orders(SubmitIdempotencyKey) WHERE SubmitIdempotencyKey IS NOT NULL;
            """, cancellationToken);
    }

    private static async Task EnsureDiningSessionTablesAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.DiningSessionTables', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.DiningSessionTables
                (
                    DiningSessionTableID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    DiningSessionCode VARCHAR(64) NOT NULL,
                    TableID INT NOT NULL,
                    IsPrimary BIT NOT NULL,
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_DiningSessionTables_CreatedAtUtc DEFAULT (SYSUTCDATETIME())
                );
            END

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.DiningSessionTables') AND name = N'UX_DiningSessionTables_Session_Table')
                CREATE UNIQUE INDEX UX_DiningSessionTables_Session_Table ON dbo.DiningSessionTables(DiningSessionCode, TableID);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.DiningSessionTables') AND name = N'IX_DiningSessionTables_TableID')
                CREATE INDEX IX_DiningSessionTables_TableID ON dbo.DiningSessionTables(TableID);

            ;WITH sessionTables AS
            (
                SELECT
                    o.DiningSessionCode,
                    o.TableID,
                    IsPrimary = CASE
                        WHEN ROW_NUMBER() OVER (PARTITION BY o.DiningSessionCode ORDER BY MIN(o.OrderTime), o.TableID) = 1 THEN CAST(1 AS bit)
                        ELSE CAST(0 AS bit)
                    END
                FROM dbo.Orders o
                WHERE o.DiningSessionCode IS NOT NULL
                  AND o.TableID IS NOT NULL
                GROUP BY o.DiningSessionCode, o.TableID
            )
            INSERT INTO dbo.DiningSessionTables (DiningSessionCode, TableID, IsPrimary, CreatedAtUtc)
            SELECT st.DiningSessionCode, st.TableID, st.IsPrimary, SYSUTCDATETIME()
            FROM sessionTables st
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.DiningSessionTables dst
                WHERE dst.DiningSessionCode = st.DiningSessionCode
                  AND dst.TableID = st.TableID
            );
            """, cancellationToken);
    }

    private static async Task EnsureOrderItemStatusColumnAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.OrderItems', 'StatusCode') IS NULL
                EXEC(N'ALTER TABLE dbo.OrderItems ADD StatusCode VARCHAR(30) NOT NULL CONSTRAINT DF_OrderItems_StatusCode DEFAULT (''PENDING'');');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE oi
            SET StatusCode =
                CASE UPPER(ISNULL(os.StatusCode, 'PENDING'))
                    WHEN 'CANCELLED' THEN 'CANCELLED'
                    WHEN 'COMPLETED' THEN 'SERVING'
                    WHEN 'SERVING' THEN 'SERVING'
                    WHEN 'READY' THEN 'READY'
                    WHEN 'PREPARING' THEN 'PREPARING'
                    WHEN 'CONFIRMED' THEN 'CONFIRMED'
                    ELSE 'PENDING'
                END
            FROM dbo.OrderItems oi
            INNER JOIN dbo.Orders o ON o.OrderID = oi.OrderID
            LEFT JOIN dbo.OrderStatus os ON os.StatusID = o.StatusID
            WHERE oi.StatusCode IS NULL
               OR LTRIM(RTRIM(oi.StatusCode)) = '';
            """, cancellationToken);
    }

    private static async Task EnsureOrderItemChefIdColumnAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.OrderItems', 'ChefId') IS NULL
                EXEC(N'ALTER TABLE dbo.OrderItems ADD ChefId INT NULL;');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'IX_OrderItems_ChefId')
                CREATE INDEX IX_OrderItems_ChefId ON dbo.OrderItems(ChefId) WHERE ChefId IS NOT NULL;
            """, cancellationToken);
    }

    private static async Task EnsureSubmitCommandTableAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.SubmitCommands', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SubmitCommands
                (
                    SubmitCommandId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    IdempotencyKey VARCHAR(100) NOT NULL,
                    TableId INT NOT NULL,
                    DiningSessionCode VARCHAR(64) NULL,
                    OrderId INT NULL,
                    Status VARCHAR(20) NOT NULL CONSTRAINT DF_SubmitCommands_Status DEFAULT ('PENDING'),
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmitCommands_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                    CompletedAtUtc DATETIME2 NULL,
                    Error NVARCHAR(MAX) NULL
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SubmitCommands') AND name = N'UX_SubmitCommands_IdempotencyKey')
                CREATE UNIQUE INDEX UX_SubmitCommands_IdempotencyKey ON dbo.SubmitCommands(IdempotencyKey);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SubmitCommands') AND name = N'IX_SubmitCommands_CreatedAtUtc')
                CREATE INDEX IX_SubmitCommands_CreatedAtUtc ON dbo.SubmitCommands(CreatedAtUtc);
            """, cancellationToken);
    }

    private static async Task EnsureInboxTableAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
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
                    Status VARCHAR(30) NOT NULL CONSTRAINT DF_InboxEvents_Status DEFAULT ('PROCESSED'),
                    RetryCount INT NOT NULL CONSTRAINT DF_InboxEvents_RetryCount DEFAULT (0),
                    ReceivedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_InboxEvents_ReceivedAtUtc DEFAULT (SYSUTCDATETIME()),
                    NextRetryAtUtc DATETIME2 NULL,
                    ProcessedAtUtc DATETIME2 NULL,
                    Error NVARCHAR(MAX) NULL
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InboxEvents') AND name = N'UX_InboxEvents_Source_SourceEventId')
                CREATE UNIQUE INDEX UX_InboxEvents_Source_SourceEventId ON dbo.InboxEvents(Source, SourceEventId);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InboxEvents') AND name = N'IX_InboxEvents_ReceivedAtUtc')
                CREATE INDEX IX_InboxEvents_ReceivedAtUtc ON dbo.InboxEvents(ReceivedAtUtc);
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.InboxEvents', 'RetryCount') IS NULL
            BEGIN
                ALTER TABLE dbo.InboxEvents ADD RetryCount INT NOT NULL CONSTRAINT DF_InboxEvents_RetryCount_Migrate DEFAULT (0);
            END

            IF COL_LENGTH('dbo.InboxEvents', 'NextRetryAtUtc') IS NULL
            BEGIN
                ALTER TABLE dbo.InboxEvents ADD NextRetryAtUtc DATETIME2 NULL;
            END
            """, cancellationToken);
    }

    private static async Task EnsureCatalogSnapshotTablesAsync(OrdersDbContext db, CancellationToken cancellationToken)
    {
        const string sql = """
                           IF OBJECT_ID(N'dbo.CatalogDishSnapshots', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.CatalogDishSnapshots
                               (
                                   DishId INT NOT NULL PRIMARY KEY,
                                   Name NVARCHAR(200) NOT NULL,
                                   CategoryId INT NOT NULL,
                                   CategoryName NVARCHAR(200) NULL,
                                   Price DECIMAL(18,2) NOT NULL,
                                   Unit NVARCHAR(50) NULL,
                                   Image NVARCHAR(500) NULL,
                                   IsActive BIT NOT NULL,
                                   Available BIT NOT NULL,
                                   RefreshedAtUtc DATETIME2 NOT NULL
                               );
                           END

                           IF OBJECT_ID(N'dbo.CatalogTableSnapshots', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.CatalogTableSnapshots
                               (
                                   TableId INT NOT NULL PRIMARY KEY,
                                   BranchId INT NOT NULL,
                                   TableNumber INT NOT NULL CONSTRAINT DF_CatalogTableSnapshots_TableNumber DEFAULT 0,
                                   QrCode NVARCHAR(100) NULL,
                                   IsActive BIT NOT NULL,
                                   StatusId INT NOT NULL,
                                   StatusCode NVARCHAR(50) NULL,
                                   StatusName NVARCHAR(100) NULL,
                                   RefreshedAtUtc DATETIME2 NOT NULL
                               );
                           END

                           IF COL_LENGTH(N'dbo.CatalogTableSnapshots', N'TableNumber') IS NULL
                           BEGIN
                               ALTER TABLE dbo.CatalogTableSnapshots
                               ADD TableNumber INT NOT NULL CONSTRAINT DF_CatalogTableSnapshots_TableNumber DEFAULT 0;
                           END

                           IF OBJECT_ID(N'dbo.CatalogBranchSnapshots', N'U') IS NULL
                           BEGIN
                               CREATE TABLE dbo.CatalogBranchSnapshots
                               (
                                   BranchId INT NOT NULL PRIMARY KEY,
                                   Name NVARCHAR(200) NOT NULL,
                                   Location NVARCHAR(500) NULL,
                                   IsActive BIT NOT NULL,
                                   RefreshedAtUtc DATETIME2 NOT NULL
                               );
                           END
                           """;

        await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
