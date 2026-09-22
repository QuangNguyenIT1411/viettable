-- Check if ChefId is being saved
USE RESTAURANT_ORDERS;
GO

-- Count items with ChefId
SELECT 
    COUNT(*) AS TotalItems,
    COUNT(ChefId) AS ItemsWithChefId,
    COUNT(CASE WHEN ChefId IS NULL THEN 1 END) AS ItemsWithoutChefId
FROM OrderItems;

-- Show recent items with their ChefId
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

-- Show items for employee 5 specifically
SELECT 
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
