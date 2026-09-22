-- CLEANUP DUPLICATE DISHES - RUN THIS CAREFULLY
USE RESTAURANT_CATALOG;
GO

-- Step 1: Find all dishes and their details
PRINT '=== ALL DISHES IN DATABASE ===';
SELECT 
    DishID,
    Name,
    Price,
    CategoryID,
    ISNULL(Available, 1) AS Available,
    Image
FROM Dishes
ORDER BY Name, DishID;

-- Step 2: Find duplicates by name
PRINT '';
PRINT '=== DUPLICATE DISHES (SAME NAME) ===';
SELECT 
    Name,
    COUNT(*) AS DuplicateCount,
    STRING_AGG(CAST(DishID AS VARCHAR), ', ') AS AllDishIDs,
    STRING_AGG(CAST(Price AS VARCHAR), ', ') AS AllPrices
FROM Dishes
GROUP BY Name
HAVING COUNT(*) > 1;

-- Step 3: For each duplicate, keep the one with highest DishID (newest) and mark others as unavailable
PRINT '';
PRINT '=== MARKING OLD DUPLICATES AS UNAVAILABLE ===';

-- Find dishes to disable (older duplicates)
WITH DishRanking AS (
    SELECT 
        DishID,
        Name,
        Price,
        ROW_NUMBER() OVER (PARTITION BY Name ORDER BY DishID DESC) AS RowNum
    FROM Dishes
)
UPDATE Dishes
SET Available = 0
WHERE DishID IN (
    SELECT DishID 
    FROM DishRanking 
    WHERE RowNum > 1
);

-- Show what was updated
SELECT 
    DishID,
    Name,
    Price,
    Available
FROM Dishes
WHERE Available = 0
ORDER BY Name;

PRINT '';
PRINT '=== FINAL ACTIVE DISHES (WHAT CUSTOMER WILL SEE) ===';
SELECT 
    DishID,
    Name,
    Price,
    CategoryID,
    Available
FROM Dishes
WHERE ISNULL(Available, 1) = 1
ORDER BY Name;

PRINT '';
PRINT 'CLEANUP COMPLETE!';
PRINT 'Old duplicate dishes have been marked as Available = 0';
PRINT 'Only the newest version of each dish will be shown to customers';
