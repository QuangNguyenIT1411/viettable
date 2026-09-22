param(
    [switch]$SkipBuild,
    [switch]$SkipDocker,
    [switch]$Runtime,
    [switch]$RequireDotnetTestExecution
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solution = Join-Path $repoRoot "SelfRestaurant.Microservices.sln"

Push-Location $repoRoot
try {
    if (-not $SkipBuild) {
        dotnet restore $solution --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

        dotnet build $solution --no-restore --nologo -v:minimal
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }
    }

    $traceabilityPath = Join-Path $repoRoot "docs\MOBILE_B0_TRACEABILITY.md"
    $contractPath = Join-Path $repoRoot "docs\MOBILE_API_V1_CONTRACT.md"
    $traceability = Get-Content -Raw $traceabilityPath
    $contract = Get-Content -Raw $contractPath

    $screenIds = [regex]::Matches($traceability, '(?m)^\| S(?<id>\d{2}) \|') | ForEach-Object { [int]$_.Groups['id'].Value }
    $ruleIds = [regex]::Matches($traceability, '(?m)^\| R(?<id>\d{2}) \|') | ForEach-Object { [int]$_.Groups['id'].Value }
    if (($screenIds -join ',') -ne ((1..72) -join ',')) { throw "Traceability must contain continuous screen IDs S01-S72." }
    if (($ruleIds -join ',') -ne ((1..27) -join ',')) { throw "Traceability must contain continuous rule IDs R01-R27." }

    foreach ($token in @('/api/mobile/v1', 'Idempotency-Key', 'X-Correlation-Id', 'MobileApiError', 'PAYMENT_REQUESTED')) {
        if (-not $contract.Contains($token)) { throw "Mobile contract is missing required token: $token" }
    }

    $routeChecks = @(
        @{ File='src\Gateway\SelfRestaurant.Gateway.Api\Controllers\CustomerGatewayController.cs'; Route='[HttpGet("session")]'; Method=' GetSession(' },
        @{ File='src\Gateway\SelfRestaurant.Gateway.Api\Controllers\CustomerGatewayController.cs'; Route='[HttpPost("/api/customer/reservations")]'; Method=' CreateReservation(' },
        @{ File='src\Services\SelfRestaurant.Catalog.Api\Controllers\CatalogController.cs'; Route='[HttpGet("api/branches")]'; Method=' GetBranches(' },
        @{ File='src\Services\SelfRestaurant.Customers.Api\Controllers\CustomersController.cs'; Route='[HttpPost("api/customers/reservations")]'; Method=' CreateReservation(' },
        @{ File='src\Services\SelfRestaurant.Orders.Api\Controllers\OrdersController.cs'; Route='[HttpPost("api/tables/{tableId:int}/order/submit")]'; Method=' SubmitOrder(' },
        @{ File='src\Services\SelfRestaurant.Billing.Api\Controllers\CashierController.cs'; Route='[HttpPost("api/orders/{orderId:int}/checkout")]'; Method=' Checkout(' },
        @{ File='src\Services\SelfRestaurant.Identity.Api\Controllers\IdentityController.cs'; Route='[HttpPost("login")]'; Method=' Login(' }
    )
    foreach ($check in $routeChecks) {
        $source = Get-Content -Raw (Join-Path $repoRoot $check.File)
        if (-not $source.Contains($check.Route) -or -not $source.Contains($check.Method)) {
            throw "Baseline route check failed for $($check.File): $($check.Route)"
        }
    }
    Write-Host "PASS source contract checks (72 screens, 27 rules, conventions, baseline routes)."

    $testOutput = & dotnet test (Join-Path $repoRoot "tests\SelfRestaurant.Backend.ContractTests\SelfRestaurant.Backend.ContractTests.csproj") --no-restore --nologo -v:minimal 2>&1
    $testExitCode = $LASTEXITCODE
    $testText = $testOutput -join [Environment]::NewLine
    Write-Host $testText
    if ($testExitCode -ne 0) { throw "B0 contract tests failed." }
    $testExecutionBlocked = $testText -match 'No test is available|Application Control policy has blocked|Skipping:'
    if ($testExecutionBlocked) {
        if ($RequireDotnetTestExecution) { throw "The test assembly was built but execution was blocked by Windows Application Control." }
        Write-Warning "xUnit execution was blocked by Windows Application Control. Source contract checks passed; rerun with -RequireDotnetTestExecution on an unrestricted test host."
    }
    elseif ($testText -notmatch 'Passed:\s+[1-9]') {
        throw "dotnet test did not report any passing tests."
    }

    if (-not $SkipDocker) {
        $hasProcessPassword = -not [string]::IsNullOrWhiteSpace($env:MSSQL_SA_PASSWORD)
        $hasDotEnv = Test-Path (Join-Path $repoRoot ".env")
        if (-not $hasProcessPassword -and -not $hasDotEnv) {
            throw "MSSQL_SA_PASSWORD is not configured. Copy .env.example to .env and set a strong local password."
        }
        docker compose config --quiet
        if ($LASTEXITCODE -ne 0) { throw "docker compose config failed." }
    }

    if ($Runtime) {
        $healthEndpoints = @(
            "http://localhost:5100/healthz",
            "http://localhost:5101/healthz",
            "http://localhost:5102/healthz",
            "http://localhost:5103/healthz",
            "http://localhost:5104/healthz",
            "http://localhost:5105/healthz"
        )

        foreach ($endpoint in $healthEndpoints) {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $endpoint -TimeoutSec 10
            if ($response.StatusCode -ne 200) { throw "Health check failed: $endpoint" }
            Write-Host "PASS $endpoint"
        }
    }

    Write-Host "B0 checks passed."
}
finally {
    Pop-Location
}
