param(
    [string]$GatewayBaseUrl = "http://localhost:5100",
    [string]$CatalogBaseUrl = "http://localhost:5101",
    [string]$OrdersBaseUrl = "http://localhost:5102",
    [string]$BillingBaseUrl = "http://localhost:5105",
    [string]$VnPayHashSecret = $env:VNPAY_HASH_SECRET,
    [switch]$KeepTestData
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot/vnpay-test-helpers.ps1"
$gateway=$GatewayBaseUrl.TrimEnd('/');$catalog=$CatalogBaseUrl.TrimEnd('/');$orders=$OrdersBaseUrl.TrimEnd('/');$billing=$BillingBaseUrl.TrimEnd('/')
$suffix=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds();$tail=$suffix.ToString().Substring($suffix.ToString().Length-8);$username="b6_$suffix";$tableId=$null
function Invoke-Json([string]$Method,[string]$Uri,[object]$Body=$null,[string]$Token="",[string]$Key=""){
    $headers=@{"X-Correlation-Id"="b6-$suffix"};if($Token){$headers.Authorization="Bearer $Token"};if($Key){$headers["Idempotency-Key"]=$Key}
    $args=@{ErrorAction="Stop";Method=$Method;Uri=$Uri;Headers=$headers;ContentType="application/json"};if($null-ne$Body){$args.Body=$Body|ConvertTo-Json -Depth 12};Invoke-RestMethod @args
}
function ErrorBody($failure){$body=$failure.ErrorDetails.Message;$response=$failure.Exception.Response;if([string]::IsNullOrWhiteSpace($body)-and$null-ne$response.Content){if($response.Content-is[System.Net.Http.HttpContent]){$body=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()}else{$body=$response.Content}};if([string]::IsNullOrWhiteSpace($body)-and$null-ne$response.PSObject.Methods["GetResponseStream"]){$stream=$response.GetResponseStream();if($null-ne$stream){$reader=[IO.StreamReader]::new($stream);try{$body=$reader.ReadToEnd()}finally{$reader.Dispose()}}};$body}
function Assert-Error([int]$Status,[string]$Code,[scriptblock]$Action){try{&$Action|Out-Null;throw "Expected HTTP $Status ($Code)."}catch{$failure=$_;$response=$failure.Exception.Response;if($null-eq$response-or$response.StatusCode.value__-ne$Status){throw};$payload=(ErrorBody $failure)|ConvertFrom-Json;if($payload.code-ne$Code){throw "Expected '$Code', got '$($payload.code)'."}}}

try {
    foreach($attempt in 1..40){try{Invoke-RestMethod "$gateway/readyz" -TimeoutSec 2|Out-Null;break}catch{if($attempt-eq40){throw};Start-Sleep -Milliseconds 500}}
    $account=Invoke-Json Post "$gateway/api/mobile/v1/auth/register" @{name="B6 Test";username=$username;password="Pass123!";phoneNumber="09$tail";email="$username@example.test"}
    $branch=(Invoke-Json Get "$gateway/api/mobile/v1/branches")[0];$layout=Invoke-Json Get "$catalog/api/branches/$($branch.branchId)/tables"
    $table=$null;$session=$null
    foreach($candidate in @($layout.tables|Where-Object{$_.isAvailable-and-not[string]::IsNullOrWhiteSpace($_.qrCode)})){
        try{$session=Invoke-Json Post "$gateway/api/mobile/v1/check-ins/walk-in" @{qrCode=$candidate.qrCode} $account.accessToken "walkin-$($candidate.tableId)";$table=$candidate;break}catch{if($null-eq$_.Exception.Response-or$_.Exception.Response.StatusCode.value__-ne409){throw}}
    }
    if($null-eq$table-or$null-eq$session){throw "No runtime-available QR table."};$tableId=$table.tableId
    $menu=Invoke-Json Get "$gateway/api/mobile/v1/branches/$($branch.branchId)/menu";$dish=$menu.categories.dishes|Where-Object{$_.available}|Select-Object -First 1
    $order=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/orders" @{items=@(@{dishId=$dish.dishId;quantity=2})} $account.accessToken "food"

    Assert-Error 400 "idempotency_key_required" {Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/checkout/quote" @{} $account.accessToken}
    $quote=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/checkout/quote" @{} $account.accessToken "quote"
    $quoteRetry=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/checkout/quote" @{} $account.accessToken "quote"
    if($quote.checkoutQuoteId-ne$quoteRetry.checkoutQuoteId-or$quote.subtotal-ne$order.subtotal-or$quote.voucherDiscount-ne0-or$quote.pointsDiscount-ne0){throw "Quote snapshot/default discounts/idempotency failed."}
    Write-Output "PASS immutable session quote, zero default discount and stable retry"

    $vouchers=Invoke-Json Get "$gateway/api/mobile/v1/me/vouchers" $null $account.accessToken;$welcome=$vouchers|Where-Object{$_.code-eq"WELCOME10"}|Select-Object -First 1
    if($null-eq$welcome){throw "Seed voucher missing."}
    $discounted=Invoke-Json Post "$gateway/api/mobile/v1/checkout/quotes/$($quote.checkoutQuoteId)/voucher" @{voucherId=$welcome.voucherId} $account.accessToken
    $withPoints=Invoke-Json Post "$gateway/api/mobile/v1/checkout/quotes/$($quote.checkoutQuoteId)/loyalty" @{points=0} $account.accessToken
    if($discounted.voucherDiscount-le0-or$withPoints.totalAmount-ne($quote.subtotal-$discounted.voucherDiscount)){throw "Explicit voucher/loyalty calculation failed."}
    Write-Output "PASS explicit voucher and loyalty selection with server-owned totals"

    $locked=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/payment-request" @{checkoutQuoteId=$quote.checkoutQuoteId} $account.accessToken
    if($locked.status-ne"PAYMENT_REQUESTED"){throw "Payment request did not lock quote/session."}
    Assert-Error 409 "session_payment_locked" {Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/orders" @{items=@(@{dishId=$dish.dishId;quantity=1})} $account.accessToken "late-food"}
    Assert-Error 400 "invalid_payment_method" {Invoke-Json Post "$gateway/api/mobile/v1/checkout/quotes/$($quote.checkoutQuoteId)/payments" @{channel="ONLINE";method="CASH";location="TABLE"} $account.accessToken "bad-method"}
    Write-Output "PASS payment request locks ordering and channel/method/location validation"

    $paymentBody=@{channel="ONLINE";method="QR"}
    $payment=Invoke-Json Post "$gateway/api/mobile/v1/checkout/quotes/$($quote.checkoutQuoteId)/payments" $paymentBody $account.accessToken "pay-1"
    $paymentRetry=Invoke-Json Post "$gateway/api/mobile/v1/checkout/quotes/$($quote.checkoutQuoteId)/payments" $paymentBody $account.accessToken "pay-1"
    if($payment.paymentId-ne$paymentRetry.paymentId-or$payment.status-ne"PENDING"-or$payment.providerName-ne"VNPAY"-or[string]::IsNullOrWhiteSpace($payment.providerReference)-or-not$payment.paymentUrl.StartsWith("https://sandbox.vnpayment.vn/")){throw "Payment creation/replay/provider session failed."}
    # ONSITE is exercised independently by test-onsite-payment.ps1 with provider disabled.
    # One quote must not collect the same obligation through two pending channels.
    Assert-Error 409 "payment_in_progress" {Invoke-Json Post "$gateway/api/mobile/v1/checkout/quotes/$($quote.checkoutQuoteId)/payments" @{channel="ONSITE";method="CASH";location="TABLE"} $account.accessToken "onsite-compat"}
    $unknown=Invoke-VnPayTestIpn -BillingBaseUrl $billing -ProviderReference "$($payment.providerReference)-UNKNOWN" -Amount $payment.amount -ResponseCode "00" -TransactionNo "UNKNOWN$tail" -HashSecret $VnPayHashSecret
    if($unknown.RspCode-ne"01"){throw "Unknown provider transaction was not rejected."}
    $invalid=Invoke-VnPayTestIpn -BillingBaseUrl $billing -ProviderReference $payment.providerReference -Amount $payment.amount -ResponseCode "00" -TransactionNo "BAD$tail" -HashSecret $VnPayHashSecret -InvalidSignature
    if($invalid.RspCode-ne"97"){throw "Invalid webhook signature was not rejected."}
    $wrongAmount=Invoke-VnPayTestIpn -BillingBaseUrl $billing -ProviderReference $payment.providerReference -Amount ($payment.amount+1) -ResponseCode "00" -TransactionNo "AMT$tail" -HashSecret $VnPayHashSecret
    if($wrongAmount.RspCode-ne"04"){throw "Signed amount mismatch was not rejected."}
    $stillPending=Invoke-Json Get "$gateway/api/mobile/v1/payments/$($payment.paymentId)" $null $account.accessToken
    if($stillPending.status-ne"PENDING"){throw "Rejected webhook mutated payment state."}
    Write-Output "PASS duplicate-channel collection guard and signed VNPAY webhook rejects unknown reference, invalid signature and amount mismatch"
    Invoke-VnPayTestIpn -BillingBaseUrl $billing -ProviderReference $payment.providerReference -Amount $payment.amount -ResponseCode "05" -TransactionNo "B6FAIL$tail" -HashSecret $VnPayHashSecret|Out-Null
    $retry=Invoke-Json Post "$gateway/api/mobile/v1/payments/$($payment.paymentId)/retry" @{} $account.accessToken "retry-1"
    if($retry.attemptNumber-ne2-or$retry.status-ne"PENDING"){throw "Failed payment retry failed."}
    $success=Invoke-VnPayTestIpn -BillingBaseUrl $billing -ProviderReference $retry.providerReference -Amount $retry.amount -ResponseCode "00" -TransactionNo "B6OK$tail" -HashSecret $VnPayHashSecret
    if($success.RspCode-ne"00"){throw "Successful webhook was not acknowledged."}
    $duplicate=Invoke-VnPayTestIpn -BillingBaseUrl $billing -ProviderReference $retry.providerReference -Amount $retry.amount -ResponseCode "00" -TransactionNo "B6OK$tail" -HashSecret $VnPayHashSecret
    if($duplicate.RspCode-ne"02"){throw "Duplicate terminal webhook was not rejected idempotently."}
    $paid=Invoke-Json Get "$gateway/api/mobile/v1/payments/$($retry.paymentId)" $null $account.accessToken
    $invoice=Invoke-Json Get "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/invoice" $null $account.accessToken
    $closed=Invoke-Json Get "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)" $null $account.accessToken
    if($paid.status-ne"SUCCEEDED"-or$invoice.totalAmount-ne$withPoints.totalAmount-or$closed.status-ne"CLOSED"){throw "Successful webhook/invoice/session closure failed."}
    Write-Output "PASS idempotent payment, failed retry, successful webhook, invoice and session closure"
    Write-Output "B6 runtime checks passed."
}
finally {
    if(-not$KeepTestData-and(docker inspect selfrestaurant-db 2>$null)){
        if($tableId){try{Invoke-RestMethod -Method Post -Uri "$orders/api/tables/$tableId/reset" -ContentType "application/json" -Body "{}"|Out-Null}catch{}}
        $sql="SET QUOTED_IDENTIFIER ON; USE RESTAURANT_BILLING; DELETE FROM dbo.SplitBillParts WHERE SplitBillId IN (SELECT SplitBillId FROM dbo.SplitBills WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username='$username')); DELETE FROM dbo.SplitBills WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username='$username'); DELETE FROM dbo.Invoices WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username='$username'); DELETE FROM dbo.Payments WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username='$username'); DELETE FROM dbo.CheckoutQuotes WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username='$username'); USE RESTAURANT_ORDERS; DELETE oi FROM dbo.OrderItems oi JOIN dbo.Orders o ON o.OrderID=oi.OrderID JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=o.CustomerID WHERE c.Username='$username'; DELETE dst FROM dbo.DiningSessionTables dst JOIN dbo.DiningSessions ds ON ds.SessionCode=dst.DiningSessionCode JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=ds.CustomerId WHERE c.Username='$username'; DELETE ds FROM dbo.DiningSessions ds JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=ds.CustomerId WHERE c.Username='$username'; DELETE o FROM dbo.Orders o JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=o.CustomerID WHERE c.Username='$username'; USE RESTAURANT_IDENTITY; DELETE r FROM dbo.MobileRefreshTokens r JOIN dbo.Customers c ON c.CustomerID=r.CustomerId WHERE c.Username='$username'; DELETE FROM dbo.CustomerSettings WHERE CustomerId IN (SELECT CustomerID FROM dbo.Customers WHERE Username='$username'); DELETE FROM dbo.Customers WHERE Username='$username';"
        $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sql));$command="echo $encoded | base64 -d | /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -C";docker exec selfrestaurant-db /bin/bash -lc $command 2>$null|Out-Null
    }
}
