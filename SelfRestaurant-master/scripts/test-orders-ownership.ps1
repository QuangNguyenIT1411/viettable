[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$init = Get-Content (Join-Path $root 'docker/db-init.sh') -Raw
if ($init -notmatch 'RESTAURANT_BILLING\|RESTAURANT_ORDERS\)\s*#.*\r?\n\s*continue') {
    throw 'Fresh bootstrap may recreate Orders synonyms'
}
$source = Join-Path $root 'src/Services/SelfRestaurant.Orders.Api'
$hits = Get-ChildItem $source -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Select-String -Pattern '(?:\bRESTAURANT(?:_[A-Z]+)?\s*\.|\[\s*RESTAURANT(?:_[A-Z]+)?\s*\]\s*\.)'
if ($hits) { throw "Qualified cross-database Orders source found: $($hits[0].Path):$($hits[0].LineNumber)" }
$query = @'
SET NOCOUNT ON;
IF EXISTS (SELECT 1 FROM sys.synonyms) THROW 51000, 'Orders synonym regression', 1;
IF EXISTS (SELECT 1 FROM sys.sql_expression_dependencies WHERE referenced_database_name IS NOT NULL OR referenced_server_name IS NOT NULL)
    THROW 51000, 'Orders cross-database dependency regression', 1;
IF EXISTS (SELECT 1 FROM sys.sql_modules WHERE definition LIKE '%RESTAURANT.%' OR definition LIKE '%RESTAURANT[_]%')
    THROW 51000, 'Orders cross-database SQL module regression', 1;
SELECT COUNT(*) AS OrdersSynonyms FROM sys.synonyms;
SELECT COUNT(*) AS OwnedOrders FROM dbo.Orders;
SELECT COUNT(*) AS OwnedSessions FROM dbo.DiningSessions;
'@
$query | docker exec -i selfrestaurant-db sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -W -d RESTAURANT_ORDERS'
if ($LASTEXITCODE -ne 0) { throw 'Orders runtime ownership assertion failed' }
Write-Output 'PASS Orders owned tables, zero aliases/cross-DB SQL references, fresh bootstrap guard'
