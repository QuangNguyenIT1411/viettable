param(
    [string]$GatewayBaseUrl = 'http://localhost:5100',
    [string]$CashierUsername = $env:VIETTABLE_TEST_CASHIER_USERNAME,
    [string]$CashierPassword = $env:VIETTABLE_TEST_CASHIER_PASSWORD,
    [switch]$CreateUiFixture,
    [switch]$PrepareAndroid,
    [switch]$Split
)
$ErrorActionPreference = 'Stop'
if (!$CashierUsername -or !$CashierPassword) { throw 'Supply a test cashier account via parameters or VIETTABLE_TEST_CASHIER_* environment variables.' }
$gateway = $GatewayBaseUrl.TrimEnd('/')
foreach ($readyUrl in @("$gateway/readyz", 'http://localhost:5105/readyz')) {
    foreach ($attempt in 1..30) {
        try { Invoke-RestMethod $readyUrl -TimeoutSec 2 | Out-Null; break }
        catch { if ($attempt -eq 30) { throw }; Start-Sleep -Milliseconds 500 }
    }
}
$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString()
$script:token = ''
$script:staff = New-Object Microsoft.PowerShell.Commands.WebRequestSession
function Api($method, $path, $body=$null, $cashier=$false, $key='') {
    $args = @{ Method=$method; Uri="$gateway$path"; ContentType='application/json'; Headers=@{'X-Correlation-Id'="onsite-$suffix"} }
    if ($cashier) { $args.WebSession=$script:staff } elseif ($script:token) { $args.Headers.Authorization="Bearer $script:token" }
    if ($key) { $args.Headers['Idempotency-Key']="$suffix-$key" }
    if ($null -ne $body) { $args.Body=$body | ConvertTo-Json -Depth 15 }
    Invoke-RestMethod @args
}
function Expect($condition,$message) { if (!$condition) { throw $message } }
function Status($expected,[scriptblock]$action) {
    try { & $action | Out-Null; throw "Expected HTTP $expected" }
    catch { if ([int]$_.Exception.Response.StatusCode -ne $expected) { throw } }
}
$login = Api Post '/api/gateway/staff/cashier/auth/login' @{username=$CashierUsername;password=$CashierPassword} $true
$branchId = $login.session.staff.branchId
Expect ($branchId -gt 0) 'Cashier must have a branch'
$username = "onsite_$suffix"
$account = Api Post '/api/mobile/v1/auth/register' @{name='Onsite Acceptance';username=$username;password='Pass123!';phoneNumber=('09'+$suffix.Substring($suffix.Length-8));email="$username@example.test"}
$script:token = $account.accessToken
$layout = Invoke-RestMethod "http://localhost:5101/api/branches/$branchId/tables"
$session = $null
foreach ($table in @($layout.tables | Where-Object { $_.isAvailable -and $_.qrCode })) {
    try { $session=Api Post '/api/mobile/v1/check-ins/walk-in' @{qrCode=$table.qrCode} $false "walk-$($table.tableId)"; break }
    catch { if ([int]$_.Exception.Response.StatusCode -ne 409) { throw } }
}
Expect ($null -ne $session) 'No available table in cashier branch; no records were deleted.'
$id = $session.diningSessionId
$menu = Api Get "/api/mobile/v1/branches/$branchId/menu"
$dish = $menu.categories.dishes | Where-Object available | Select-Object -First 1
Api Post "/api/mobile/v1/dining-sessions/$id/orders" @{items=@(@{dishId=$dish.dishId;quantity=2})} $false 'order' | Out-Null
if ($PrepareAndroid) {
    [pscustomobject]@{username=$username;sessionId=$id;branchId=$branchId}
    return
}
$quote = Api Post "/api/mobile/v1/dining-sessions/$id/checkout/quote" @{} $false 'quote'
Api Post "/api/mobile/v1/dining-sessions/$id/payment-request" @{checkoutQuoteId=$quote.checkoutQuoteId} | Out-Null
$paymentIds = @()
if ($Split) {
    $first = [long][Math]::Floor($quote.totalAmount/3)
    $splitBill = Api Post "/api/mobile/v1/dining-sessions/$id/split-bill" @{checkoutQuoteId=$quote.checkoutQuoteId;amounts=@($first,$first,[long]($quote.totalAmount-2*$first))} $false 'split'
    foreach ($part in $splitBill.parts) {
        $body = @{splitBillPartId=$part.splitBillPartId;channel='ONSITE';method='CASH';location='CASHIER'}
        $result = Api Post "/api/mobile/v1/split-bills/$($splitBill.splitBillId)/payments" $body $false "part-$($part.splitBillPartId)"
        $replay = Api Post "/api/mobile/v1/split-bills/$($splitBill.splitBillId)/payments" $body $false "part-$($part.splitBillPartId)"
        Expect ($result.payment.paymentId -eq $replay.payment.paymentId) 'Split creation retry duplicated payment'
        $paymentIds += $result.payment.paymentId
    }
} else {
    $payment = Api Post "/api/mobile/v1/checkout/quotes/$($quote.checkoutQuoteId)/payments" @{channel='ONSITE';method='CASH';location='CASHIER'} $false 'payment'
    Expect ($payment.status -eq 'PENDING' -and !$payment.providerReference) 'ONSITE must be provider-independent PENDING'
    $paymentIds += $payment.paymentId
}
if ($CreateUiFixture) {
    # Only test fixture metadata, no tokens/credentials. Test account uses the existing runtime suite password.
    [pscustomobject]@{username=$username;sessionId=$id;paymentIds=$paymentIds;quoteId=$quote.checkoutQuoteId;amount=$quote.totalAmount}
    return
}
$listing = Api Get '/api/gateway/staff/cashier/onsite-payments' $null $true
foreach ($pidValue in $paymentIds) { Expect (@($listing | Where-Object paymentId -eq $pidValue).Count -eq 1) 'Cashier listing missing payment' }
Status 401 { Api Post "/api/gateway/staff/cashier/onsite-payments/$($paymentIds[0])/confirm" @{} }
try { Invoke-RestMethod -Method Post -Uri "http://localhost:5105/api/internal/cashier/onsite-payments/$($paymentIds[0])/confirm" -ContentType 'application/json' -Body '{}' | Out-Null; throw 'Direct unauthenticated confirm accepted' }
catch { if ([int]$_.Exception.Response.StatusCode -ne 401) { throw } }
for ($i=0; $i -lt $paymentIds.Count; $i++) {
    $pidValue=$paymentIds[$i]
    if ($i -eq 0) {
        Add-Type -AssemblyName System.Net.Http
        $handler = [System.Net.Http.HttpClientHandler]::new(); $handler.CookieContainer=$script:staff.Cookies
        $http = [System.Net.Http.HttpClient]::new($handler)
        try {
            $uri="$gateway/api/gateway/staff/cashier/onsite-payments/$pidValue/confirm"
            $one=$http.PostAsync($uri,[System.Net.Http.StringContent]::new('{}',[Text.Encoding]::UTF8,'application/json'))
            $two=$http.PostAsync($uri,[System.Net.Http.StringContent]::new('{}',[Text.Encoding]::UTF8,'application/json'))
            [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($one,$two))
            Expect ($one.Result.IsSuccessStatusCode -and $two.Result.IsSuccessStatusCode) 'Concurrent cashier confirmation failed'
            $one.Result.Dispose(); $two.Result.Dispose()
        } finally { $http.Dispose() }
        Write-Output 'PASS concurrent cashier confirmations'
    }
    $result=Api Post "/api/gateway/staff/cashier/onsite-payments/$pidValue/confirm" @{amount=1} $true
    $duplicate=Api Post "/api/gateway/staff/cashier/onsite-payments/$pidValue/confirm" @{} $true
    $paid=Api Get "/api/mobile/v1/payments/$pidValue"
    Expect ($paid.status -eq 'SUCCEEDED' -and $paid.amount -eq $result.amount -and $duplicate.status -eq 'SUCCEEDED') 'Confirmation/replay failed'
    $current=Api Get "/api/mobile/v1/dining-sessions/$id"
    if ($i -lt $paymentIds.Count-1) {
        Expect ($current.status -eq 'PAYMENT_REQUESTED') 'Intermediate split prematurely closed session'
        Status 404 { Api Get "/api/mobile/v1/dining-sessions/$id/invoice" }
    }
    Write-Output "PASS cashier confirmation payment=$pidValue status=$($paid.status) session=$($current.status)"
}
$invoice=Api Get "/api/mobile/v1/dining-sessions/$id/invoice"
$invoiceReplay=Api Get "/api/mobile/v1/dining-sessions/$id/invoice"
Expect ($invoice.invoiceId -eq $invoiceReplay.invoiceId -and $invoice.totalAmount -eq $quote.totalAmount) 'Canonical invoice mismatch'
Expect ((Api Get "/api/mobile/v1/dining-sessions/$id").status -eq 'CLOSED') 'Session not closed'
if ($Split) {
    $final=Api Get "/api/mobile/v1/split-bills/$($splitBill.splitBillId)"
    Expect ($final.status -eq 'PAID' -and @($final.parts | Where-Object status -ne 'PAID').Count -eq 0) 'Split incomplete'
}
Write-Output "PASS ONSITE invoice=$($invoice.invoiceId), session=$id CLOSED; fixture retained, no SQL cleanup."
