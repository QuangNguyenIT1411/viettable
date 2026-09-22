param(
    [string]$Server = "(localdb)\\MSSQLLocalDB",
    [string]$MasterDatabase = "RESTAURANT"
)

$ErrorActionPreference = "Stop"

function Get-SqlCmdPath {
    $candidates = @(
        "sqlcmd",
        "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\SQLCMD.EXE",
        "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE"
    )

    foreach ($candidate in $candidates) {
        try {
            $command = Get-Command $candidate -ErrorAction Stop
            return $command.Source
        }
        catch {
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    throw "sqlcmd was not found. Install SQL Server command line tools or update setup-service-db-shells.ps1 with the installed path."
}

function Invoke-SqlCmdChecked {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $sqlcmd @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed with exit code $LASTEXITCODE. Arguments: $($Arguments -join ' ')"
    }
}

$serviceDatabases = @(
    "RESTAURANT_CATALOG",
    "RESTAURANT_ORDERS",
    "RESTAURANT_CUSTOMERS",
    "RESTAURANT_IDENTITY",
    "RESTAURANT_BILLING"
)

$catalogOwnershipScript = Join-Path $PSScriptRoot "catalog-owned-tables.sql"
$identityOwnershipScript = Join-Path $PSScriptRoot "identity-owned-tables.sql"
$ordersOwnershipScript = Join-Path $PSScriptRoot "orders-owned-tables.sql"
$catalogShellAllowList = @()
$identityShellAllowList = @()

$createDatabasesSql = @(
    "IF DB_ID('$MasterDatabase') IS NULL THROW 50000, 'Master database $MasterDatabase does not exist.', 1;"
)

foreach ($db in $serviceDatabases) {
    $createDatabasesSql += "IF DB_ID('$db') IS NULL CREATE DATABASE [$db];"
}

$sqlcmd = Get-SqlCmdPath
Invoke-SqlCmdChecked -Arguments @("-S", $Server, "-E", "-Q", ($createDatabasesSql -join " "))

$syncObjectsSqlTemplate = @"
SET NOCOUNT ON;

DECLARE @master sysname = N'$MasterDatabase';
DECLARE @target sysname = DB_NAME();
DECLARE @sql nvarchar(max) = N'';
DECLARE @allow TABLE (name sysname NOT NULL PRIMARY KEY);
__ALLOW_INSERTS__

SELECT @sql = @sql +
    N'IF EXISTS (SELECT 1 FROM sys.synonyms WHERE name = N''' + o.name + N''' AND schema_id = SCHEMA_ID(N''dbo'')) ' +
    N'DROP SYNONYM dbo.' + QUOTENAME(o.name) + N';' + CHAR(10)
FROM
(
    SELECT name FROM [$MasterDatabase].sys.tables WHERE schema_id = SCHEMA_ID(N'dbo')
    UNION
    SELECT name FROM [$MasterDatabase].sys.views WHERE schema_id = SCHEMA_ID(N'dbo')
) AS o;

EXEC sp_executesql @sql;

SET @sql = N'';

SELECT @sql = @sql +
    N'IF NOT EXISTS (' +
    N'    SELECT 1' +
    N'    FROM sys.objects' +
    N'    WHERE schema_id = SCHEMA_ID(N''dbo'')' +
    N'      AND name = N''' + o.name + N'''' +
    N') AND NOT EXISTS (' +
    N'    SELECT 1' +
    N'    FROM sys.synonyms' +
    N'    WHERE schema_id = SCHEMA_ID(N''dbo'')' +
    N'      AND name = N''' + o.name + N'''' +
    N') ' +
    N'CREATE SYNONYM dbo.' + QUOTENAME(o.name) + N' FOR ' + QUOTENAME(@master) + N'.dbo.' + QUOTENAME(o.name) + N';' + CHAR(10)
FROM
(
    SELECT name FROM [$MasterDatabase].sys.tables WHERE schema_id = SCHEMA_ID(N'dbo')
    UNION
    SELECT name FROM [$MasterDatabase].sys.views WHERE schema_id = SCHEMA_ID(N'dbo')
) AS o
INNER JOIN @allow a ON a.name = o.name
ORDER BY o.name;

EXEC sp_executesql @sql;
"@

function New-AllowInsertSql {
    param([string[]]$Names)

    if ($null -eq $Names -or $Names.Count -eq 0) {
        return "-- no allowed synonyms for this service database"
    }

    return ($Names | ForEach-Object { "INSERT INTO @allow (name) VALUES (N'$_');" }) -join [Environment]::NewLine
}

foreach ($db in $serviceDatabases) {
    Write-Host "Preparing service database shell: $db"
    $allowList = switch ($db) {
        "RESTAURANT_CATALOG" { $catalogShellAllowList }
        "RESTAURANT_IDENTITY" { $identityShellAllowList }
        default { @(
            "ActiveOrders","Bills","Branches","BranchRevenue","Categories","CategoryDish","CustomerLoyalty","Customers",
            "DiningTables","DishDetails","Dishes","DishIngredients","EmployeeRoles","Employees","Ingredients","LoyaltyCards",
            "MenuCategory","Menus","OrderItemIngredients","OrderItems","Orders","OrderStatus","PasswordResetTokens",
            "PaymentMethod","Payments","PaymentStatus","Reports","Restaurants","TableNumbers","TableStatus"
        ) }
    }

    $syncObjectsSql = $syncObjectsSqlTemplate.Replace("__ALLOW_INSERTS__", (New-AllowInsertSql -Names $allowList))
    Invoke-SqlCmdChecked -Arguments @("-S", $Server, "-E", "-d", $db, "-Q", $syncObjectsSql, "-b")
}

if (Test-Path $catalogOwnershipScript) {
    Write-Host "Materializing Catalog ownership tables in RESTAURANT_CATALOG"
    Invoke-SqlCmdChecked -Arguments @("-S", $Server, "-E", "-d", "RESTAURANT_CATALOG", "-i", $catalogOwnershipScript, "-b")
}

if (Test-Path $identityOwnershipScript) {
    Write-Host "Materializing Identity ownership tables in RESTAURANT_IDENTITY"
    Invoke-SqlCmdChecked -Arguments @("-S", $Server, "-E", "-d", "RESTAURANT_IDENTITY", "-i", $identityOwnershipScript, "-b")
}

if (Test-Path $ordersOwnershipScript) {
    Write-Host "Materializing Orders ownership tables in RESTAURANT_ORDERS"
    Invoke-SqlCmdChecked -Arguments @("-S", $Server, "-E", "-d", "RESTAURANT_ORDERS", "-i", $ordersOwnershipScript, "-b")
}

Write-Host "OK: service database shells are ready on $Server"
