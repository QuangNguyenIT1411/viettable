-- Find duplicate dishes by name
USE RESTAURANT_CATALOG;
GO

-- 1. Find dishes with same name
SELECT 
    Name,
    COUNT(*) AS Count,
    STRING_AGG(CAST(DishID AS VARCHAR), ', ') AS DishIDs,
    STRING_AGG(CAST(Price AS VARCHAR), ', ') AS Prices,
    STRING_AGG(CAST(ISNULL(Available, 1) AS VARCHAR), ', ') AS AvailableFlags
FROM Dishes
GROUP BY Name
HAVING COUNT(*) > 1
ORDER BY Name;

-- 2. Show all dishes with their status
SELECT 
    DishID,
    Name,
    Price,
    CategoryID,
    Available,
    CASE WHEN Available IS NULL THEN 'NULL (treated as TRUE)'
         WHEN Available = 1 THEN 'TRUE'
         ELSE 'FALSE'
    END AS AvailableStatus
FROM Dishes
ORDER BY Name, DishID;

-- 3. Find dishes that should be hidden (Available = 0 or NULL but should be 0)
SELECT 
    DishID,
    Name,
    Price,
    Available
FROM Dishes
WHERE Available = 0 OR Available IS NULL
ORDER BY Name;
