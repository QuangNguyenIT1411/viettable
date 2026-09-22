param(
    [string]$ConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT_CATALOG;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
    [string]$IdentityConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT_IDENTITY;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
    [string]$OrdersConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT_ORDERS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
    [string]$BillingConnectionString = "Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT_BILLING;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Invoke-NonQuery {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection,
        [System.Data.SqlClient.SqlTransaction]$Transaction,
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $cmd = $Connection.CreateCommand()
    if ($null -ne $Transaction) {
        $cmd.Transaction = $Transaction
    }
    $cmd.CommandText = $Sql
    $cmd.CommandTimeout = 120
    return $cmd.ExecuteNonQuery()
}

function Invoke-Scalar {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    $cmd.CommandTimeout = 120
    return $cmd.ExecuteScalar()
}

function New-UnicodeString {
    param([int[]]$CodePoints)
    return -join ($CodePoints | ForEach-Object { [char]$_ })
}

function Update-BaselineCategory {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlTransaction]$Transaction,
        [Parameter(Mandatory = $true)]
        [int]$CategoryId,
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Description,
        [Parameter(Mandatory = $true)]
        [int]$DisplayOrder
    )

    $cmd = $Connection.CreateCommand()
    $cmd.Transaction = $Transaction
    $cmd.CommandTimeout = 120
    $cmd.CommandText = @"
UPDATE Categories
SET Name = @name,
    Description = @description,
    DisplayOrder = @displayOrder,
    IsActive = 1,
    UpdatedAt = GETDATE()
WHERE CategoryID = @categoryId;
"@
    [void]$cmd.Parameters.Add("@name", [System.Data.SqlDbType]::NVarChar, 200)
    [void]$cmd.Parameters.Add("@description", [System.Data.SqlDbType]::NVarChar, 1000)
    [void]$cmd.Parameters.Add("@displayOrder", [System.Data.SqlDbType]::Int)
    [void]$cmd.Parameters.Add("@categoryId", [System.Data.SqlDbType]::Int)

    $cmd.Parameters["@name"].Value = $Name
    $cmd.Parameters["@description"].Value = $Description
    $cmd.Parameters["@displayOrder"].Value = $DisplayOrder
    $cmd.Parameters["@categoryId"].Value = $CategoryId

    [void]$cmd.ExecuteNonQuery()
}

function Ensure-StaffRegressionMenus {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)]
        [System.Data.SqlClient.SqlTransaction]$Transaction
    )

    $sql = @"
DECLARE @today date = CAST(GETDATE() AS date);
DECLARE @branchId int;
DECLARE @menuId int;
DECLARE @menuName nvarchar(200);

DECLARE branch_cur CURSOR LOCAL FAST_FORWARD FOR
SELECT BranchID
FROM Branches
WHERE BranchID IN (1, 2, 3)
  AND ISNULL(IsActive, 1) = 1;

OPEN branch_cur;
FETCH NEXT FROM branch_cur INTO @branchId;

WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT TOP 1 @menuId = MenuID
    FROM Menus
    WHERE BranchID = @branchId
      AND ISNULL(IsActive, 1) = 1
      AND [Date] = @today
    ORDER BY MenuID DESC;

    IF @menuId IS NULL
    BEGIN
        SET @menuName = N'Thực đơn chi nhánh ' + CAST(@branchId AS nvarchar(20)) + N' - ' + CONVERT(nvarchar(10), @today, 103);

        INSERT INTO Menus (MenuName, [Date], IsActive, CreatedAt, UpdatedAt, BranchID)
        VALUES (@menuName, @today, 1, GETDATE(), GETDATE(), @branchId);

        SET @menuId = CAST(SCOPE_IDENTITY() AS int);
    END

    MERGE MenuCategory AS target
    USING (
        SELECT @menuId AS MenuID, c.CategoryID
        FROM Categories c
        WHERE c.CategoryID IN (1, 2, 3, 4)
          AND ISNULL(c.IsActive, 1) = 1
    ) AS src
    ON target.MenuID = src.MenuID AND target.CategoryID = src.CategoryID
    WHEN MATCHED THEN
        UPDATE SET IsActive = 1, UpdatedAt = GETDATE()
    WHEN NOT MATCHED THEN
        INSERT (MenuID, CategoryID, IsActive, CreatedAt, UpdatedAt)
        VALUES (src.MenuID, src.CategoryID, 1, GETDATE(), GETDATE());

    ;WITH EligibleDishes AS
    (
        SELECT d.DishID, d.CategoryID
        FROM Dishes d
        WHERE d.CategoryID IN (1, 2, 3, 4)
          AND ISNULL(d.IsActive, 1) = 1
          AND ISNULL(d.Available, 1) = 1
    )
    MERGE CategoryDish AS target
    USING
    (
        SELECT mc.MenuCategoryID, ed.DishID
        FROM MenuCategory mc
        INNER JOIN EligibleDishes ed ON ed.CategoryID = mc.CategoryID
        WHERE mc.MenuID = @menuId
          AND ISNULL(mc.IsActive, 1) = 1
    ) AS src
    ON target.MenuCategoryID = src.MenuCategoryID AND target.DishID = src.DishID
    WHEN MATCHED THEN
        UPDATE SET IsAvailable = 1, UpdatedAt = GETDATE()
    WHEN NOT MATCHED THEN
        INSERT (MenuCategoryID, DishID, DisplayOrder, IsAvailable, CreatedAt, UpdatedAt)
        VALUES (
            src.MenuCategoryID,
            src.DishID,
            ISNULL((
                SELECT MAX(cd.DisplayOrder)
                FROM CategoryDish cd
                WHERE cd.MenuCategoryID = src.MenuCategoryID
            ), 0) + 1,
            1,
            GETDATE(),
            GETDATE()
        );

    FETCH NEXT FROM branch_cur INTO @branchId;
END

CLOSE branch_cur;
DEALLOCATE branch_cur;
"@

    Invoke-NonQuery -Connection $Connection -Transaction $Transaction -Sql $sql | Out-Null
}

$conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$conn.Open()

