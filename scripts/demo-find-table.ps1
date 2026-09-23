param([int]$BranchId = 0)

$ErrorActionPreference = 'Stop'
try {
    $branches = @(Invoke-RestMethod 'http://127.0.0.1:5100/api/mobile/v1/branches' -TimeoutSec 5 | ForEach-Object { $_ })
    # Fixed SELECT only. The password expands inside the SQL container and is never printed.
    $activeRows = @(& docker exec selfrestaurant-db sh -c '/opt/mssql-tools18/bin/sqlcmd -b -S localhost -U sa -P \"$MSSQL_SA_PASSWORD\" -C -d RESTAURANT_ORDERS -h -1 -W -Q \"SET NOCOUNT ON; SELECT TableId FROM dbo.DiningSessions WHERE EndedAtUtc IS NULL UNION SELECT TableID FROM dbo.Orders WHERE IsActive = 1;\"' 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Orders read-only query failed: $($activeRows -join ' ')" }
    $occupied = [Collections.Generic.HashSet[int]]::new()
    foreach ($row in $activeRows) {
        $parsed = 0
        if ([int]::TryParse($row.Trim(), [ref]$parsed)) { [void]$occupied.Add($parsed) }
    }
    Write-Host "Orders occupied table IDs: $($occupied.Count)"
    $matches = @()
    foreach ($branch in $branches) {
        if ($BranchId -gt 0 -and $branch.branchId -ne $BranchId) { continue }
        $catalog = Invoke-RestMethod "http://127.0.0.1:5101/api/branches/$($branch.branchId)/tables" -TimeoutSec 5
        foreach ($table in $catalog.tables) {
            if ($table.isAvailable -and -not $occupied.Contains([int]$table.tableId) -and
                -not [string]::IsNullOrWhiteSpace($table.qrCode)) {
                $matches += [pscustomobject]@{
                    Branch = $branch.name; BranchId = $branch.branchId
                    Table = $table.displayTableNumber; TableId = $table.tableId
                    Seats = $table.numberOfSeats; QrCode = $table.qrCode
                }
            }
        }
    }
    if ($matches.Count -eq 0) { Write-Host 'NO DEMO TABLE: no Catalog-available table without an active Orders session/order.'; exit 1 }
    $matches | Sort-Object BranchId,Table | Format-Table -AutoSize
    Write-Host 'Choose a row immediately before check-in; reservation/customer context may still affect validation.'
} catch {
    Write-Error $_.Exception.Message
    exit 1
}
