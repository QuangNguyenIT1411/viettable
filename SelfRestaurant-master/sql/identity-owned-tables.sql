SET NOCOUNT ON;

-- Identity owns customer/staff credentials and loyalty identity data. Copy the
-- legacy rows once; subsequent starts keep the service-local tables intact.
DECLARE @owned TABLE (Name sysname NOT NULL PRIMARY KEY);
INSERT INTO @owned (Name) VALUES
    (N'Customers'),
    (N'EmployeeRoles'),
    (N'Employees'),
    (N'PasswordResetTokens'),
    (N'LoyaltyCards');

DECLARE @name sysname;
DECLARE @sql nvarchar(max);
DECLARE owned_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @owned;
OPEN owned_cursor;
FETCH NEXT FROM owned_cursor INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = @name)
    BEGIN
        SET @sql = N'DROP SYNONYM dbo.' + QUOTENAME(@name) + N';';
        EXEC sp_executesql @sql;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name = @name)
    BEGIN
        SET @sql = N'SELECT * INTO dbo.' + QUOTENAME(@name) +
                   N' FROM RESTAURANT.dbo.' + QUOTENAME(@name) + N';';
        EXEC sp_executesql @sql;
    END;

    FETCH NEXT FROM owned_cursor INTO @name;
END;
CLOSE owned_cursor;
DEALLOCATE owned_cursor;

DECLARE @keys TABLE (TableName sysname NOT NULL, ColumnName sysname NOT NULL, ConstraintName sysname NOT NULL);
INSERT INTO @keys (TableName, ColumnName, ConstraintName) VALUES
    (N'Customers', N'CustomerID', N'PK_Customers'),
    (N'EmployeeRoles', N'RoleID', N'PK_EmployeeRoles'),
    (N'Employees', N'EmployeeID', N'PK_Employees'),
    (N'PasswordResetTokens', N'TokenID', N'PK_PasswordResetTokens'),
    (N'LoyaltyCards', N'CardID', N'PK_LoyaltyCards');

DECLARE @column sysname;
DECLARE @constraint sysname;
DECLARE key_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, ColumnName, ConstraintName FROM @keys;
OPEN key_cursor;
FETCH NEXT FROM key_cursor INTO @name, @column, @constraint;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.' + @name) AND [type] = N'PK')
    BEGIN
        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@name) + N' ADD CONSTRAINT ' +
                   QUOTENAME(@constraint) + N' PRIMARY KEY (' + QUOTENAME(@column) + N');';
        EXEC sp_executesql @sql;
    END;
    FETCH NEXT FROM key_cursor INTO @name, @column, @constraint;
END;
CLOSE key_cursor;
DEALLOCATE key_cursor;

IF OBJECT_ID(N'dbo.CatalogBranchSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CatalogBranchSnapshots
    (
        BranchId INT NOT NULL CONSTRAINT PK_CatalogBranchSnapshots PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Location NVARCHAR(500) NULL,
        IsActive BIT NOT NULL,
        RefreshedAtUtc DATETIME2 NOT NULL
    );
END;

MERGE dbo.CatalogBranchSnapshots AS target
USING
(
    SELECT BranchID, Name, Location, CONVERT(bit, ISNULL(IsActive, 1)) AS IsActive
    FROM RESTAURANT.dbo.Branches
) AS source
ON target.BranchId = source.BranchID
WHEN MATCHED THEN UPDATE SET
    Name = source.Name,
    Location = source.Location,
    IsActive = source.IsActive,
    RefreshedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (BranchId, Name, Location, IsActive, RefreshedAtUtc)
    VALUES (source.BranchID, source.Name, source.Location, source.IsActive, SYSUTCDATETIME());

IF EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'CustomerLoyalty')
    DROP SYNONYM dbo.CustomerLoyalty;

EXEC(N'
CREATE OR ALTER VIEW dbo.CustomerLoyalty AS
SELECT
    c.CustomerID,
    c.Name,
    c.PhoneNumber,
    c.Email,
    c.LoyaltyPoints,
    lc.CardID,
    lc.Points AS CardPoints,
    lc.IssueDate,
    lc.ExpiryDate
FROM dbo.Customers c
LEFT JOIN dbo.LoyaltyCards lc ON c.CustomerID = lc.CustomerID
WHERE c.IsActive = 1;
');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Customers') AND name = N'UQ_Customers_Username')
    CREATE UNIQUE INDEX UQ_Customers_Username ON dbo.Customers(Username);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.EmployeeRoles') AND name = N'UQ_EmployeeRoles_RoleCode')
    CREATE UNIQUE INDEX UQ_EmployeeRoles_RoleCode ON dbo.EmployeeRoles(RoleCode);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Employees') AND name = N'UQ_Employees_Username')
    CREATE UNIQUE INDEX UQ_Employees_Username ON dbo.Employees(Username);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PasswordResetTokens') AND name = N'idx_token')
    CREATE INDEX idx_token ON dbo.PasswordResetTokens(Token);

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