try {
    $beforeAutoCats = [int](Invoke-Scalar -Connection $conn -Sql @"
SELECT COUNT(*) FROM Categories WHERE Name LIKE 'AUTO[_]%' OR Name LIKE 'DBG[_]%' OR Name LIKE 'CODEX[_]%'
"@)
    $beforeAutoDishes = [int](Invoke-Scalar -Connection $conn -Sql @"
SELECT COUNT(*) FROM Dishes WHERE Name LIKE 'AUTO[_]%' OR Name LIKE 'DBG[_]%' OR Name LIKE 'CODEX[_]%'
"@)

    Write-Host "Before cleanup: auto/debug categories=$beforeAutoCats, auto/debug dishes=$beforeAutoDishes"

    if ($WhatIf) {
        Write-Host "WhatIf enabled. No data changed."
        return
    }

    $tx = $conn.BeginTransaction()
    try {
        # 1) Restore baseline category names/order for the main flows.
        Update-BaselineCategory -Connection $conn -Transaction $tx -CategoryId 1 -DisplayOrder 1 `
            -Name (New-UnicodeString @(77,243,110,32,99,104,237,110,104)) `
            -Description (New-UnicodeString @(67,225,99,32,109,243,110,32,259,110,32,99,104,237,110,104))

        Update-BaselineCategory -Connection $conn -Transaction $tx -CategoryId 2 -DisplayOrder 2 `
            -Name (New-UnicodeString @(77,243,110,32,112,104,7909)) `
            -Description (New-UnicodeString @(67,225,99,32,109,243,110,32,259,110,32,112,104,7909))

        Update-BaselineCategory -Connection $conn -Transaction $tx -CategoryId 3 -DisplayOrder 3 `
            -Name (New-UnicodeString @(84,114,225,110,103,32,109,105,7879,110,103)) `
            -Description (New-UnicodeString @(67,225,99,32,109,243,110,32,116,114,225,110,103,32,109,105,7879,110,103))

        Update-BaselineCategory -Connection $conn -Transaction $tx -CategoryId 4 -DisplayOrder 4 `
            -Name (New-UnicodeString @(272,7891,32,117,7889,110,103)) `
            -Description (New-UnicodeString @(67,225,99,32,108,111,7841,105,32,273,7891,32,117,7889,110,103))

        # 2) Disable test categories to prevent leaking into admin/customer views.
        Invoke-NonQuery -Connection $conn -Transaction $tx -Sql @"
UPDATE Categories
SET IsActive = 0,
    UpdatedAt = GETDATE()
WHERE CategoryID NOT IN (1, 2, 3, 4)
  AND (
      Name LIKE 'AUTO[_]%'
      OR Name LIKE 'DBG[_]%'
      OR Name LIKE 'CODEX[_]%'
  );
"@ | Out-Null

        # 3) Disable test dishes and dishes attached to disabled/non-baseline categories.
        Invoke-NonQuery -Connection $conn -Transaction $tx -Sql @"
UPDATE d
SET d.IsActive = 0,
    d.Available = 0,
    d.UpdatedAt = GETDATE()
FROM Dishes d
LEFT JOIN Categories c ON c.CategoryID = d.CategoryID
WHERE d.Name LIKE 'AUTO[_]%'
   OR d.Name LIKE 'DBG[_]%'
   OR d.Name LIKE 'CODEX[_]%'
   OR d.CategoryID NOT IN (1, 2, 3, 4)
   OR ISNULL(c.IsActive, 0) = 0;
"@ | Out-Null

        # 4) Keep baseline menu categories active.
        Invoke-NonQuery -Connection $conn -Transaction $tx -Sql @"
UPDATE MenuCategory
SET IsActive = 1,
    UpdatedAt = GETDATE()
WHERE CategoryID IN (1, 2, 3, 4);
"@ | Out-Null

        # 5) Disable category-dish mappings when either side is disabled.
        Invoke-NonQuery -Connection $conn -Transaction $tx -Sql @"
UPDATE cd
SET cd.IsAvailable = 0,
    cd.UpdatedAt = GETDATE()
FROM CategoryDish cd
JOIN Dishes d ON d.DishID = cd.DishID
JOIN MenuCategory mc ON mc.MenuCategoryID = cd.MenuCategoryID
JOIN Categories c ON c.CategoryID = mc.CategoryID
WHERE ISNULL(d.IsActive, 0) = 0
   OR ISNULL(d.Available, 0) = 0
   OR ISNULL(mc.IsActive, 0) = 0
   OR ISNULL(c.IsActive, 0) = 0;
"@ | Out-Null

        # 6) Recreate a stable today-menu baseline for staff regression branches.
        Ensure-StaffRegressionMenus -Connection $conn -Transaction $tx

        $tx.Commit()
    }
    catch {
        $tx.Rollback()
        throw
    }

    $afterAutoCats = [int](Invoke-Scalar -Connection $conn -Sql @"
SELECT COUNT(*) FROM Categories WHERE IsActive = 1 AND (Name LIKE 'AUTO[_]%' OR Name LIKE 'DBG[_]%' OR Name LIKE 'CODEX[_]%')
"@)
    $afterAutoDishes = [int](Invoke-Scalar -Connection $conn -Sql @"
SELECT COUNT(*) FROM Dishes WHERE ISNULL(IsActive, 1) = 1 AND (Name LIKE 'AUTO[_]%' OR Name LIKE 'DBG[_]%' OR Name LIKE 'CODEX[_]%')
"@)

    Write-Host "After cleanup: active auto/debug categories=$afterAutoCats, active auto/debug dishes=$afterAutoDishes"

    $identityConn = New-Object System.Data.SqlClient.SqlConnection($IdentityConnectionString)
    $identityConn.Open()
    try {
        if (-not $WhatIf) {
            $identityTx = $identityConn.BeginTransaction()
            try {
                Invoke-NonQuery -Connection $identityConn -Transaction $identityTx -Sql @"
DELETE prt
FROM PasswordResetTokens prt
INNER JOIN Customers c ON c.CustomerID = prt.CustomerID
WHERE c.Username LIKE 'cusd[_]%'
   OR c.Username LIKE 'autocus%'
   OR ISNULL(c.Email, '') LIKE '%@example.local';

DELETE lc
FROM LoyaltyCards lc
INNER JOIN Customers c ON c.CustomerID = lc.CustomerID
WHERE c.Username LIKE 'cusd[_]%'
   OR c.Username LIKE 'autocus%'
   OR ISNULL(c.Email, '') LIKE '%@example.local';

DELETE FROM Customers
WHERE Username LIKE 'cusd[_]%'
   OR Username LIKE 'autocus%'
   OR ISNULL(Email, '') LIKE '%@example.local';

DELETE FROM Employees
WHERE Username LIKE 'autoemp%'
   OR Name LIKE 'Auto Employee%';
"@ | Out-Null
                $identityTx.Commit()
            }
            catch {
                $identityTx.Rollback()
                throw
            }
        }
    }
    finally {
        $identityConn.Close()
    }

    $customersConn = New-Object System.Data.SqlClient.SqlConnection("Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT_CUSTOMERS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True")
    $customersConn.Open()
    try {
        if (-not $WhatIf) {
            $customersTx = $customersConn.BeginTransaction()
            try {
                Invoke-NonQuery -Connection $customersConn -Transaction $customersTx -Sql @"
IF OBJECT_ID(N'dbo.ReadyDishNotifications', N'U') IS NOT NULL DELETE FROM dbo.ReadyDishNotifications;
IF OBJECT_ID(N'dbo.InboxEvents', N'U') IS NOT NULL DELETE FROM dbo.InboxEvents;
"@ | Out-Null
                $customersTx.Commit()
            }
            catch {
                $customersTx.Rollback()
                throw
            }
        }
    }
    finally {
        $customersConn.Close()
    }

    $ordersConn = New-Object System.Data.SqlClient.SqlConnection($OrdersConnectionString)
    $ordersConn.Open()
    try {
        if (-not $WhatIf) {
            $ordersTx = $ordersConn.BeginTransaction()
            try {
                Invoke-NonQuery -Connection $ordersConn -Transaction $ordersTx -Sql @"
IF OBJECT_ID(N'dbo.Bills', N'U') IS NOT NULL OR EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'Bills') DELETE FROM dbo.Bills;
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NOT NULL OR EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'OrderItems') DELETE FROM dbo.OrderItems;
IF OBJECT_ID(N'dbo.Orders', N'U') IS NOT NULL OR EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'Orders') DELETE FROM dbo.Orders;
IF OBJECT_ID(N'dbo.InboxEvents', N'U') IS NOT NULL OR EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'InboxEvents') DELETE FROM dbo.InboxEvents;
IF OBJECT_ID(N'dbo.OutboxEvents', N'U') IS NOT NULL OR EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'OutboxEvents') DELETE FROM dbo.OutboxEvents;
"@ | Out-Null
                $ordersTx.Commit()
            }
            catch {
                $ordersTx.Rollback()
                throw
            }
        }
    }
    finally {
        $ordersConn.Close()
    }

    $billingConn = New-Object System.Data.SqlClient.SqlConnection($BillingConnectionString)
    $billingConn.Open()
    try {
        if (-not $WhatIf) {
            $billingTx = $billingConn.BeginTransaction()
            try {
                Invoke-NonQuery -Connection $billingConn -Transaction $billingTx -Sql @"
IF OBJECT_ID(N'dbo.Bills', N'U') IS NOT NULL OR EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'Bills') DELETE FROM dbo.Bills;
IF OBJECT_ID(N'dbo.OrderContextSnapshots', N'U') IS NOT NULL DELETE FROM dbo.OrderContextSnapshots;
IF OBJECT_ID(N'dbo.OutboxEvents', N'U') IS NOT NULL OR EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'OutboxEvents') DELETE FROM dbo.OutboxEvents;
"@ | Out-Null
                $billingTx.Commit()
            }
            catch {
                $billingTx.Rollback()
                throw
            }
        }
    }
    finally {
        $billingConn.Close()
    }

    $catalogTx = $conn.BeginTransaction()
    try {
        Invoke-NonQuery -Connection $conn -Transaction $catalogTx -Sql @"
UPDATE Ingredients
SET IsActive = 0
WHERE Name LIKE 'AUTO[_]%'
   OR Name LIKE 'DBG[_]%'
   OR Name LIKE 'CODEX[_]%';

UPDATE DiningTables
SET CurrentOrderID = NULL,
    StatusID = (
        SELECT TOP 1 StatusID
        FROM TableStatus
        WHERE StatusCode = 'AVAILABLE'
    ),
    UpdatedAt = GETDATE()
WHERE ISNULL(IsActive, 1) = 1;

UPDATE DiningTables
SET IsActive = 0,
    CurrentOrderID = NULL,
    StatusID = (
        SELECT TOP 1 StatusID
        FROM TableStatus
        WHERE StatusCode = 'AVAILABLE'
    ),
    UpdatedAt = GETDATE()
WHERE QRCode LIKE 'AUTO-%';
"@ | Out-Null
        $catalogTx.Commit()
    }
    catch {
        $catalogTx.Rollback()
        throw
    }

    Write-Host "Cleanup completed."
}
finally {
    $conn.Close()
}
