param(
    [string]$GatewayBaseUrl="http://localhost:5100", [string]$CatalogBaseUrl="http://localhost:5101",
    [string]$OrdersBaseUrl="http://localhost:5102", [string]$BillingBaseUrl="http://localhost:5105",
    [string]$VnPayHashSecret=$env:VNPAY_HASH_SECRET,
    [switch]$KeepTestData
)
$ErrorActionPreference="Stop";. "$PSScriptRoot/vnpay-test-helpers.ps1";$gateway=$GatewayBaseUrl.TrimEnd('/');$catalog=$CatalogBaseUrl.TrimEnd('/');$orders=$OrdersBaseUrl.TrimEnd('/');$billing=$BillingBaseUrl.TrimEnd('/')
$suffix=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds();$tail=$suffix.ToString().Substring($suffix.ToString().Length-8);$users=@("b7_a_$suffix","b7_b_$suffix");$tableId=$null
function Invoke-Json([string]$Method,[string]$Uri,[object]$Body=$null,[string]$Token="",[string]$Key=""){$headers=@{"X-Correlation-Id"="b7-$suffix"};if($Token){$headers.Authorization="Bearer $Token"};if($Key){$headers["Idempotency-Key"]=$Key};$args=@{ErrorAction="Stop";Method=$Method;Uri=$Uri;Headers=$headers;ContentType="application/json"};if($null-ne$Body){$args.Body=$Body|ConvertTo-Json -Depth 12};Invoke-RestMethod @args}
function ErrorBody($failure){$body=$failure.ErrorDetails.Message;$response=$failure.Exception.Response;if([string]::IsNullOrWhiteSpace($body)-and$null-ne$response.Content){if($response.Content-is[System.Net.Http.HttpContent]){$body=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()}else{$body=$response.Content}};if([string]::IsNullOrWhiteSpace($body)-and$null-ne$response.PSObject.Methods["GetResponseStream"]){$stream=$response.GetResponseStream();if($null-ne$stream){$reader=[IO.StreamReader]::new($stream);try{$body=$reader.ReadToEnd()}finally{$reader.Dispose()}}};$body}
function Assert-Error([int]$Status,[string]$Code,[scriptblock]$Action){try{&$Action|Out-Null;throw "Expected HTTP $Status ($Code)."}catch{$failure=$_;$response=$failure.Exception.Response;if($null-eq$response-or$response.StatusCode.value__-ne$Status){throw};$payload=(ErrorBody $failure)|ConvertFrom-Json;if($payload.code-ne$Code){throw "Expected '$Code', got '$($payload.code)'."}}}
function Register([int]$i){Invoke-Json Post "$gateway/api/mobile/v1/auth/register" @{name="B7 Test $i";username=$users[$i];password="Pass123!";phoneNumber="0$($i+7)$tail";email="$($users[$i])@example.test"}}
try{
    foreach($attempt in 1..40){try{Invoke-RestMethod "$gateway/readyz" -TimeoutSec 2|Out-Null;break}catch{if($attempt-eq40){throw};Start-Sleep -Milliseconds 500}}
    $accounts=@((Register 0);(Register 1));$branch=(Invoke-Json Get "$gateway/api/mobile/v1/branches")[0];$menu=Invoke-Json Get "$gateway/api/mobile/v1/branches/$($branch.branchId)/menu";$dish=$menu.categories.dishes|Where-Object{$_.available}|Select-Object -First 1

    $favorite=Invoke-Json Post "$gateway/api/mobile/v1/me/favorites/dishes/$($dish.dishId)" @{} $accounts[0].accessToken
    $favoriteReplay=Invoke-Json Post "$gateway/api/mobile/v1/me/favorites/dishes/$($dish.dishId)" @{} $accounts[0].accessToken
    $favorites=Invoke-Json Get "$gateway/api/mobile/v1/me/favorites" $null $accounts[0].accessToken;$otherFavorites=Invoke-Json Get "$gateway/api/mobile/v1/me/favorites" $null $accounts[1].accessToken
    if($favorite.favoriteId-ne$favoriteReplay.favoriteId-or$favorites.items.Count-ne1-or$otherFavorites.items.Count-ne0){throw "Favorite idempotency or customer isolation failed."}
    Invoke-Json Delete "$gateway/api/mobile/v1/me/favorites/dishes/$($dish.dishId)" $null $accounts[0].accessToken|Out-Null
    if((Invoke-Json Get "$gateway/api/mobile/v1/me/favorites" $null $accounts[0].accessToken).items.Count-ne0){throw "Favorite removal failed."}
    Write-Output "PASS Catalog-validated favorite add/list/remove and customer isolation"

    $faqs=Invoke-Json Get "$gateway/api/mobile/v1/support/faqs";if($faqs.items.Count-lt3){throw "FAQ list failed."}
    Assert-Error 400 "idempotency_key_required" {Invoke-Json Post "$gateway/api/mobile/v1/support/requests" @{category="GENERAL";subject="Test";message="Need help"} $accounts[0].accessToken}
    $supportBody=@{category="ORDER";subject="B7 support check";message="I need help with my order"}
    $support=Invoke-Json -Method Post -Uri "$gateway/api/mobile/v1/support/requests" -Body $supportBody -Token $accounts[0].accessToken -Key "support"
    $supportReplay=Invoke-Json -Method Post -Uri "$gateway/api/mobile/v1/support/requests" -Body $supportBody -Token $accounts[0].accessToken -Key "support"
    if($support.supportRequestId-ne$supportReplay.supportRequestId-or$support.status-ne"OPEN"){throw "Support retry failed."}
    Assert-Error 409 "idempotency_conflict" {Invoke-Json Post "$gateway/api/mobile/v1/support/requests" @{category="GENERAL";subject="Changed";message="Changed"} $accounts[0].accessToken "support"}
    Write-Output "PASS public FAQ and idempotent customer support request"

    Assert-Error 409 "review_not_eligible" {Invoke-Json Post "$gateway/api/mobile/v1/reviews" @{invoiceId=999999999;overallRating=5;foodRating=5;serviceRating=5;spaceRating=5;comment="Too early"} $accounts[0].accessToken "early-review"}
    $layout=Invoke-Json Get "$catalog/api/branches/$($branch.branchId)/tables";$table=$null;$session=$null
    foreach($candidate in @($layout.tables|Where-Object{$_.isAvailable-and-not[string]::IsNullOrWhiteSpace($_.qrCode)})){
        try{$session=Invoke-Json Post "$gateway/api/mobile/v1/check-ins/walk-in" @{qrCode=$candidate.qrCode} $accounts[0].accessToken "walkin-$($candidate.tableId)";$table=$candidate;break}catch{if($null-eq$_.Exception.Response-or$_.Exception.Response.StatusCode.value__-ne409){throw}}
    }
    if($null-eq$table-or$null-eq$session){throw "No runtime-available QR table."};$tableId=$table.tableId
    $order=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/orders" @{items=@(@{dishId=$dish.dishId;quantity=1})} $accounts[0].accessToken "food"
    Invoke-RestMethod -Method Post -Uri "$orders/api/orders/$($order.orderId)/chef/start"|Out-Null
    Invoke-RestMethod -Method Post -Uri "$orders/api/orders/$($order.orderId)/items/$($order.items[0].itemId)/chef/ready"|Out-Null
    $notification=$null;foreach($attempt in 1..30){$inbox=Invoke-Json Get "$gateway/api/mobile/v1/notifications?limit=10" $null $accounts[0].accessToken;if($inbox.items.Count-gt0){$notification=$inbox.items[0];break};Start-Sleep -Milliseconds 500}
    if($null-eq$notification-or$notification.isRead){throw "Ready notification was not projected."}
    $read=Invoke-Json Post "$gateway/api/mobile/v1/notifications/$($notification.notificationId)/read" @{} $accounts[0].accessToken;if(-not$read.isRead){throw "Notification read failed."}
    Assert-Error 403 "forbidden" {Invoke-Json Post "$gateway/api/mobile/v1/notifications/$($notification.notificationId)/read" @{} $accounts[1].accessToken}
    Write-Output "PASS paged notification inbox, ready-event projection, read and ownership"

    $quote=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/checkout/quote" @{} $accounts[0].accessToken "quote"
    Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/payment-request" @{checkoutQuoteId=$quote.checkoutQuoteId} $accounts[0].accessToken|Out-Null
    $payment=Invoke-Json Post "$gateway/api/mobile/v1/checkout/quotes/$($quote.checkoutQuoteId)/payments" @{channel="ONLINE";method="QR"} $accounts[0].accessToken "payment"
    Invoke-VnPayTestIpn -BillingBaseUrl $billing -ProviderReference $payment.providerReference -Amount $payment.amount -ResponseCode "00" -TransactionNo "B7OK$tail" -HashSecret $VnPayHashSecret|Out-Null
    $invoice=Invoke-Json Get "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/invoice" $null $accounts[0].accessToken
    $reviewBody=@{invoiceId=$invoice.invoiceId;overallRating=5;foodRating=5;serviceRating=4;spaceRating=4;comment="B7 review";imageUrls=@("https://example.test/review.jpg")}
    $review=Invoke-Json Post "$gateway/api/mobile/v1/reviews" $reviewBody $accounts[0].accessToken "review"
    $reviewReplay=Invoke-Json Post "$gateway/api/mobile/v1/reviews" $reviewBody $accounts[0].accessToken "review"
    if($review.reviewId-ne$reviewReplay.reviewId){throw "Review retry failed."}
    $updated=Invoke-Json Put "$gateway/api/mobile/v1/reviews/$($review.reviewId)" @{overallRating=4;foodRating=4;serviceRating=4;spaceRating=4;comment="Updated";imageUrls=@()} $accounts[0].accessToken
    Assert-Error 403 "forbidden" {Invoke-Json Put "$gateway/api/mobile/v1/reviews/$($review.reviewId)" @{overallRating=1;foodRating=1;serviceRating=1;spaceRating=1} $accounts[1].accessToken}
    $reviews=Invoke-Json Get "$gateway/api/mobile/v1/me/reviews" $null $accounts[0].accessToken;if($updated.overallRating-ne4-or$reviews.items.Count-ne1){throw "Review update/list failed."}
    Invoke-Json Delete "$gateway/api/mobile/v1/reviews/$($review.reviewId)" $null $accounts[0].accessToken|Out-Null
    Write-Output "PASS completed-transaction review eligibility, retry, ownership, update/list/delete"
    Write-Output "B7 runtime checks passed."
}
finally{
    if(-not$KeepTestData-and(docker inspect selfrestaurant-db 2>$null)){
        if($tableId){try{Invoke-RestMethod -Method Post -Uri "$orders/api/tables/$tableId/reset" -ContentType "application/json" -Body "{}"|Out-Null}catch{}}
        $names="'"+($users-join"','")+"'";$sql="SET QUOTED_IDENTIFIER ON; USE RESTAURANT_CUSTOMERS; DELETE FROM dbo.CustomerFavorites WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username IN ($names)); DELETE FROM dbo.CustomerReviews WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username IN ($names)); DELETE FROM dbo.SupportRequests WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username IN ($names)); DELETE FROM dbo.ReadyDishNotifications WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username IN ($names)); USE RESTAURANT_BILLING; DELETE FROM dbo.Invoices WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username IN ($names)); DELETE FROM dbo.MobilePayments WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username IN ($names)); DELETE FROM dbo.CheckoutQuotes WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username IN ($names)); USE RESTAURANT_ORDERS; DELETE sr FROM dbo.ServiceRequests sr JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=sr.CustomerId WHERE c.Username IN ($names); DELETE oi FROM dbo.OrderItems oi JOIN dbo.Orders o ON o.OrderID=oi.OrderID JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=o.CustomerID WHERE c.Username IN ($names); DELETE o FROM dbo.Orders o JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=o.CustomerID WHERE c.Username IN ($names); DELETE dst FROM dbo.DiningSessionTables dst JOIN dbo.DiningSessions ds ON ds.SessionCode=dst.DiningSessionCode JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=ds.CustomerId WHERE c.Username IN ($names); DELETE ds FROM dbo.DiningSessions ds JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=ds.CustomerId WHERE c.Username IN ($names); USE RESTAURANT_IDENTITY; DELETE r FROM dbo.MobileRefreshTokens r JOIN dbo.Customers c ON c.CustomerID=r.CustomerId WHERE c.Username IN ($names); DELETE FROM dbo.CustomerSettings WHERE CustomerId IN (SELECT CustomerID FROM dbo.Customers WHERE Username IN ($names)); DELETE FROM dbo.Customers WHERE Username IN ($names);";$encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sql));$command="echo $encoded | base64 -d | /opt/mssql-tools18/bin/sqlcmd -b -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -C";docker exec selfrestaurant-db /bin/bash -lc $command 2>$null|Out-Null;if($LASTEXITCODE-ne0){throw "B7 test-data cleanup failed."}
    }
}
