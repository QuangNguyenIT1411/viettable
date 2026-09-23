[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$sql=@'
SET NOCOUNT ON;
IF EXISTS(SELECT 1 FROM sys.synonyms) THROW 51000, 'Billing synonym regression', 1;
IF EXISTS(SELECT 1 FROM sys.sql_expression_dependencies WHERE referenced_database_name IS NOT NULL OR referenced_server_name IS NOT NULL)
 THROW 51000, 'Billing cross-database dependency regression', 1;
IF OBJECT_ID('billing.Bills','U') IS NULL THROW 51000, 'Owned Bills missing', 1;
IF EXISTS(SELECT 1 FROM sys.sql_modules WHERE definition LIKE '%RESTAURANT.%' OR definition LIKE '%RESTAURANT].%')
 THROW 51000, 'Qualified shared database SQL module found', 1;
SELECT COUNT(*) AS SynonymCount FROM sys.synonyms;
SELECT COUNT(*) AS LocalBillCount,SUM(TotalAmount) AS LocalBillTotal FROM billing.Bills;
SELECT COUNT(*) AS InvoiceCount,SUM(TotalAmount) AS InvoiceTotal FROM dbo.Invoices;
SELECT COUNT(*) AS PaymentCount,SUM(Amount) AS PaymentTotal FROM dbo.MobilePayments;
'@
$sql | docker exec -i selfrestaurant-db sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -W -d RESTAURANT_BILLING'
if ($LASTEXITCODE -ne 0) { throw 'Runtime ownership assertion failed' }
$paths=@((Join-Path $root 'src/Services/SelfRestaurant.Billing.Api'),(Join-Path $root 'sql/billing-owned-tables.sql'))
$bad=Get-ChildItem $paths -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' -and $_.Extension -in '.cs','.sql' } |
 Select-String -Pattern 'RESTAURANT\s*\]\s*\.|RESTAURANT\.dbo|CREATE\s+SYNONYM|base_object_name|PARSENAME\('
if ($bad) { throw "Production Billing cross-database source regression: $($bad.Path -join ', ')" }
$init=Get-Content (Join-Path $root 'docker/db-init.sh') -Raw
if ($init -notmatch '(?s)RESTAURANT_BILLING\).*?continue\s*;;') { throw 'Fresh setup must skip Billing synonym generation' }
Write-Output 'PASS Billing local schema, no synonyms/cross-DB SQL dependencies and production source guard'
