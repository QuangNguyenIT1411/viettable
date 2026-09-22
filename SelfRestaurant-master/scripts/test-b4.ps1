param(
    [string]$GatewayBaseUrl = "http://localhost:5100",
    [string]$CatalogBaseUrl = "http://localhost:5101",
    [switch]$KeepTestData
)

$ErrorActionPreference = "Stop"
$gateway = $GatewayBaseUrl.TrimEnd('/')
$catalog = $CatalogBaseUrl.TrimEnd('/')
$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$tail = $suffix.ToString().Substring($suffix.ToString().Length - 8)
$usernames = @("b4_a_$suffix", "b4_b_$suffix", "b4_c_$suffix")
$tableIds = @()

function Invoke-Json([string]$Method, [string]$Uri, [object]$Body = $null,
    [string]$Token = "", [string]$IdempotencyKey = "") {
    $headers = @{ "X-Correlation-Id" = "b4-$suffix" }
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    if ($IdempotencyKey) { $headers["Idempotency-Key"] = $IdempotencyKey }
    $args = @{ ErrorAction="Stop"; Method=$Method; Uri=$Uri; Headers=$headers; ContentType="application/json" }
    if ($null -ne $Body) { $args.Body = ($Body | ConvertTo-Json -Depth 10) }
    Invoke-RestMethod @args
}

