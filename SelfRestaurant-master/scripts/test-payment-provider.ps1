param(
    [string]$TmnCode = $env:VNPAY_TMN_CODE,
    [string]$HashSecret = $env:VNPAY_HASH_SECRET,
    [string]$ReturnUrl = $env:VNPAY_RETURN_URL,
    [switch]$UseLocalContractCredentials,
    [switch]$LeaveProviderConfigured
)

$ErrorActionPreference = "Stop"
$generatedCredentials = $false
if ($UseLocalContractCredentials) {
    $TmnCode = "LOCALTEST"
    $HashSecret = [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")
    $ReturnUrl = "http://localhost:5105/api/payments/providers/vnpay/return"
    $generatedCredentials = $true
}
if ([string]::IsNullOrWhiteSpace($TmnCode) -or [string]::IsNullOrWhiteSpace($HashSecret) -or [string]::IsNullOrWhiteSpace($ReturnUrl)) {
    throw "Set VNPAY_TMN_CODE, VNPAY_HASH_SECRET and VNPAY_RETURN_URL, or use -UseLocalContractCredentials for local signed-IPN contract tests."
}

try {
    $env:VNPAY_ENABLED = "true"
    $env:VNPAY_TMN_CODE = $TmnCode
    $env:VNPAY_HASH_SECRET = $HashSecret
    $env:VNPAY_RETURN_URL = $ReturnUrl
    docker compose up -d --force-recreate billing gateway | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not start Billing/Gateway with VNPAY configuration." }

    & "$PSScriptRoot/test-b6.ps1" -VnPayHashSecret $HashSecret
    & "$PSScriptRoot/test-split-bill.ps1" -VnPayHashSecret $HashSecret
    Write-Output "Payment provider contract runtime checks passed."
    if ($generatedCredentials) {
        Write-Output "NOTE local credentials validate VietTable signing/state transitions only; they are not a real VNPAY merchant transaction."
    }
}
finally {
    if ($generatedCredentials -and -not $LeaveProviderConfigured) {
        Remove-Item Env:VNPAY_ENABLED -ErrorAction SilentlyContinue
        Remove-Item Env:VNPAY_TMN_CODE -ErrorAction SilentlyContinue
        Remove-Item Env:VNPAY_HASH_SECRET -ErrorAction SilentlyContinue
        Remove-Item Env:VNPAY_RETURN_URL -ErrorAction SilentlyContinue
        docker compose up -d --force-recreate billing gateway | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warning "Runtime tests passed, but Billing/Gateway could not be restored to provider-disabled defaults." }
    }
}
