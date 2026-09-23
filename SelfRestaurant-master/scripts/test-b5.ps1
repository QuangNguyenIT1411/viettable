param(
    [string]$GatewayBaseUrl = "http://localhost:5100",
    [string]$CatalogBaseUrl = "http://localhost:5101",
    [string]$OrdersBaseUrl = "http://localhost:5102",
    [switch]$KeepTestData
)

$ErrorActionPreference = "Stop"
$gateway=$GatewayBaseUrl.TrimEnd('/'); $catalog=$CatalogBaseUrl.TrimEnd('/'); $orders=$OrdersBaseUrl.TrimEnd('/')
$suffix=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds(); $tail=$suffix.ToString().Substring($suffix.ToString().Length-8)
$users=@("b5_a_$suffix","b5_b_$suffix"); $tableId=$null

$ready=$false
foreach($attempt in 1..30){
    try{Invoke-RestMethod -Method Get -Uri "$orders/healthz" -TimeoutSec 2|Out-Null;$ready=$true;break}catch{Start-Sleep -Milliseconds 500}
}
if(-not $ready){throw "Orders service is not ready."}

function Invoke-Json([string]$Method,[string]$Uri,[object]$Body=$null,[string]$Token="",[string]$Key="") {
    $headers=@{"X-Correlation-Id"="b5-$suffix"}; if($Token){$headers.Authorization="Bearer $Token"}; if($Key){$headers["Idempotency-Key"]=$Key}
    $args=@{ErrorAction="Stop";Method=$Method;Uri=$Uri;Headers=$headers;ContentType="application/json"}
    if($null-ne $Body){$args.Body=$Body|ConvertTo-Json -Depth 10}; Invoke-RestMethod @args
}
function ErrorBody($failure){
    $body=$failure.ErrorDetails.Message; $response=$failure.Exception.Response
    if([string]::IsNullOrWhiteSpace($body)-and $null-ne $response.Content){
        if($response.Content-is [System.Net.Http.HttpContent]){$body=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()}else{$body=$response.Content}
    }
    if([string]::IsNullOrWhiteSpace($body)-and $null-ne $response.PSObject.Methods["GetResponseStream"]){
        $stream=$response.GetResponseStream(); if($null-ne $stream){$reader=[IO.StreamReader]::new($stream);try{$body=$reader.ReadToEnd()}finally{$reader.Dispose()}}
    }; $body
}
function Assert-Error([int]$Status,[string]$Code,[scriptblock]$Action){
    try{&$Action|Out-Null;throw "Expected HTTP $Status ($Code)."}catch{
        $failure=$_;$response=$failure.Exception.Response;if($null-eq $response-or $response.StatusCode.value__-ne $Status){throw}
        $payload=(ErrorBody $failure)|ConvertFrom-Json;if($payload.code-ne $Code){throw "Expected '$Code', got '$($payload.code)'."}
        if(-not $payload.correlationId){throw "Missing correlationId."}
    }
}
function Register([int]$i){Invoke-Json Post "$gateway/api/mobile/v1/auth/register" @{
    name="B5 Test $i";username=$users[$i];password="Pass123!";phoneNumber="0$($i+7)$tail";email="$($users[$i])@example.test"
}}