function Read-ErrorBody($Failure) {
    $body = $Failure.ErrorDetails.Message
    $response = $Failure.Exception.Response
    if ([string]::IsNullOrWhiteSpace($body) -and $null -ne $response.Content) {
        if ($response.Content -is [System.Net.Http.HttpContent]) { $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() }
        else { $body = $response.Content }
    }
    if ([string]::IsNullOrWhiteSpace($body) -and $null -ne $response.PSObject.Methods["GetResponseStream"]) {
        $stream = $response.GetResponseStream()
        if ($null -ne $stream) {
            $reader = [IO.StreamReader]::new($stream)
            try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
    }
    $body
}

function Assert-HttpError([int]$Status, [string]$Code, [scriptblock]$Action) {
    try { & $Action | Out-Null; throw "Expected HTTP $Status ($Code), request succeeded." }
    catch {
        $failure = $_; $response = $failure.Exception.Response
        if ($null -eq $response -or $response.StatusCode.value__ -ne $Status) { throw }
        $payload = (Read-ErrorBody $failure) | ConvertFrom-Json
        if ($payload.code -ne $Code) { throw "Expected '$Code', got '$($payload.code)'." }
        if (-not $payload.correlationId) { throw "MobileApiError did not contain correlationId." }
    }
}

function Register([int]$Index) {
    Invoke-Json "Post" "$gateway/api/mobile/v1/auth/register" @{
        name="B4 Test $Index"; username=$usernames[$Index]; password="Pass123!"
        phoneNumber="0$($Index + 7)$tail"; email="$($usernames[$Index])@example.test"
    }
}

try {
    Assert-HttpError 401 "unauthorized" { Invoke-Json "Get" "$gateway/api/mobile/v1/dining-sessions/active" }
    $accounts = @((Register 0); (Register 1); (Register 2))
    $branches = Invoke-Json "Get" "$gateway/api/mobile/v1/branches"
    $branchId = $branches[0].branchId
    $catalogTables = Invoke-Json "Get" "$catalog/api/branches/$branchId/tables"
    $tables = @($catalogTables.tables | Where-Object { $_.isAvailable -and -not [string]::IsNullOrWhiteSpace($_.qrCode) } | Select-Object -First 2)
    if ($tables.Count -lt 2) { throw "B4 needs two available tables with QR codes." }
    $tableIds = @($tables[0].tableId, $tables[1].tableId)

    Assert-HttpError 404 "qr_not_found" {
        Invoke-Json "Post" "$gateway/api/mobile/v1/check-ins/qr/validate" @{ qrCode="missing-$suffix" } $accounts[0].accessToken
    }
    $walkinValidation = Invoke-Json "Post" "$gateway/api/mobile/v1/check-ins/qr/validate" @{ qrCode=$tables[0].qrCode } $accounts[0].accessToken
    if ($walkinValidation.outcome -ne "WALK_IN_AVAILABLE") { throw "QR without reservation did not return WALK_IN_AVAILABLE." }
    Write-Output "PASS QR validation and unknown QR rejection"

    $reservedAt = [DateTimeOffset]::UtcNow.AddMinutes(20).ToString("O")
    $reservation = Invoke-Json "Post" "$gateway/api/mobile/v1/reservations" @{
        branchId=$branchId; tableId=$tables[0].tableId; partySize=2; reservedAt=$reservedAt; note="B4 check-in"
    } $accounts[0].accessToken "reservation"
    $menu = Invoke-Json "Get" "$gateway/api/mobile/v1/branches/$branchId/menu"
    $dish = $menu.categories.dishes | Where-Object { $_.available } | Select-Object -First 1
    Invoke-Json "Put" "$gateway/api/mobile/v1/reservations/$($reservation.reservationId)/preorder" @{
        items=@(@{ dishId=$dish.dishId; quantity=2; note="B4 preorder" })
    } $accounts[0].accessToken | Out-Null
    Invoke-Json "Post" "$gateway/api/mobile/v1/reservations/$($reservation.reservationId)/preorder/submit" $null $accounts[0].accessToken "preorder" | Out-Null

    $mismatch = Invoke-Json "Post" "$gateway/api/mobile/v1/check-ins/qr/validate" @{
        qrCode=$tables[1].qrCode; reservationId=$reservation.reservationId
    } $accounts[0].accessToken
    if ($mismatch.outcome -ne "MISMATCH_CONFIRMATION_REQUIRED" -or -not $mismatch.confirmationRequired) {
        throw "Reservation/table mismatch was not surfaced for confirmation."
    }
    Assert-HttpError 409 "table_mismatch_confirmation_required" {
        Invoke-Json "Post" "$gateway/api/mobile/v1/check-ins/reservation" @{
            reservationId=$reservation.reservationId; qrCode=$tables[1].qrCode
        } $accounts[0].accessToken "checkin-mismatch"
    }
    Write-Output "PASS wrong-table scan requires explicit confirmation"

    $ready = Invoke-Json "Post" "$gateway/api/mobile/v1/check-ins/qr/validate" @{
        qrCode=$tables[0].qrCode; reservationId=$reservation.reservationId
    } $accounts[0].accessToken
    if ($ready.outcome -ne "READY") { throw "Assigned reservation table was not READY." }
    $session = Invoke-Json "Post" "$gateway/api/mobile/v1/check-ins/reservation" @{
        reservationId=$reservation.reservationId; qrCode=$tables[0].qrCode
    } $accounts[0].accessToken "checkin"
    $retry = Invoke-Json "Post" "$gateway/api/mobile/v1/check-ins/reservation" @{
        reservationId=$reservation.reservationId; qrCode=$tables[0].qrCode
    } $accounts[0].accessToken "checkin"
    if ($session.diningSessionId -ne $retry.diningSessionId -or -not $session.convertedOrderId -or -not $retry.alreadyCheckedIn) {
        throw "Reservation check-in retry or pre-order conversion failed."
    }
    Write-Output "PASS reservation check-in, stable retry and one-time pre-order conversion"

    $active = Invoke-Json "Get" "$gateway/api/mobile/v1/dining-sessions/active" $null $accounts[0].accessToken
    $detail = Invoke-Json "Get" "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)" $null $accounts[0].accessToken
    if ($active.diningSessionId -ne $session.diningSessionId -or $detail.sessionCode -ne $session.sessionCode) { throw "Session read projection failed." }
    Assert-HttpError 403 "forbidden" {
        Invoke-Json "Get" "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)" $null $accounts[1].accessToken
    }
    $cancel = Invoke-Json "Post" "$gateway/api/mobile/v1/dining-sessions/$($session.diningSessionId)/cancel-payment-request" $null $accounts[0].accessToken
    if ($cancel.status -ne "ACTIVE") { throw "Cancel payment request is not idempotent for ACTIVE session." }
    Write-Output "PASS active/detail ownership and cancel-payment-request guard"

    $walkin = Invoke-Json "Post" "$gateway/api/mobile/v1/check-ins/walk-in" @{ qrCode=$tables[1].qrCode } $accounts[1].accessToken "walkin"
    $walkinRetry = Invoke-Json "Post" "$gateway/api/mobile/v1/check-ins/walk-in" @{ qrCode=$tables[1].qrCode } $accounts[1].accessToken "walkin"
    if ($walkin.diningSessionId -ne $walkinRetry.diningSessionId) { throw "Walk-in retry created a duplicate session." }
    Assert-HttpError 409 "table_session_active" {
        Invoke-Json "Post" "$gateway/api/mobile/v1/check-ins/walk-in" @{ qrCode=$tables[1].qrCode } $accounts[2].accessToken "walkin-conflict"
    }
    Write-Output "PASS walk-in idempotency and one-active-session-per-table conflict"
    Write-Output "B4 runtime checks passed."
}
finally {
    if (-not $KeepTestData -and (docker inspect selfrestaurant-db 2>$null)) {
        foreach ($tableId in $tableIds) {
            try { Invoke-RestMethod -Method Post -Uri "http://localhost:5102/api/tables/$tableId/reset" -ContentType "application/json" -Body "{}" | Out-Null } catch { }
        }
        $names = "'" + ($usernames -join "','") + "'"
        $sql = "SET QUOTED_IDENTIFIER ON; USE RESTAURANT_ORDERS; DELETE oi FROM dbo.OrderItems oi JOIN dbo.Orders o ON o.OrderID=oi.OrderID JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=o.CustomerID WHERE c.Username IN ($names); DELETE dst FROM dbo.DiningSessionTables dst JOIN dbo.DiningSessions ds ON ds.SessionCode=dst.DiningSessionCode JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=ds.CustomerId WHERE c.Username IN ($names); DELETE ds FROM dbo.DiningSessions ds JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=ds.CustomerId WHERE c.Username IN ($names); DELETE o FROM dbo.Orders o JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=o.CustomerID WHERE c.Username IN ($names); DELETE p FROM dbo.ReservationPreOrders p JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=p.CustomerId WHERE c.Username IN ($names); USE RESTAURANT_CUSTOMERS; DELETE r FROM dbo.Reservations r JOIN RESTAURANT_IDENTITY.dbo.Customers c ON c.CustomerID=r.CustomerId WHERE c.Username IN ($names); USE RESTAURANT_IDENTITY; DELETE r FROM dbo.MobileRefreshTokens r JOIN dbo.Customers c ON c.CustomerID=r.CustomerId WHERE c.Username IN ($names); DELETE FROM dbo.CustomerSettings WHERE CustomerId IN (SELECT CustomerID FROM dbo.Customers WHERE Username IN ($names)); DELETE FROM dbo.Customers WHERE Username IN ($names);"
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sql))
        $command = "echo $encoded | base64 -d | /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -C"
        docker exec selfrestaurant-db /bin/bash -lc $command 2>$null | Out-Null
    }
}
