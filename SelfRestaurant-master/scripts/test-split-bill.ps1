param(
    [string]$GatewayBaseUrl = "http://localhost:5100",
    [string]$CatalogBaseUrl = "http://localhost:5101",
    [string]$OrdersBaseUrl = "http://localhost:5102",
    [string]$BillingBaseUrl = "http://localhost:5105",
    [string]$VnPayHashSecret = $env:VNPAY_HASH_SECRET,
    [switch]$KeepTestData,
    [switch]$MixedOnsite
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot/vnpay-test-helpers.ps1"
$gateway=$GatewayBaseUrl.TrimEnd('/');$catalog=$CatalogBaseUrl.TrimEnd('/');$orders=$OrdersBaseUrl.TrimEnd('/');$billing=$BillingBaseUrl.TrimEnd('/')
$suffix=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds();$tail=$suffix.ToString().Substring($suffix.ToString().Length-8);$username="split_$suffix";$tableId=$null
function Invoke-Json([string]$Method,[string]$Uri,[object]$Body=$null,[string]$Token="",[string]$Key=""){
    $headers=@{"X-Correlation-Id"="split-$suffix"};if($Token){$headers.Authorization="Bearer $Token"};if($Key){$headers["Idempotency-Key"]=$Key}
    $args=@{ErrorAction="Stop";Method=$Method;Uri=$Uri;Headers=$headers;ContentType="application/json"};if($null-ne$Body){$args.Body=$Body|ConvertTo-Json -Depth 12};Invoke-RestMethod @args
}
function ErrorBody($failure){$body=$failure.ErrorDetails.Message;$response=$failure.Exception.Response;if([string]::IsNullOrWhiteSpace($body)-and$null-ne$response.Content){if($response.Content-is[System.Net.Http.HttpContent]){$body=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()}else{$body=$response.Content}};if([string]::IsNullOrWhiteSpace($body)-and$null-ne$response.PSObject.Methods["GetResponseStream"]){$stream=$response.GetResponseStream();if($null-ne$stream){$reader=[IO.StreamReader]::new($stream);try{$body=$reader.ReadToEnd()}finally{$reader.Dispose()}}};$body}
function Assert-Error([int]$Status,[string]$Code,[scriptblock]$Action){try{&$Action|Out-Null;throw "Expected HTTP $Status ($Code)."}catch{$failure=$_;$response=$failure.Exception.Response;if($null-eq$response-or$response.StatusCode.value__-ne$Status){throw};$payload=(ErrorBody $failure)|ConvertFrom-Json;if($payload.code-ne$Code){throw "Expected '$Code', got '$($payload.code)'."}}}

try {
    foreach($attempt in 1..40){try{Invoke-RestMethod "$gateway/readyz" -TimeoutSec 2|Out-Null;break}catch{if($attempt-eq40){throw};Start-Sleep -Milliseconds 500}}
    $account=Invoke-Json Post "$gateway/api/mobile/v1/auth/register" @{name="Split Test";username=$username;password="Pass123!";phoneNumber="09$tail";email="$username@example.test"}
    $branches=Invoke-Json Get "$gateway/api/mobile/v1/branches"
    $branch=$branches[0]
    if ($MixedOnsite) {
        if (!$env:VIETTABLE_TEST_CASHIER_USERNAME -or !$env:VIETTABLE_TEST_CASHIER_PASSWORD) { throw 'Mixed acceptance requires test cashier credentials.' }
        $staff=New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $cashierLogin=Invoke-RestMethod -Method Post -Uri "$gateway/api/gateway/staff/cashier/auth/login" -WebSession $staff -ContentType 'application/json' -Body (@{username=$env:VIETTABLE_TEST_CASHIER_USERNAME;password=$env:VIETTABLE_TEST_CASHIER_PASSWORD}|ConvertTo-Json)
        $branch=$branches|Where-Object branchId -eq $cashierLogin.session.staff.branchId|Select-Object -First 1
        if (!$branch) { throw 'Cashier branch is not available.' }
    }
    $layout=Invoke-Json Get "$catalog/api/branches/$($branch.branchId)/tables"
    $table=$null;$session=$null
    foreach($candidate in @($layout.tables|Where-Object{$_.isAvailable-and-not[string]::IsNullOrWhiteSpace($_.qrCode)})){
        try{$session=Invoke-Json Post "$gateway/api/mobile/v1/check-ins/walk-in" @{qrCode=$candidate.qrCode} $account.accessToken "walkin-$($candidate.tableId)";$table=$candidate;break}catch{if($null-eq$_.Exception.Response-or$_.Exception.Response.StatusCode.value__-ne409){throw}}
    }
    if($null-eq$table-or$null-eq$session){throw "No runtime-available QR table."};$tableId=$table.tableId
    $menu=Invoke-Json Get "$gateway/api/mobile/v1/branches/$($branch.branchId)/menu";$dish=$menu.categories.dishes|Where-Object{$_.available}|Select-Object -First 1
    Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/orders" @{items=@(@{dishId=$dish.dishId;quantity=2})} $account.accessToken "food"|Out-Null
    $quote=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/checkout/quote" @{} $account.accessToken "quote"
    Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/payment-request" @{checkoutQuoteId=$quote.checkoutQuoteId} $account.accessToken|Out-Null

    $first=[long][Math]::Floor($quote.totalAmount/3);$last=[long]$quote.totalAmount-(2*$first);$amounts=@($first,$first,$last)
    Assert-Error 409 "split_total_mismatch" {Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/split-bill" @{checkoutQuoteId=$quote.checkoutQuoteId;amounts=@($first,$first,$first)} $account.accessToken "bad-total"}
    $split=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/split-bill" @{checkoutQuoteId=$quote.checkoutQuoteId;amounts=$amounts} $account.accessToken "split"
    $replay=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/split-bill" @{checkoutQuoteId=$quote.checkoutQuoteId;amounts=$amounts} $account.accessToken "split"
    Assert-Error 409 "split_bill_exists" {Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/split-bill" @{checkoutQuoteId=$quote.checkoutQuoteId;amounts=$amounts} $account.accessToken "another-split"}
    $loaded=Invoke-Json Get "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/split-bill" $null $account.accessToken
    if($split.splitBillId-ne$replay.splitBillId-or$split.splitBillId-ne$loaded.splitBillId-or(($loaded.parts|Measure-Object amount -Sum).Sum)-ne$quote.totalAmount){throw "Split idempotency/reload/total failed."}
    Write-Output "PASS canonical split creation, exact total, stable replay, duplicate guard and reload"

    $part=$loaded.parts[0]
    $created=Invoke-Json Post "$gateway/api/mobile/v1/split-bills/$($split.splitBillId)/payments" @{splitBillPartId=$part.splitBillPartId;channel="ONLINE";method="QR"} $account.accessToken "part-1"
    Invoke-VnPayTestIpn -BillingBaseUrl $billing -ProviderReference $created.payment.providerReference -Amount $created.payment.amount -ResponseCode "05" -TransactionNo "SPFAIL$tail" -HashSecret $VnPayHashSecret|Out-Null
    $failed=Invoke-Json Get "$gateway/api/mobile/v1/split-bills/$($split.splitBillId)" $null $account.accessToken
    if(($failed.parts|Where-Object splitBillPartId -eq $part.splitBillPartId).paymentStatus-ne"FAILED"){throw "Failed part status projection failed."}
    $retry=Invoke-Json Post "$gateway/api/mobile/v1/payments/$($created.payment.paymentId)/retry" @{} $account.accessToken "retry-part-1"
    Invoke-VnPayTestIpn -BillingBaseUrl $billing -ProviderReference $retry.providerReference -Amount $retry.amount -ResponseCode "00" -TransactionNo "SPRETRY$tail" -HashSecret $VnPayHashSecret|Out-Null
    $afterRetry=Invoke-Json Get "$gateway/api/mobile/v1/split-bills/$($split.splitBillId)" $null $account.accessToken
    $retriedPart=$afterRetry.parts|Where-Object splitBillPartId -eq $part.splitBillPartId
    if($retriedPart.status-ne"PAID"-or$retriedPart.paymentId-ne$retry.paymentId-or$retriedPart.paymentStatus-ne"SUCCEEDED"){throw "Split retry relink/finalization failed."}
    Write-Output "PASS failed split payment projection, real retry relink and paid part state"

    foreach($remaining in @($afterRetry.parts|Where-Object status -eq "UNPAID")){
        if ($MixedOnsite) {
            if (!$env:VIETTABLE_TEST_CASHIER_USERNAME -or !$env:VIETTABLE_TEST_CASHIER_PASSWORD) { throw 'Mixed acceptance requires test cashier credentials.' }
            $staff=New-Object Microsoft.PowerShell.Commands.WebRequestSession
            Invoke-RestMethod -Method Post -Uri "$gateway/api/gateway/staff/cashier/auth/login" -WebSession $staff -ContentType 'application/json' -Body (@{username=$env:VIETTABLE_TEST_CASHIER_USERNAME;password=$env:VIETTABLE_TEST_CASHIER_PASSWORD}|ConvertTo-Json) | Out-Null
            # The failed provider attempt must never be collectible by cashier, even after relink.
            try { Invoke-RestMethod -Method Post -Uri "$gateway/api/gateway/staff/cashier/onsite-payments/$($created.payment.paymentId)/confirm" -WebSession $staff -ContentType 'application/json' -Body '{}'|Out-Null; throw 'Old online attempt accepted by cashier' }
            catch { if ([int]$_.Exception.Response.StatusCode -ne 403) { throw } }
            $result=Invoke-Json Post "$gateway/api/mobile/v1/split-bills/$($split.splitBillId)/payments" @{splitBillPartId=$remaining.splitBillPartId;channel="ONSITE";method="CASH";location="CASHIER"} $account.accessToken "onsite-$($remaining.splitBillPartId)"
            Invoke-RestMethod -Method Post -Uri "$gateway/api/gateway/staff/cashier/onsite-payments/$($result.payment.paymentId)/confirm" -WebSession $staff -ContentType 'application/json' -Body '{}'|Out-Null
            continue
        }
        $result=Invoke-Json Post "$gateway/api/mobile/v1/split-bills/$($split.splitBillId)/payments" @{splitBillPartId=$remaining.splitBillPartId;channel="ONLINE";method="QR"} $account.accessToken "part-$($remaining.splitBillPartId)"
        Invoke-VnPayTestIpn -BillingBaseUrl $billing -ProviderReference $result.payment.providerReference -Amount $result.payment.amount -ResponseCode "00" -TransactionNo "SPOK$($remaining.splitBillPartId)$tail" -HashSecret $VnPayHashSecret|Out-Null
    }
    $paid=Invoke-Json Get "$gateway/api/mobile/v1/split-bills/$($split.splitBillId)" $null $account.accessToken
    $invoice=Invoke-Json Get "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/invoice" $null $account.accessToken
    $closed=Invoke-Json Get "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)" $null $account.accessToken
    if($paid.status-ne"PAID"-or($paid.parts|Where-Object status -ne "PAID")-or$invoice.totalAmount-ne$quote.totalAmount-or$closed.status-ne"CLOSED"){throw "Split completion/invoice/session closure failed."}
    Write-Output "PASS all parts paid, authoritative invoice total and backend-owned session closure"
    Write-Output "Split bill runtime checks passed."
}
finally {
    if(-not$KeepTestData-and(docker inspect selfrestaurant-db 2>$null)){
        if($tableId){try{Invoke-RestMethod -Method Post -Uri "$orders/api/tables/$tableId/reset" -ContentType "application/json" -Body "{}"|Out-Null}catch{}}
        $sql="SET QUOTED_IDENTIFIER ON; USE RESTAURANT_BILLING; DELETE FROM dbo.SplitBillParts WHERE SplitBillId IN (SELECT SplitBillId FROM dbo.SplitBills WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username='$username')); DELETE FROM dbo.SplitBills WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username='$username'); DELETE FROM dbo.Invoices WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username='$username'); DELETE FROM dbo.Payments WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username='$username'); DELETE FROM dbo.CheckoutQuotes WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username='$username'); USE RESTAURANT_ORDERS; DELETE oi FROM dbo.OrderItems oi JOIN dbo.Orders o ON o.OrderID=oi.OrderID JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=o.CustomerID WHERE c.Username='$username'; DELETE dst FROM dbo.DiningSessionTables dst JOIN dbo.DiningSessions ds ON ds.SessionCode=dst.DiningSessionCode JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=ds.CustomerId WHERE c.Username='$username'; DELETE o FROM dbo.Orders o JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=o.CustomerID WHERE c.Username='$username'; DELETE ds FROM dbo.DiningSessions ds JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=ds.CustomerId WHERE c.Username='$username'; USE RESTAURANT_IDENTITY; DELETE r FROM dbo.MobileRefreshTokens r JOIN dbo.Customers c ON c.CustomerID=r.CustomerId WHERE c.Username='$username'; DELETE FROM dbo.CustomerSettings WHERE CustomerId IN (SELECT CustomerID FROM dbo.Customers WHERE Username='$username'); DELETE FROM dbo.Customers WHERE Username='$username';"
        $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sql));$command="echo $encoded | base64 -d | /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -C";docker exec selfrestaurant-db /bin/bash -lc $command 2>$null|Out-Null
    }
}