try {
    $accounts=@((Register 0);(Register 1))
    $branch=(Invoke-Json Get "$gateway/api/mobile/v1/branches")[0]
    $layout=Invoke-Json Get "$catalog/api/branches/$($branch.branchId)/tables"
    $table=$null;$session=$null
    foreach($candidate in @($layout.tables|Where-Object{$_.isAvailable-and-not[string]::IsNullOrWhiteSpace($_.qrCode)})){
        try{$session=Invoke-Json Post "$gateway/api/mobile/v1/check-ins/walk-in" @{qrCode=$candidate.qrCode} $accounts[0].accessToken "walkin-$($candidate.tableId)";$table=$candidate;break}
        catch{if($null-eq$_.Exception.Response-or$_.Exception.Response.StatusCode.value__-ne409){throw}}
    }
    if($null-eq$table-or$null-eq$session){throw "No runtime-available QR table."};$tableId=$table.tableId
    if($session.status-ne "ACTIVE"){throw "Walk-in session was not created."}

    $menu=Invoke-Json Get "$gateway/api/mobile/v1/branches/$($branch.branchId)/menu"
    $dish=$menu.categories.dishes|Where-Object{$_.available}|Select-Object -First 1
    Assert-Error 400 "idempotency_key_required" {
        Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/orders" @{items=@(@{dishId=$dish.dishId;quantity=1})} $accounts[0].accessToken
    }
    $body=@{items=@(@{dishId=$dish.dishId;quantity=1;note="B5 round one"});note="First round"}
    $order1=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/orders" $body $accounts[0].accessToken "round-1"
    $retry=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/orders" $body $accounts[0].accessToken "round-1"
    if($order1.orderId-ne $retry.orderId-or $order1.status-ne "CONFIRMED"){throw "Order retry or initial status failed."}
    Assert-Error 409 "idempotency_conflict" {
        Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/orders" @{items=@(@{dishId=$dish.dishId;quantity=2});note="Changed"} $accounts[0].accessToken "round-1"
    }
    $order2=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/orders" @{items=@(@{dishId=$dish.dishId;quantity=1});note="Second round"} $accounts[0].accessToken "round-2"
    if($order2.orderId-eq $order1.orderId-or $order2.diningSessionId-ne $session.diningSessionId){throw "Second round did not create another order in the same session."}
    Write-Output "PASS multi-round session orders, stable retry and request-hash conflict"

    $list=Invoke-Json Get "$gateway/api/mobile/v1/orders?scope=active" $null $accounts[0].accessToken
    $detail=Invoke-Json Get "$gateway/api/mobile/v1/orders/$($order1.orderId)" $null $accounts[0].accessToken
    $tracking=Invoke-Json Get "$gateway/api/mobile/v1/orders/$($order1.orderId)/tracking" $null $accounts[0].accessToken
    if($list.Count-lt 2-or $detail.items.Count-ne 1-or $tracking.statusCounts.CONFIRMED-ne 1){
        throw "Order list/detail/tracking failed: list=$($list.Count), detailItems=$($detail.items.Count), confirmed=$($tracking.statusCounts.CONFIRMED), payload=$($tracking|ConvertTo-Json -Depth 6 -Compress)"
    }
    Assert-Error 403 "forbidden" {Invoke-Json Get "$gateway/api/mobile/v1/orders/$($order1.orderId)" $null $accounts[1].accessToken}
    Invoke-RestMethod -Method Post -Uri "$orders/api/orders/$($order1.orderId)/chef/start"|Out-Null
    $progress=Invoke-Json Get "$gateway/api/mobile/v1/orders/$($order1.orderId)/tracking" $null $accounts[0].accessToken
    if($progress.statusCounts.PREPARING-ne 1){throw "Kitchen status was not reflected in tracking."}
    Write-Output "PASS owned order list/detail and item-level kitchen tracking"

    $serviceBody=@{requestType="WATER";note="Two glasses"}
    $service=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/service-requests" $serviceBody $accounts[0].accessToken "water"
    $serviceRetry=Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/service-requests" $serviceBody $accounts[0].accessToken "water"
    if($service.serviceRequestId-ne $serviceRetry.serviceRequestId-or $service.status-ne "OPEN"){throw "Service request retry failed."}
    Assert-Error 409 "idempotency_conflict" {
        Invoke-Json Post "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/service-requests" @{requestType="UTENSILS"} $accounts[0].accessToken "water"
    }
    $serviceList=Invoke-Json Get "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/service-requests" $null $accounts[0].accessToken
    $cancelled=Invoke-Json Post "$gateway/api/mobile/v1/service-requests/$($service.serviceRequestId)/cancel" $null $accounts[0].accessToken
    $cancelRetry=Invoke-Json Post "$gateway/api/mobile/v1/service-requests/$($service.serviceRequestId)/cancel" $null $accounts[0].accessToken
    if($serviceList.Count-ne 1-or $cancelled.status-ne "CANCELLED"-or $cancelRetry.status-ne "CANCELLED"){throw "Service request list/cancel failed."}
    Write-Output "PASS idempotent service request create/list/cancel lifecycle"

    Assert-Error 401 "unauthorized" {Invoke-Json Post "$gateway/hubs/mobile-notifications/negotiate?negotiateVersion=1"}
    $negotiate=Invoke-Json Post "$gateway/hubs/mobile-notifications/negotiate?negotiateVersion=1" $null $accounts[0].accessToken
    if(-not $negotiate.connectionToken){throw "Authorized mobile hub negotiation failed."}
    Write-Output "PASS authenticated customer-scoped mobile realtime hub negotiation"
    Write-Output "B5 runtime checks passed."
}
finally {
    if (-not $KeepTestData -and (docker inspect selfrestaurant-db 2>$null)) {
        if($tableId){try{Invoke-RestMethod -Method Post -Uri "$orders/api/tables/$tableId/reset" -ContentType "application/json" -Body "{}"|Out-Null}catch{}}
        $names="'"+($users-join "','")+"'"
        $sql="SET QUOTED_IDENTIFIER ON; USE RESTAURANT_ORDERS; DELETE sr FROM dbo.ServiceRequests sr JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=sr.CustomerId WHERE c.Username IN ($names); DELETE oi FROM dbo.OrderItems oi JOIN dbo.Orders o ON o.OrderID=oi.OrderID JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=o.CustomerID WHERE c.Username IN ($names); DELETE dst FROM dbo.DiningSessionTables dst JOIN dbo.DiningSessions ds ON ds.SessionCode=dst.DiningSessionCode JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=ds.CustomerId WHERE c.Username IN ($names); DELETE ds FROM dbo.DiningSessions ds JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=ds.CustomerId WHERE c.Username IN ($names); DELETE o FROM dbo.Orders o JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=o.CustomerID WHERE c.Username IN ($names); USE RESTAURANT_IDENTITY; DELETE r FROM dbo.MobileRefreshTokens r JOIN dbo.Customers c ON c.CustomerID=r.CustomerId WHERE c.Username IN ($names); DELETE FROM dbo.CustomerSettings WHERE CustomerId IN (SELECT CustomerID FROM dbo.Customers WHERE Username IN ($names)); DELETE FROM dbo.Customers WHERE Username IN ($names);"
        $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sql));$command="echo $encoded | base64 -d | /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -C"
        docker exec selfrestaurant-db /bin/bash -lc $command 2>$null|Out-Null
    }
}
