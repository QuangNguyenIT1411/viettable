-- Migration: Add ChefId column to OrderItems table
-- Purpose: Track which chef prepared each dish
-- Date: 2026-05-09

USE [restaurant]
GO

-- Step 1: Add ChefId column if it doesn't exist
IF COL_LENGTH('dbo.OrderItems', 'ChefId') IS NULL
BEGIN
    PRINT 'Adding ChefId column to OrderItems table...'
    ALTER TABLE dbo.OrderItems 
    ADD ChefId INT NULL;
    PRINT 'ChefId column added successfully.'
END
ELSE
BEGIN
    PRINT 'ChefId column already exists in OrderItems table.'
END
GO

-- Step 2: Create index for better query performance
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'IX_OrderItems_ChefId')
BEGIN
    PRINT 'Creating index IX_OrderItems_ChefId...'
    CREATE INDEX IX_OrderItems_ChefId ON dbo.OrderItems(ChefId) WHERE ChefId IS NOT NULL;
    PRINT 'Index created successfully.'
END
ELSE
BEGIN
    PRINT 'Index IX_OrderItems_ChefId already exists.'
END
GO

-- Step 3: Verify the changes
PRINT 'Verification:'
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'OrderItems' AND COLUMN_NAME = 'ChefId';
GO

PRINT 'Migration completed successfully!'
PRINT 'Note: Existing OrderItems will have ChefId = NULL'
PRINT 'New items will be assigned ChefId when chef starts preparing them'
GO
