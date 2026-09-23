# Read-only aggregate audit. Never mutates database rows or exposes customer details.
$ErrorActionPreference = 'Stop'
function Read-Count([string]$Database, [string]$Query) {
    $lines = @($Query | docker exec -i selfrestaurant-db sh -c ('SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -b -S localhost -U sa -C -d ' + $Database + ' -h -1 -W'))
    if ($LASTEXITCODE -ne 0) { throw "Read-only query failed for $Database" }
    return ($lines | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1)
}

try {
    $metrics = [ordered]@{
        ActiveDiningSessions = Read-Count 'RESTAURANT_ORDERS' 'SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.DiningSessions WHERE EndedAtUtc IS NULL;'
        ActiveOrders = Read-Count 'RESTAURANT_ORDERS' 'SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Orders WHERE IsActive = 1;'
        OpenServiceRequests = Read-Count 'RESTAURANT_ORDERS' "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.ServiceRequests WHERE Status = 'OPEN';"
        PendingPayments = Read-Count 'RESTAURANT_BILLING' "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.MobilePayments WHERE Status = 'PENDING';"
        OpenSplitBills = Read-Count 'RESTAURANT_BILLING' "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.SplitBills WHERE Status = 'OPEN';"
        UpcomingReservations = Read-Count 'RESTAURANT_CUSTOMERS' "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Reservations WHERE ReservedAt >= SYSUTCDATETIME() AND Status IN ('Pending','Confirmed','CheckingIn');"
    }
    $metrics.GetEnumerator() | ForEach-Object { Write-Host ("{0,-24} {1}" -f $_.Key,$_.Value) }
    Write-Host 'Read-only audit complete. Existing fixtures were not changed.'
} catch { Write-Error $_.Exception.Message; exit 1 }
