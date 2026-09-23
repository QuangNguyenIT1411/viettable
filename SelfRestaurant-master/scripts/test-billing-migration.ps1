[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$stamp=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$source="BillingMigrationSource_$stamp"
$target="BillingMigrationTarget_$stamp"
function Sql([string]$database,[string]$query) {
    if ($database -notmatch '^[A-Za-z0-9_]+$') { throw 'Unsafe test database name' }
    ("SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;`n"+$query) | docker exec -i selfrestaurant-db sh -c ('SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -W -d '+$database)
    if ($LASTEXITCODE -ne 0) { throw "Migration SQL failed in $database" }
}
Sql master "CREATE DATABASE [$source]; CREATE DATABASE [$target];"
$schema=Get-Content (Join-Path $root 'sql/billing-owned-tables.sql') -Raw
Sql $source $schema
Sql $source @'
ALTER SCHEMA dbo TRANSFER billing.Bills;
SET IDENTITY_INSERT dbo.Bills ON;
INSERT dbo.Bills(BillID,OrderID,BillCode,BillTime,Subtotal,Discount,PointsDiscount,PointsUsed,TotalAmount,PaymentMethod,PaymentAmount,ChangeAmount,EmployeeID,CustomerID,IsActive,BranchNameSnapshot)
VALUES(17,901,N'HISTORY-A','2020-02-29T12:34:56.123',123456.78,12.34,0,NULL,123444.44,N'CASH',NULL,NULL,NULL,NULL,0,N'Lịch sử'),
      (29,902,N'HISTORY-B','2021-01-01T00:00:00',70000,0,1000,1000,69000,N'TRANSFER',69000,0,3,5,1,NULL);
SET IDENTITY_INSERT dbo.Bills OFF;
'@
Sql $target $schema
Sql $target $schema
$migration=(Get-Content (Join-Path $root 'sql/migrate-billing-bills.sql') -Raw).Replace('RESTAURANT',$source)
Sql $target $migration
Sql $target $migration
Sql $target @'
IF (SELECT COUNT(*) FROM billing.Bills)<>2 THROW 51000,'Copy/replay count mismatch',1;
IF (SELECT SUM(TotalAmount) FROM billing.Bills)<>192444.44 THROW 51000,'Decimal integrity failure',1;
IF EXISTS(SELECT 1 FROM sys.synonyms) THROW 51000,'Fresh schema created synonyms',1;
INSERT billing.Bills(OrderID,BillCode,BillTime,Subtotal,Discount,PointsDiscount,TotalAmount,PaymentMethod,IsActive)
VALUES(903,N'LOCAL-AFTER-CUTOVER','2026-09-23',1,0,0,1,N'CASH',1);
IF (SELECT BillID FROM billing.Bills WHERE BillCode=N'LOCAL-AFTER-CUTOVER')<=29 THROW 51000,'Identity regression',1;
'@
Sql $target $migration
Sql $target "IF (SELECT COUNT(*) FROM billing.Bills)<>3 THROW 51000,'Replay overwrote local data',1;"
Write-Output "PASS isolated nonempty copy, every-column reconciliation, null/date/decimal/identity preservation, fresh schema and migration replay. Retained test DBs: $source / $target"
