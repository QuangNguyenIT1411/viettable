-- Check if ChefId is populated in OrderItems
SELECT TOP 20 
    ItemID, 
    OrderID, 
    DishID, 
    ChefId, 
    StatusCode,
    Quantity
FROM OrderItems 
WHERE ChefId IS NOT NULL 
ORDER BY ItemID DESC;

-- Check specific order
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
WHERE o.OrderCode = 'ORD-20260509152114528-796'
ORDER BY oi.ItemID;
