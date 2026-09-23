-- Customers-only metadata retirement after source and SQL consumer audit.
-- Repeat-safe; never drops target tables or another service's objects.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lock int;
EXEC @lock = sys.sp_getapplock @Resource = 'customers-synonym-retirement-v1', @LockMode = 'Exclusive', @LockOwner = 'Transaction';
IF @lock < 0 THROW 51000, 'Customers retirement lock unavailable', 1;

DECLARE @expected TABLE (Name sysname PRIMARY KEY);
INSERT @expected(Name) VALUES
('ActiveOrders'),('Bills'),('Branches'),('BranchRevenue'),('Categories'),('CategoryDish'),
('CustomerLoyalty'),('Customers'),('DiningTables'),('DishDetails'),('Dishes'),
('DishIngredients'),('EmployeeRoles'),('Employees'),('Ingredients'),('LoyaltyCards'),
('MenuCategory'),('Menus'),('OrderItemIngredients'),('OrderItems'),('Orders'),
('OrderStatus'),('PasswordResetTokens'),('PaymentMethod'),('Payments'),
('PaymentStatus'),('Reports'),('Restaurants'),('TableNumbers'),('TableStatus');

IF DB_NAME() <> 'RESTAURANT_CUSTOMERS' THROW 51000, 'Wrong database: Customers only', 1;
IF EXISTS (SELECT 1 FROM sys.synonyms s WHERE s.schema_id <> SCHEMA_ID('dbo') OR NOT EXISTS (SELECT 1 FROM @expected e WHERE e.Name=s.name))
    THROW 51000, 'Unexpected synonym; review before cleanup', 1;
IF EXISTS (SELECT 1 FROM sys.synonyms s WHERE s.base_object_name <> N'[RESTAURANT].[dbo].' + QUOTENAME(s.name))
    THROW 51000, 'Unexpected synonym target; review before cleanup', 1;
IF EXISTS (SELECT 1 FROM sys.sql_expression_dependencies d JOIN sys.synonyms s ON d.referenced_id=s.object_id)
    THROW 51000, 'SQL object depends on a Customers synonym', 1;

DECLARE @sql nvarchar(max)=N'';
SELECT @sql = @sql + N'DROP SYNONYM dbo.' + QUOTENAME(s.name) + N';' + CHAR(10)
FROM sys.synonyms s ORDER BY s.name;
EXEC sys.sp_executesql @sql;
COMMIT;
SELECT COUNT(*) AS RemainingCustomersSynonyms FROM sys.synonyms;
