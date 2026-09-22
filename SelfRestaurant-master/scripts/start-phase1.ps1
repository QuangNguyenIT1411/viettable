param(
    [switch]$Rebuild,
    [switch]$EnableRabbitMq
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$dotnetExe = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
if (!(Test-Path $dotnetExe)) {
    $dotnetExe = "dotnet"
}
$npmCmd = Join-Path $env:ProgramFiles "nodejs\npm.cmd"
if (!(Test-Path $npmCmd)) {
    $npmCmd = "npm.cmd"
}

$gatewayProjectRelative = "src\Gateway\SelfRestaurant.Gateway.Api\SelfRestaurant.Gateway.Api.csproj"
if (!(Test-Path (Join-Path $root $gatewayProjectRelative))) {
    throw "Gateway.Api project was not found under src\Gateway."
}
$gatewayProjectDir = Split-Path $gatewayProjectRelative -Parent
$gatewayProjectName = [System.IO.Path]::GetFileNameWithoutExtension($gatewayProjectRelative)

$frontendApps = @(
    @{ Name = "customer"; Dir = (Join-Path $root "src\Frontend\selfrestaurant-customer-web"); Dist = (Join-Path $root "src\Frontend\selfrestaurant-customer-web\dist") },
    @{ Name = "chef"; Dir = (Join-Path $root "src\Frontend\selfrestaurant-chef-web"); Dist = (Join-Path $root "src\Frontend\selfrestaurant-chef-web\dist") },
    @{ Name = "cashier"; Dir = (Join-Path $root "src\Frontend\selfrestaurant-cashier-web"); Dist = (Join-Path $root "src\Frontend\selfrestaurant-cashier-web\dist") },
    @{ Name = "admin"; Dir = (Join-Path $root "src\Frontend\selfrestaurant-admin-web"); Dist = (Join-Path $root "src\Frontend\selfrestaurant-admin-web\dist") }
)

function Invoke-DotnetChecked {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $proc = Start-Process -FilePath $dotnetExe -ArgumentList $Arguments -Wait -NoNewWindow -PassThru
    if ($proc.ExitCode -ne 0) {
        throw "$dotnetExe $($Arguments -join ' ') failed with exit code $($proc.ExitCode)"
    }
}

function Build-FrontendApp {
    param([string]$Dir)

    Push-Location $Dir
    try {
        & $npmCmd install
        if (-not $?) { throw "npm install failed for $Dir" }
        & $npmCmd run build
        if (-not $?) { throw "npm run build failed for $Dir" }
    }
    finally {
        Pop-Location
    }
}

Write-Host "Stopping old running processes..."
$names = @(
    "SelfRestaurant.Catalog.Api",
    "SelfRestaurant.Orders.Api",
    "SelfRestaurant.Customers.Api",
    "SelfRestaurant.Identity.Api",
    "SelfRestaurant.Billing.Api",
    "SelfRestaurant.Gateway.Api"
)
foreach ($n in $names) {
    Get-Process -Name $n -ErrorAction SilentlyContinue | Stop-Process -Force
}

Write-Host "Preparing LocalDB schema..."
powershell.exe -ExecutionPolicy Bypass -File "$root\sql\setup-localdb.ps1"
powershell.exe -ExecutionPolicy Bypass -File "$root\sql\materialize-owned-sql.ps1" -Database "RESTAURANT_ORDERS" -ScriptPath "$root\sql\orders-owned-tables.sql"

if ($Rebuild) {
    Write-Host "Building frontend bundles..."
    foreach ($app in $frontendApps) {
        Build-FrontendApp -Dir $app.Dir
    }

    Write-Host "Building service binaries..."
    Invoke-DotnetChecked -Arguments @("build", "src\Services\SelfRestaurant.Catalog.Api\SelfRestaurant.Catalog.Api.csproj", "-c", "Release")
    Invoke-DotnetChecked -Arguments @("build", "src\Services\SelfRestaurant.Orders.Api\SelfRestaurant.Orders.Api.csproj", "-c", "Release")
    Invoke-DotnetChecked -Arguments @("build", "src\Services\SelfRestaurant.Customers.Api\SelfRestaurant.Customers.Api.csproj", "-c", "Release")
    Invoke-DotnetChecked -Arguments @("build", "src\Services\SelfRestaurant.Identity.Api\SelfRestaurant.Identity.Api.csproj", "-c", "Release")
    Invoke-DotnetChecked -Arguments @("build", "src\Services\SelfRestaurant.Billing.Api\SelfRestaurant.Billing.Api.csproj", "-c", "Release")
    Invoke-DotnetChecked -Arguments @("build", $gatewayProjectRelative, "-c", "Release")
}
else {
    foreach ($app in $frontendApps) {
        if (!(Test-Path $app.Dist)) {
            Write-Host "Building missing frontend bundle for $($app.Name)..."
            Build-FrontendApp -Dir $app.Dir
        }
    }
}

$logDir = Join-Path $root ".runlogs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$services = @(
    @{ Name = "catalog"; Exe = "src\Services\SelfRestaurant.Catalog.Api\bin\Release\net8.0\SelfRestaurant.Catalog.Api.exe"; Url = "http://localhost:5101" },
    @{ Name = "orders"; Exe = "src\Services\SelfRestaurant.Orders.Api\bin\Release\net8.0\SelfRestaurant.Orders.Api.exe"; Url = "http://localhost:5102" },
    @{ Name = "customers"; Exe = "src\Services\SelfRestaurant.Customers.Api\bin\Release\net8.0\SelfRestaurant.Customers.Api.exe"; Url = "http://localhost:5103" },
    @{ Name = "identity"; Exe = "src\Services\SelfRestaurant.Identity.Api\bin\Release\net8.0\SelfRestaurant.Identity.Api.exe"; Url = "http://localhost:5104" },
    @{ Name = "billing"; Exe = "src\Services\SelfRestaurant.Billing.Api\bin\Release\net8.0\SelfRestaurant.Billing.Api.exe"; Url = "http://localhost:5105" },
    @{ Name = "gateway"; Exe = (Join-Path $gatewayProjectDir ("bin\Release\net8.0\" + $gatewayProjectName + ".exe")); Url = "http://localhost:5100" }
)

Write-Host "Starting services..."
foreach ($svc in $services) {
    $exe = Join-Path $root $svc.Exe
    $wd = Split-Path $exe -Parent
    $out = Join-Path $logDir "$($svc.Name).out.log"
    $err = Join-Path $logDir "$($svc.Name).err.log"

    $env:ASPNETCORE_URLS = $svc.Url
    # Keep the local phase-1 stack in one environment so developer-only
    # orchestration endpoints (for example reset-test-state) are consistently
    # available across gateway and downstream services during QA validation.
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:Services__Catalog = "http://localhost:5101"
    $env:Services__Orders = "http://localhost:5102"
    $env:Services__Customers = "http://localhost:5103"
    $env:Services__Identity = "http://localhost:5104"
    $env:Services__Billing = "http://localhost:5105"
    if ($svc.Name -eq "gateway") {
        $env:Frontend__CustomerDistPath = (Join-Path $root "src\Frontend\selfrestaurant-customer-web\dist")
        $env:Frontend__ChefDistPath = (Join-Path $root "src\Frontend\selfrestaurant-chef-web\dist")
        $env:Frontend__CashierDistPath = (Join-Path $root "src\Frontend\selfrestaurant-cashier-web\dist")
        $env:Frontend__AdminDistPath = (Join-Path $root "src\Frontend\selfrestaurant-admin-web\dist")
    }
    else {
        $env:Frontend__CustomerDistPath = $null
        $env:Frontend__ChefDistPath = $null
        $env:Frontend__CashierDistPath = $null
        $env:Frontend__AdminDistPath = $null
    }
    if ($EnableRabbitMq -and ($svc.Name -in @("orders", "billing"))) {
        $env:RabbitMq__Enabled = "true"
        $env:RabbitMq__Host = "localhost"
        $env:RabbitMq__Port = "5672"
        $env:RabbitMq__Username = "guest"
        $env:RabbitMq__Password = "guest"
        $env:RabbitMq__VirtualHost = "/"
        $env:RabbitMq__Exchange = "selfrestaurant.events"
        $env:RabbitMq__RoutingKeyPrefix = "selfrestaurant"
    }
    else {
        $env:RabbitMq__Enabled = $null
        $env:RabbitMq__Host = $null
        $env:RabbitMq__Port = $null
        $env:RabbitMq__Username = $null
        $env:RabbitMq__Password = $null
        $env:RabbitMq__VirtualHost = $null
        $env:RabbitMq__Exchange = $null
        $env:RabbitMq__RoutingKeyPrefix = $null
    }

    Start-Process -FilePath $exe -WorkingDirectory $wd -RedirectStandardOutput $out -RedirectStandardError $err | Out-Null
}

$env:ASPNETCORE_URLS = $null
$env:ASPNETCORE_ENVIRONMENT = $null
$env:Services__Catalog = $null
$env:Services__Orders = $null
$env:Services__Customers = $null
$env:Services__Identity = $null
$env:Services__Billing = $null
$env:Frontend__CustomerDistPath = $null
$env:Frontend__ChefDistPath = $null
$env:Frontend__CashierDistPath = $null
$env:Frontend__AdminDistPath = $null
$env:RabbitMq__Enabled = $null
$env:RabbitMq__Host = $null
$env:RabbitMq__Port = $null
$env:RabbitMq__Username = $null
$env:RabbitMq__Password = $null
$env:RabbitMq__VirtualHost = $null
$env:RabbitMq__Exchange = $null
$env:RabbitMq__RoutingKeyPrefix = $null

Start-Sleep -Seconds 3

Write-Host "Health checks:"
$checks = @(
    "http://localhost:5101/healthz",
    "http://localhost:5102/healthz",
    "http://localhost:5103/healthz",
    "http://localhost:5104/healthz",
    "http://localhost:5105/healthz",
    "http://localhost:5100/healthz",
    "http://localhost:5100/"
)
foreach ($url in $checks) {
    try {
        $res = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10
        Write-Host "OK  $url => $($res.StatusCode)"
    }
    catch {
        Write-Host "FAIL $url => $($_.Exception.Message)"
    }
}

Write-Host "Done. Logs are in .runlogs/"
