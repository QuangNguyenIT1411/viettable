param(
    [switch]$EnsureReverse,
    [switch]$LaunchApp
)

$ErrorActionPreference = 'Stop'
$adb = 'D:\Quang\Android\platform-tools\adb.exe'
$package = 'com.restaurant.viettable'
$failed = [Collections.Generic.List[string]]::new()

function Resolve-DemoCredential([string]$UsernameVariable, [string]$PasswordVariable, [string]$FileName) {
    $username = [Environment]::GetEnvironmentVariable($UsernameVariable)
    $password = [Environment]::GetEnvironmentVariable($PasswordVariable)
    if ($username -and $password) { return @{ Username=$username; Password=$password } }
    $path = Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts\$FileName"
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $credential = Import-Clixml -LiteralPath $path
    if ($credential -isnot [System.Management.Automation.PSCredential]) { throw "Invalid credential artifact: $FileName" }
    return @{ Username=$credential.UserName; Password=$credential.GetNetworkCredential().Password }
}

function Report([string]$Name, [bool]$Ok, [string]$Reason = '') {
    if ($Ok) { Write-Host ("{0,-18} OK" -f $Name) }
    else {
        Write-Host ("{0,-18} FAIL {1}" -f $Name,$Reason)
        $script:failed.Add("${Name}: $Reason")
    }
}

try {
    & docker info --format '{{.ServerVersion}}' 2>$null | Out-Null
    $dockerOk = $LASTEXITCODE -eq 0
} catch { $dockerOk = $false }
Report 'Docker' $dockerOk 'Docker Desktop engine unavailable'

if ($dockerOk) {
    $services = [ordered]@{
        Gateway='selfrestaurant-gateway'; Catalog='selfrestaurant-catalog'
        Orders='selfrestaurant-orders'; Customers='selfrestaurant-customers'
        Identity='selfrestaurant-identity'; Billing='selfrestaurant-billing'
        SQL='selfrestaurant-db'; RabbitMQ='selfrestaurant-rabbitmq'
    }
    foreach ($item in $services.GetEnumerator()) {
        $running = & docker inspect --format '{{.State.Running}}' $item.Value 2>$null
        Report $item.Key ($LASTEXITCODE -eq 0 -and $running -eq 'true') "container $($item.Value) not running"
    }
    foreach ($port in 5100..5105) {
        try {
            $ready = Invoke-RestMethod "http://127.0.0.1:$port/readyz" -TimeoutSec 4
            Report "Ready :$port" ($ready.status -eq 'ready') 'unexpected ready response'
        } catch { Report "Ready :$port" $false 'HTTP readiness unavailable' }
    }
    try {
        $branches = @(Invoke-RestMethod 'http://127.0.0.1:5100/api/mobile/v1/branches' -TimeoutSec 5 | ForEach-Object { $_ })
        Report 'Branch data' ($branches.Count -gt 0) 'Gateway returned no branches'
        if ($branches.Count -gt 0) {
            $menu = Invoke-RestMethod "http://127.0.0.1:5100/api/mobile/v1/branches/$($branches[0].branchId)/menu" -TimeoutSec 5
            Report 'Menu data' (@($menu.categories).Count -gt 0) 'Gateway returned no menu categories'
        }
    } catch { Report 'Branch/menu' $false 'Gateway catalog query failed' }
    $sqlResult = @(& docker exec selfrestaurant-db sh -c '/opt/mssql-tools18/bin/sqlcmd -b -S localhost -U sa -P \"$MSSQL_SA_PASSWORD\" -C -h -1 -W -Q \"SELECT 1\"' 2>$null)
    Report 'SQL query' ($LASTEXITCODE -eq 0 -and $sqlResult -contains '1') 'SQL Server did not answer SELECT 1'
    & docker exec selfrestaurant-rabbitmq rabbitmq-diagnostics -q ping 2>$null | Out-Null
    Report 'RabbitMQ ping' ($LASTEXITCODE -eq 0) 'broker did not answer ping'
}

$device = $null
if (Test-Path -LiteralPath $adb) {
    $devices = @(& $adb devices -l | Select-String '^\S+\s+device\b')
    $samsung = @($devices | Where-Object { $_.Line -match 'model:SM_A528B\b' })
    if ($samsung.Count -eq 1) { $device = ($samsung[0].Line -split '\s+')[0] }
}
Report 'Samsung' ($null -ne $device) 'SM-A528B not connected/authorized'

