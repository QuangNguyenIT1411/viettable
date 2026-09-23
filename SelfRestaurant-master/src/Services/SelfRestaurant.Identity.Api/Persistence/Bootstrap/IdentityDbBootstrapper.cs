using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace SelfRestaurant.Identity.Api.Persistence;

public static class IdentityDbBootstrapper
{
    private static readonly string[] OwnedTables =
    [
        "BusinessAuditLogs",
        "CatalogBranchSnapshots",
        "Customers",
        "EmployeeRoles",
        "Employees",
        "PasswordResetTokens",
        "LoyaltyCards",
        "MobileRefreshTokens",
        "CustomerSettings"
    ];

    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await WaitForDatabaseAsync(db, logger, cancellationToken);
        await EnsureBusinessAuditLogsTableAsync(db, logger, cancellationToken);
        await EnsureMobileRefreshTokensTableAsync(db, logger, cancellationToken);
        await EnsureCustomerSettingsTableAsync(db, logger, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID('dbo.LoyaltySettlements', 'U') IS NULL
            CREATE TABLE dbo.LoyaltySettlements (
                SettlementKey NVARCHAR(100) NOT NULL PRIMARY KEY,
                CustomerId INT NOT NULL, PointsUsed INT NOT NULL,
                AmountPaid DECIMAL(18,2) NOT NULL, ResponseJson NVARCHAR(MAX) NOT NULL
            );
            """, cancellationToken);
        await ValidateOwnedSchemaAsync(db, logger, cancellationToken);
        await EnsureCustomerExternalLoginColumnsAsync(db, logger, cancellationToken);
        await SeedReferenceDataAsync(db, logger, cancellationToken);
    }

    private static async Task WaitForDatabaseAsync(IdentityDbContext db, ILogger logger, CancellationToken cancellationToken)
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

    private static Task SeedReferenceDataAsync(IdentityDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("Identity reference seed skipped; service no longer owns OrderStatus.");
        return Task.CompletedTask;
    }

    private static async Task ValidateOwnedSchemaAsync(IdentityDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        foreach (var table in OwnedTables)
        {
            if (!await ObjectExistsAsync(db, table, requirePhysicalTable: true, cancellationToken))
            {
                missing.Add(table);
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Identity storage is missing owned tables: " + string.Join(", ", missing) +
                ". Run sql/setup-service-db-shells.ps1 or complete the Identity DB cutover before starting Identity.Api.");
        }

        if (!await ObjectExistsAsync(db, "CustomerLoyalty", requirePhysicalTable: false, cancellationToken))
        {
            throw new InvalidOperationException("Identity storage is missing CustomerLoyalty view.");
        }

        logger.LogInformation("Identity schema validated without transitional Branches dependency.");
    }

    private static async Task EnsureBusinessAuditLogsTableAsync(IdentityDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            const string createSql = """
                IF OBJECT_ID(N'dbo.BusinessAuditLogs', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.BusinessAuditLogs
                    (
                        BusinessAuditLogId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_IdentityBusinessAuditLogs_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                        ActionType VARCHAR(100) NOT NULL,
                        EntityType VARCHAR(50) NOT NULL,
                        EntityId VARCHAR(100) NOT NULL,
                        ActorType VARCHAR(30) NULL,
                        ActorId INT NULL,
                        ActorCode VARCHAR(50) NULL,
                        ActorName NVARCHAR(100) NULL,
                        ActorRoleCode VARCHAR(50) NULL,
                        CustomerId INT NULL,
                        EmployeeId INT NULL,
                        IpAddress VARCHAR(50) NULL,
                        UserAgent VARCHAR(500) NULL,
                        CorrelationId VARCHAR(100) NULL,
                        Notes NVARCHAR(1000) NULL,
                        BeforeState NVARCHAR(MAX) NULL,
                        AfterState NVARCHAR(MAX) NULL
                    );
                END
                """;

            await ExecuteNonQueryAsync(db, createSql, cancellationToken);

            const string indexesSql = """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_CreatedAtUtc')
                    CREATE INDEX IX_BusinessAuditLogs_CreatedAtUtc ON dbo.BusinessAuditLogs(CreatedAtUtc);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_Entity')
                    CREATE INDEX IX_BusinessAuditLogs_Entity ON dbo.BusinessAuditLogs(EntityType, EntityId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_CustomerId')
                    CREATE INDEX IX_BusinessAuditLogs_CustomerId ON dbo.BusinessAuditLogs(CustomerId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BusinessAuditLogs') AND name = N'IX_BusinessAuditLogs_EmployeeId')
                    CREATE INDEX IX_BusinessAuditLogs_EmployeeId ON dbo.BusinessAuditLogs(EmployeeId);
                """;

            await ExecuteNonQueryAsync(db, indexesSql, cancellationToken);

            logger.LogInformation("Identity BusinessAuditLogs table is ready.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureMobileRefreshTokensTableAsync(IdentityDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await ExecuteNonQueryAsync(db, """
                IF OBJECT_ID(N'dbo.MobileRefreshTokens', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.MobileRefreshTokens
                    (
                        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MobileRefreshTokens PRIMARY KEY,
                        CustomerId INT NOT NULL,
                        TokenHash VARCHAR(64) NOT NULL,
                        FamilyId VARCHAR(36) NOT NULL,
                        CreatedAtUtc DATETIME2 NOT NULL,
                        ExpiresAtUtc DATETIME2 NOT NULL,
                        RevokedAtUtc DATETIME2 NULL,
                        ReplacedByTokenHash VARCHAR(64) NULL,
                        RevokedReason VARCHAR(100) NULL,
                        CreatedByIp VARCHAR(64) NULL,
                        UserAgent NVARCHAR(500) NULL,
                        CONSTRAINT FK_MobileRefreshTokens_Customers FOREIGN KEY (CustomerId)
                            REFERENCES dbo.Customers(CustomerID) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX UQ_MobileRefreshTokens_TokenHash ON dbo.MobileRefreshTokens(TokenHash);
                    CREATE INDEX IX_MobileRefreshTokens_Customer_Expiry ON dbo.MobileRefreshTokens(CustomerId, ExpiresAtUtc);
                    CREATE INDEX IX_MobileRefreshTokens_Family ON dbo.MobileRefreshTokens(FamilyId);
                END;
                """, cancellationToken);
            logger.LogInformation("Identity mobile refresh-token table is ready.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureCustomerSettingsTableAsync(IdentityDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await ExecuteNonQueryAsync(db, """
                IF OBJECT_ID(N'dbo.CustomerSettings', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.CustomerSettings
                    (
                        CustomerId INT NOT NULL CONSTRAINT PK_CustomerSettings PRIMARY KEY,
                        PushNotificationsEnabled BIT NOT NULL CONSTRAINT DF_CustomerSettings_Push DEFAULT (1),
                        SmsReservationRemindersEnabled BIT NOT NULL CONSTRAINT DF_CustomerSettings_Sms DEFAULT (1),
                        BiometricLoginEnabled BIT NOT NULL CONSTRAINT DF_CustomerSettings_Biometric DEFAULT (0),
                        LanguageCode VARCHAR(10) NOT NULL CONSTRAINT DF_CustomerSettings_Language DEFAULT ('vi-VN'),
                        DefaultBranchId INT NULL,
                        UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CustomerSettings_Updated DEFAULT (SYSUTCDATETIME()),
                        CONSTRAINT FK_CustomerSettings_Customers FOREIGN KEY (CustomerId)
                            REFERENCES dbo.Customers(CustomerID) ON DELETE CASCADE
                    );
                END;
                """, cancellationToken);
            logger.LogInformation("Identity customer settings table is ready.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureCustomerExternalLoginColumnsAsync(IdentityDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await ExecuteNonQueryAsync(db, """
                IF COL_LENGTH('dbo.Customers', 'AuthProvider') IS NULL
                    ALTER TABLE dbo.Customers ADD AuthProvider varchar(30) NOT NULL CONSTRAINT DF_Customers_AuthProvider DEFAULT ('Password');
                """, cancellationToken);

            await ExecuteNonQueryAsync(db, """
                IF COL_LENGTH('dbo.Customers', 'ExternalProvider') IS NULL
                    ALTER TABLE dbo.Customers ADD ExternalProvider varchar(30) NULL;
                """, cancellationToken);

            await ExecuteNonQueryAsync(db, """
                IF COL_LENGTH('dbo.Customers', 'ExternalSubject') IS NULL
                    ALTER TABLE dbo.Customers ADD ExternalSubject varchar(120) NULL;
                """, cancellationToken);

            await ExecuteNonQueryAsync(db, """
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID('dbo.Customers')
                      AND name = 'Password'
                      AND is_nullable = 0
                )
                    ALTER TABLE dbo.Customers ALTER COLUMN Password varchar(255) NULL;
                """, cancellationToken);

            await ExecuteNonQueryAsync(db, """
                UPDATE dbo.Customers
                SET AuthProvider = 'Password'
                WHERE AuthProvider IS NULL OR LTRIM(RTRIM(AuthProvider)) = '';
                """, cancellationToken);

            await ExecuteNonQueryAsync(db, """
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID('dbo.Customers')
                      AND name = 'idx_customers_external_login'
                )
                    CREATE INDEX idx_customers_external_login ON dbo.Customers(ExternalProvider, ExternalSubject);
                """, cancellationToken);

            logger.LogInformation("Identity customer external login columns are ready.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task ExecuteNonQueryAsync(IdentityDbContext db, string sql, CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> ObjectExistsAsync(
        IdentityDbContext db,
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
}
