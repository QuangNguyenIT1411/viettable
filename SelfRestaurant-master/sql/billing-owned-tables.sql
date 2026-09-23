SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
IF SCHEMA_ID(N'billing') IS NULL EXEC(N'CREATE SCHEMA billing AUTHORIZATION dbo');
IF OBJECT_ID(N'billing.Bills', N'U') IS NULL
BEGIN
    CREATE TABLE billing.Bills (
        BillID INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BillingOwnedBills PRIMARY KEY,
        OrderID INT NOT NULL, BillCode NVARCHAR(50) NOT NULL,
        BillTime DATETIME NOT NULL DEFAULT GETDATE(),
        Subtotal DECIMAL(18,2) NOT NULL, Discount DECIMAL(18,2) NOT NULL,
        PointsDiscount DECIMAL(18,2) NOT NULL, PointsUsed INT NULL,
        TotalAmount DECIMAL(18,2) NOT NULL, PaymentMethod NVARCHAR(20) NOT NULL,
        PaymentAmount DECIMAL(18,2) NULL, ChangeAmount DECIMAL(18,2) NULL,
        EmployeeID INT NULL, CustomerID INT NULL, IsActive BIT NOT NULL DEFAULT 1,
        OrderCodeSnapshot NVARCHAR(50) NULL, TableIdSnapshot INT NULL,
        TableNameSnapshot NVARCHAR(200) NULL, BranchIdSnapshot INT NULL,
        BranchNameSnapshot NVARCHAR(200) NULL, DiningSessionCode VARCHAR(64) NULL,
        CheckoutIdempotencyKey VARCHAR(100) NULL
    );
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'billing.Bills') AND name='IX_Bills_BillTime')
    CREATE INDEX IX_Bills_BillTime ON billing.Bills(BillTime);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'billing.Bills') AND name='IX_Bills_OrderID')
    CREATE INDEX IX_Bills_OrderID ON billing.Bills(OrderID);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'billing.Bills') AND name='UX_Bills_CheckoutIdempotencyKey')
    CREATE UNIQUE INDEX UX_Bills_CheckoutIdempotencyKey ON billing.Bills(CheckoutIdempotencyKey) WHERE CheckoutIdempotencyKey IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'billing.Bills') AND name='UX_Bills_OrderID_Active')
    CREATE UNIQUE INDEX UX_Bills_OrderID_Active ON billing.Bills(OrderID) WHERE IsActive=1;
IF OBJECT_ID(N'billing.OwnershipMigrations', N'U') IS NULL
    CREATE TABLE billing.OwnershipMigrations (
        MigrationId VARCHAR(100) NOT NULL PRIMARY KEY,
        SourceRows BIGINT NOT NULL, SourceTotal DECIMAL(38,2) NULL,
        CompletedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
