SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Orders', N'SN') IS NOT NULL
    DROP SYNONYM dbo.Orders;
IF OBJECT_ID(N'dbo.OrderItems', N'SN') IS NOT NULL
    DROP SYNONYM dbo.OrderItems;
IF OBJECT_ID(N'dbo.OrderStatus', N'SN') IS NOT NULL
    DROP SYNONYM dbo.OrderStatus;

IF OBJECT_ID(N'dbo.OrderStatus', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderStatus
    (
        StatusID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        StatusCode VARCHAR(50) NOT NULL,
        StatusName NVARCHAR(100) NOT NULL
    );
    CREATE UNIQUE INDEX UX_OrderStatus_StatusCode ON dbo.OrderStatus(StatusCode);
END

IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Orders
    (
        OrderID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OrderCode VARCHAR(50) NULL,
        OrderTime DATETIME NOT NULL CONSTRAINT DF_Orders_OrderTime DEFAULT (GETDATE()),
        CompletedTime DATETIME NULL,
        Note NVARCHAR(1000) NULL,
        IsActive BIT NULL CONSTRAINT DF_Orders_IsActive DEFAULT (1),
        TableID INT NULL,
        CustomerID INT NULL,
        StatusID INT NOT NULL,
        CashierID INT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'UX_Orders_OrderCode')
BEGIN
    CREATE UNIQUE INDEX UX_Orders_OrderCode ON dbo.Orders(OrderCode) WHERE OrderCode IS NOT NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'idx_orders_time')
BEGIN
    CREATE INDEX idx_orders_time ON dbo.Orders(OrderTime);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'idx_orders_status')
BEGIN
    CREATE INDEX idx_orders_status ON dbo.Orders(StatusID);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Orders') AND name = N'idx_orders_table')
BEGIN
    CREATE INDEX idx_orders_table ON dbo.Orders(TableID);
END

IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderItems
    (
        ItemID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Quantity INT NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        LineTotal DECIMAL(18,2) NOT NULL,
        Note NVARCHAR(500) NULL,
        OrderID INT NOT NULL,
        DishID INT NOT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'idx_orderitems_order')
BEGIN
    CREATE INDEX idx_orderitems_order ON dbo.OrderItems(OrderID);
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'idx_orderitems_dish')
BEGIN
    CREATE INDEX idx_orderitems_dish ON dbo.OrderItems(DishID);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Orders_OrderStatus')
BEGIN
    ALTER TABLE dbo.Orders WITH NOCHECK
    ADD CONSTRAINT FK_Orders_OrderStatus FOREIGN KEY (StatusID) REFERENCES dbo.OrderStatus(StatusID);
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrderItems_Orders')
BEGIN
    ALTER TABLE dbo.OrderItems WITH NOCHECK
    ADD CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderID) REFERENCES dbo.Orders(OrderID);
END

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
END

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
END

IF COL_LENGTH('dbo.ReservationPreOrders', 'ConvertedOrderId') IS NULL
    ALTER TABLE dbo.ReservationPreOrders ADD ConvertedOrderId INT NULL;
IF COL_LENGTH('dbo.ReservationPreOrders', 'ConvertedAtUtc') IS NULL
    ALTER TABLE dbo.ReservationPreOrders ADD ConvertedAtUtc DATETIME2 NULL;

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
END

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
END