if ($device) {
    $reverse = @(& $adb -s $device reverse --list)
    $hasReverse = @($reverse | Where-Object { $_ -match 'tcp:5100\s+tcp:5100' }).Count -gt 0
    if (-not $hasReverse -and $EnsureReverse) {
        & $adb -s $device reverse tcp:5100 tcp:5100 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $reverse = @(& $adb -s $device reverse --list)
            $hasReverse = @($reverse | Where-Object { $_ -match 'tcp:5100\s+tcp:5100' }).Count -gt 0
        }
    }
    Report 'ADB reverse' $hasReverse 'tcp:5100 mapping missing'
    $installed = @(& $adb -s $device shell pm path $package)
    $hasApk = $LASTEXITCODE -eq 0 -and @($installed | Where-Object { $_ -match '^package:' }).Count -gt 0
    Report 'APK installed' $hasApk 'install debug APK with demo-install-app.ps1'
    $batteryText = (& $adb -s $device shell dumpsys battery | Select-String '^\s*level:\s*(\d+)' | Select-Object -First 1).Matches.Groups[1].Value
    $battery = 0
    if ([int]::TryParse($batteryText, [ref]$battery)) {
        Report 'Battery' ($battery -ge 30) "$battery% (minimum 30%; prefer 60%)"
    }
    if ($LaunchApp -and $hasApk) {
        $since = [long]((& $adb -s $device shell date '+%s').Trim())
        $launch = @(& $adb -s $device shell am start -W -n "$package/.MainActivity" 2>&1)
        $launched = $LASTEXITCODE -eq 0 -and @($launch | Where-Object { $_ -match '^Status: ok' }).Count -gt 0
        Report 'App launch' $launched 'MainActivity did not launch'
        $fatal = @(& $adb -s $device logcat -d -v epoch -t 300 -s 'AndroidRuntime:E' '*:S' 2>$null | Where-Object {
            $_ -match '^\s*(\d+)\.' -and [long]$Matches[1] -ge $since -and $_ -match 'FATAL EXCEPTION'
        })
        Report 'Recent crash' ($fatal.Count -eq 0) 'AndroidRuntime FATAL EXCEPTION after launch'
    }
}

$customerCredential = Resolve-DemoCredential 'VIETTABLE_DEMO_USERNAME' 'VIETTABLE_DEMO_PASSWORD' 'demo-customer.credential.xml'
if ($customerCredential) {
    $login = $null
    try {
        $body = @{ username=$customerCredential.Username; password=$customerCredential.Password } | ConvertTo-Json
        $login = Invoke-RestMethod -Method Post 'http://127.0.0.1:5100/api/mobile/v1/auth/login' -Body $body -ContentType 'application/json' -TimeoutSec 8
        if (-not $login.accessToken) { throw 'missing access token' }
        $profile = Invoke-RestMethod -Method Get 'http://127.0.0.1:5100/api/mobile/v1/me' -Headers @{Authorization="Bearer $($login.accessToken)"} -TimeoutSec 8
        Report 'Demo customer' ([bool]$profile) 'authenticated profile unavailable'
    } catch { Report 'Demo customer' $false 'credentials rejected or API unavailable' }
    finally {
        if ($login -and $login.refreshToken) {
            try {
                $body = @{ refreshToken=$login.refreshToken } | ConvertTo-Json
                Invoke-RestMethod -Method Post 'http://127.0.0.1:5100/api/mobile/v1/auth/logout' -Body $body -ContentType 'application/json' -TimeoutSec 8 | Out-Null
            } catch { Write-Warning 'Could not revoke the short-lived preflight login session.' }
        }
    }
} else {
    Report 'Demo customer' $false 'set process env credential or encrypted artifacts/demo-customer.credential.xml'
}

$cashierCredential = Resolve-DemoCredential 'VIETTABLE_TEST_CASHIER_USERNAME' 'VIETTABLE_TEST_CASHIER_PASSWORD' 'demo-cashier.credential.xml'
if ($cashierCredential) {
    try {
        $staffSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $body = @{ username=$cashierCredential.Username; password=$cashierCredential.Password } | ConvertTo-Json
        $login = Invoke-RestMethod -Method Post 'http://127.0.0.1:5100/api/gateway/staff/cashier/auth/login' -Body $body -ContentType 'application/json' -WebSession $staffSession -TimeoutSec 8
        $staff = $login.session.staff
        $valid = $login.success -and $staff.roleCode -eq 'CASHIER' -and $staff.branchId -gt 0
        Report 'Demo cashier' $valid 'login failed, wrong role or no branch'
        if ($valid) { Write-Host "Cashier branch ID: $($staff.branchId)" }
    } catch { Report 'Demo cashier' $false 'credentials rejected or API unavailable' }
    finally {
        if ($staffSession) {
            try { Invoke-RestMethod -Method Post 'http://127.0.0.1:5100/api/gateway/staff/cashier/auth/logout' -WebSession $staffSession -TimeoutSec 8 | Out-Null } catch { }
        }
    }
} else {
    Report 'Demo cashier' $false 'set process env credential or encrypted artifacts/demo-cashier.credential.xml'
}

if ($failed.Count -eq 0) { Write-Host 'DEMO PREFLIGHT OK (business flow still requires manual rehearsal)'; exit 0 }
Write-Host 'DEMO NOT READY'
foreach ($reason in $failed) { Write-Host "Reason: $reason" }
exit 1
