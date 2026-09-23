[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

# Read-only runtime inventory. Nonzero exit is expected while service DB shells
# still contain cross-database aliases; never mutate a database to make it pass.
$databases = @(
    'RESTAURANT', 'RESTAURANT_CATALOG', 'RESTAURANT_ORDERS',
    'RESTAURANT_CUSTOMERS', 'RESTAURANT_IDENTITY', 'RESTAURANT_BILLING'
)
$query = @'
SET NOCOUNT ON;
SELECT CONCAT(DB_NAME(), '|',
 (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0), '|',
 (SELECT COUNT(*) FROM sys.synonyms), '|',
 (SELECT COUNT(*) FROM sys.sql_expression_dependencies WHERE referenced_database_name IS NOT NULL OR referenced_server_name IS NOT NULL), '|',
 (SELECT COUNT(*) FROM sys.views v JOIN sys.sql_modules m ON m.object_id=v.object_id WHERE m.definition LIKE '%RESTAURANT.%' OR m.definition LIKE '%RESTAURANT[_]%'), '|',
 (SELECT COUNT(*) FROM sys.procedures p JOIN sys.sql_modules m ON m.object_id=p.object_id WHERE m.definition LIKE '%RESTAURANT.%' OR m.definition LIKE '%RESTAURANT[_]%'), '|',
 (SELECT COUNT(*) FROM sys.objects o JOIN sys.sql_modules m ON m.object_id=o.object_id WHERE o.type IN ('FN','IF','TF','FS','FT') AND (m.definition LIKE '%RESTAURANT.%' OR m.definition LIKE '%RESTAURANT[_]%')), '|',
 (SELECT COUNT(*) FROM sys.triggers t JOIN sys.sql_modules m ON m.object_id=t.object_id WHERE m.definition LIKE '%RESTAURANT.%' OR m.definition LIKE '%RESTAURANT[_]%'));
'@
$result = @()
foreach ($database in $databases) {
    $lines = $query | docker exec -i selfrestaurant-db sh -c ('SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -h -1 -W -d ' + $database)
    if ($LASTEXITCODE -ne 0) { throw "SQL metadata query failed for $database`: $($lines -join ' ')" }
    $row = @($lines | Where-Object { $_ -match '^RESTAURANT(?:_[A-Z]+)?\|\d+\|' })
    if ($row.Count -ne 1) { throw "Unexpected SQL metadata response for $database" }
    $fields = $row[0].Trim() -split '\|'
    $result += [pscustomobject]@{
        Database = $fields[0]; Tables = [int]$fields[1]; Synonyms = [int]$fields[2]
        CrossDbDependencies = [int]$fields[3]; CrossDbViews = [int]$fields[4]
        CrossDbProcedures = [int]$fields[5]; CrossDbFunctions = [int]$fields[6]
        CrossDbTriggers = [int]$fields[7]
    }
}
$result | Format-Table -AutoSize | Out-String -Width 200 | Write-Output

$root = Split-Path -Parent $PSScriptRoot
$runtimePaths = @((Join-Path $root 'src/Services'), (Join-Path $root 'src/Gateway'))
$sourceHits = Get-ChildItem $runtimePaths -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Select-String -Pattern '(?:\bRESTAURANT(?:_[A-Z]+)?\s*\.|\[\s*RESTAURANT(?:_[A-Z]+)?\s*\]\s*\.)'
if ($sourceHits) {
    $sourceHits | ForEach-Object { Write-Output "SOURCE REVIEW $($_.Path):$($_.LineNumber)" }
}

$violations = @($result | Where-Object {
    $_.Database -ne 'RESTAURANT' -and
    ($_.Synonyms -gt 0 -or $_.CrossDbDependencies -gt 0 -or $_.CrossDbViews -gt 0 -or
     $_.CrossDbProcedures -gt 0 -or $_.CrossDbFunctions -gt 0 -or $_.CrossDbTriggers -gt 0)
})
if ($violations.Count -gt 0 -or $sourceHits) {
    Write-Output ("Ownership guard FAILED: " + (($violations | ForEach-Object { $_.Database }) -join ', '))
    exit 1
}
Write-Output 'Ownership guard PASS'
