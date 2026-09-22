param(
    [int[]]$Ports = @(5100, 5101, 5102, 5103, 5104, 5105)
)

$ErrorActionPreference = "Stop"

Write-Host "Checking local dev ports: $($Ports -join ', ')"

$listeners = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
    Where-Object { $Ports -contains $_.LocalPort } |
    Sort-Object LocalPort, OwningProcess -Unique

if (-not $listeners) {
    Write-Host "No running listeners found on the configured service ports."
    return
}

foreach ($listener in $listeners) {
    $process = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
    if (-not $process) {
        Write-Host "Port $($listener.LocalPort) is bound by PID $($listener.OwningProcess), but the process no longer exists."
        continue
    }

    Write-Host "Stopping PID $($process.Id) [$($process.ProcessName)] on port $($listener.LocalPort)..."
    Stop-Process -Id $process.Id -Force
}

Write-Host "Done. The dev service ports are free for a clean restart."
