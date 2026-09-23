param([string]$CashierUsername=$env:VIETTABLE_TEST_CASHIER_USERNAME,
      [string]$CashierPassword=$env:VIETTABLE_TEST_CASHIER_PASSWORD)
$ErrorActionPreference='Stop'
# Explicit opt-in outage acceptance. Stops ONLY Orders, always restarts it. No DB writes.
$fixture = . "$PSScriptRoot/test-onsite-payment.ps1" -CashierUsername $CashierUsername -CashierPassword $CashierPassword -CreateUiFixture
$pidValue=$fixture.paymentIds[0]
try {
    docker stop selfrestaurant-orders | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not stop Orders for outage acceptance' }
    Api Post "/api/gateway/staff/cashier/onsite-payments/$pidValue/confirm" @{} $true | Out-Null
    $paid=Api Get "/api/mobile/v1/payments/$pidValue"
    $invoice=Api Get "/api/mobile/v1/dining-sessions/$($fixture.sessionId)/invoice"
    Expect ($paid.status -eq 'SUCCEEDED' -and $invoice.invoiceId -gt 0) 'Payment/invoice did not persist during Orders outage'
    Write-Output "PASS durable success during Orders outage: payment=$pidValue invoice=$($invoice.invoiceId)"
} finally {
    docker start selfrestaurant-orders | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Orders restart failed; restart selfrestaurant-orders immediately' }
}
$closed=$false
foreach($attempt in 1..30) {
    try { $current=Api Get "/api/mobile/v1/dining-sessions/$($fixture.sessionId)"; if($current.status -eq 'CLOSED'){$closed=$true;break} } catch {}
    Start-Sleep -Seconds 1
}
Expect $closed 'Reconciliation did not close session after Orders recovery'
Api Post "/api/gateway/staff/cashier/onsite-payments/$pidValue/confirm" @{} $true | Out-Null
Expect ((Api Get "/api/mobile/v1/dining-sessions/$($fixture.sessionId)/invoice").invoiceId -eq $invoice.invoiceId) 'Retry duplicated invoice'
Write-Output "PASS automatic reconciliation session=$($fixture.sessionId) CLOSED and stable invoice"
