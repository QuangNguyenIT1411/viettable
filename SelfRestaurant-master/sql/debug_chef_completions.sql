-- Debug: Check ChefId data in OrderItems
USE RESTAURANT_ORDERS;
GO

-- 1. Check if ChefId column exists and has data
SELECT TOP 20
    oi.ItemID,
    oi.OrderID,
    o.OrderCode,
    oi.DishID,
    oi.ChefId,
    oi.StatusCode,
    oi.Quantity,
    o.OrderTime,
    o.TableID
FROM OrderItems oi
JOIN Orders o ON oi.OrderID = o.OrderID
ORDER BY oi.ItemID DESC;

-- 2. Count items with ChefId
SELECT 
    COUNT(*) AS TotalItems,
    COUNT(ChefId) AS ItemsWithChef,
    COUNT(CASE WHEN ChefId IS NULL THEN 1 END) AS ItemsWithoutChef
FROM OrderItems;

-- 3. Check specific employee (ID = 5)
SELECT TOP 20
    oi.ItemID,
    oi.OrderID,
    o.OrderCode,
    oi.DishID,
    oi.ChefId,
    oi.StatusCode,
    oi.Quantity,
    o.OrderTime,
    o.TableID
FROM OrderItems oi
JOIN Orders o ON oi.OrderID = o.OrderID
WHERE oi.ChefId = 5
ORDER BY oi.ItemID DESC;

-- 4. Check recent orders for employee 5's branch
-- First, find employee 5's branch from RESTAURANT_IDENTITY
-- Then check orders from tables in that branch
SELECT TOP 20
    oi.ItemID,
    oi.OrderID,
    o.OrderCode,
    oi.DishID,
    oi.ChefId,
    oi.StatusCode,
    oi.Quantity,
    o.OrderTime,
    o.TableID
FROM OrderItems oi
JOIN Orders o ON oi.OrderID = o.OrderID
WHERE o.OrderTime >= DATEADD(day, -7, GETDATE())
ORDER BY o.OrderTime DESC, oi.ItemID DESC;
