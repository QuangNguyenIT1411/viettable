[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$init = Get-Content (Join-Path $root 'docker/db-init.sh') -Raw
if ($init -notmatch 'RESTAURANT_BILLING\|RESTAURANT_ORDERS\|RESTAURANT_CUSTOMERS\)\s*#.*\r?\n\s*continue') {
    throw 'Docker fresh bootstrap may recreate Customers synonyms'
}
$localInit = Get-Content (Join-Path $root 'sql/setup-service-db-shells.ps1') -Raw
if ($localInit -notmatch '"RESTAURANT_CUSTOMERS"\s*\{\s*\$ownedShellAllowList\s*\}') {
    throw 'LocalDB fresh bootstrap may recreate Customers synonyms'
}
$source = Join-Path $root 'src/Services/SelfRestaurant.Customers.Api'
$hits = Get-ChildItem $source -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Select-String -Pattern '(?:\bRESTAURANT(?:_[A-Z]+)?\s*\.|\[\s*RESTAURANT(?:_[A-Z]+)?\s*\]\s*\.)'
if ($hits) { throw "Qualified cross-database Customers source found: $($hits[0].Path):$($hits[0].LineNumber)" }
$query = @'
SET NOCOUNT ON;
IF EXISTS (SELECT 1 FROM sys.synonyms) THROW 51000, 'Customers synonym regression', 1;
IF EXISTS (SELECT 1 FROM sys.sql_expression_dependencies WHERE referenced_database_name IS NOT NULL OR referenced_server_name IS NOT NULL)
    THROW 51000, 'Customers cross-database dependency regression', 1;
IF EXISTS (SELECT 1 FROM sys.sql_modules WHERE definition LIKE '%RESTAURANT.%' OR definition LIKE '%RESTAURANT[_]%')
    THROW 51000, 'Customers cross-database SQL module regression', 1;
SELECT COUNT(*) AS CustomersSynonyms FROM sys.synonyms;
SELECT COUNT(*) AS OwnedReservations FROM dbo.Reservations;
SELECT COUNT(*) AS OwnedReviews FROM dbo.CustomerReviews;
'@
$query | docker exec -i selfrestaurant-db sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -W -d RESTAURANT_CUSTOMERS'
if ($LASTEXITCODE -ne 0) { throw 'Customers runtime ownership assertion failed' }
Write-Output 'PASS Customers owned tables, zero aliases/cross-DB SQL references, fresh bootstrap guard'
