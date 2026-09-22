param(
    [switch]$Rebuild,
    [switch]$BuildFrontend
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$dotnetExe = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
if (!(Test-Path $dotnetExe)) { $dotnetExe = "dotnet" }
$npmCmd = Join-Path $env:ProgramFiles "nodejs\npm.cmd"
if (!(Test-Path $npmCmd)) { $npmCmd = "npm.cmd" }
$gatewayApiProject = "src\Gateway\SelfRestaurant.Gateway.Api\SelfRestaurant.Gateway.Api.csproj"
$gatewayApiExe = Join-Path $root "src\Gateway\SelfRestaurant.Gateway.Api\bin\Release\net8.0\SelfRestaurant.Gateway.Api.exe"
$customerFrontendDir = Join-Path $root "src\Frontend\selfrestaurant-customer-web"
$customerFrontendDist = Join-Path $customerFrontendDir "dist"
$chefFrontendDir = Join-Path $root "src\Frontend\selfrestaurant-chef-web"
$chefFrontendDist = Join-Path $chefFrontendDir "dist"
$cashierFrontendDir = Join-Path $root "src\Frontend\selfrestaurant-cashier-web"
$cashierFrontendDist = Join-Path $cashierFrontendDir "dist"
$adminFrontendDir = Join-Path $root "src\Frontend\selfrestaurant-admin-web"
$adminFrontendDist = Join-Path $adminFrontendDir "dist"
$logDir = Join-Path $root ".runlogs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Invoke-Checked {
    param([string]$FilePath, [string[]]$Arguments, [string]$WorkingDirectory = $root)
    $proc = Start-Process -FilePath $FilePath -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory -Wait -NoNewWindow -PassThru
    if ($proc.ExitCode -ne 0) { throw "$FilePath $($Arguments -join ' ') failed with exit code $($proc.ExitCode)" }
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

Get-Process -Name "SelfRestaurant.Gateway.Api" -ErrorAction SilentlyContinue | Stop-Process -Force

if ($BuildFrontend -or !(Test-Path $customerFrontendDist)) {
    Build-FrontendApp -Dir $customerFrontendDir
}
if ($BuildFrontend -or !(Test-Path $chefFrontendDist)) {
    Build-FrontendApp -Dir $chefFrontendDir
}
if ($BuildFrontend -or !(Test-Path $cashierFrontendDist)) {
    Build-FrontendApp -Dir $cashierFrontendDir
}
if ($BuildFrontend -or !(Test-Path $adminFrontendDist)) {
    Build-FrontendApp -Dir $adminFrontendDir
}

if ($Rebuild) {
    Invoke-Checked -FilePath $dotnetExe -Arguments @("build", $gatewayApiProject, "-c", "Release")
}

$env:ASPNETCORE_URLS = "http://localhost:5110"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Frontend__CustomerDistPath = $customerFrontendDist
$env:Frontend__ChefDistPath = $chefFrontendDist
$env:Frontend__CashierDistPath = $cashierFrontendDist
$env:Frontend__AdminDistPath = $adminFrontendDist
$env:Services__Catalog = "http://localhost:5101"
$env:Services__Orders = "http://localhost:5102"
$env:Services__Customers = "http://localhost:5103"
$env:Services__Identity = "http://localhost:5104"
$env:Services__Billing = "http://localhost:5105"

Start-Process -FilePath $gatewayApiExe -WorkingDirectory (Split-Path $gatewayApiExe -Parent) -RedirectStandardOutput (Join-Path $logDir "gateway-api.out.log") -RedirectStandardError (Join-Path $logDir "gateway-api.err.log") | Out-Null
Start-Sleep -Seconds 3

try {
    $health = Invoke-WebRequest -Uri "http://localhost:5110/healthz" -UseBasicParsing -TimeoutSec 10
    Write-Host "OK  http://localhost:5110/healthz => $($health.StatusCode)"
} catch {
    Write-Host "FAIL http://localhost:5110/healthz => $($_.Exception.Message)"
}

$env:ASPNETCORE_URLS = $null
$env:ASPNETCORE_ENVIRONMENT = $null
$env:Frontend__CustomerDistPath = $null
$env:Frontend__ChefDistPath = $null
$env:Frontend__CashierDistPath = $null
$env:Frontend__AdminDistPath = $null
$env:Services__Catalog = $null
$env:Services__Orders = $null
$env:Services__Customers = $null
$env:Services__Identity = $null
$env:Services__Billing = $null
