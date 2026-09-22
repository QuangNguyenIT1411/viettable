param(
    [string]$GatewayBaseUrl = "http://localhost:5100",
    [switch]$KeepTestData
)

$ErrorActionPreference = "Stop"
$gateway = $GatewayBaseUrl.TrimEnd('/')
$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$username1 = "b3test_a_$suffix"
$username2 = "b3test_b_$suffix"
$tail = $suffix.ToString().Substring($suffix.ToString().Length - 8)
$reservationId = $null
$reservationId2 = $null

function Invoke-Json([string]$Method, [string]$Uri, [object]$Body = $null,
    [string]$Token = "", [string]$IdempotencyKey = "") {
    $headers = @{ "X-Correlation-Id" = "b3-$suffix" }
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    if ($IdempotencyKey) { $headers["Idempotency-Key"] = $IdempotencyKey }
    $parameters = @{ ErrorAction="Stop"; Method=$Method; Uri=$Uri; Headers=$headers; ContentType="application/json" }
    if ($null -ne $Body) { $parameters.Body = ($Body | ConvertTo-Json -Depth 10) }
    Invoke-RestMethod @parameters
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
        if ($null -ne $stream) { $reader = [IO.StreamReader]::new($stream); try { $body=$reader.ReadToEnd() } finally { $reader.Dispose() } }
    }
    $body
}

function Assert-HttpError([int]$Status, [string]$Code, [scriptblock]$Action) {
    try { & $Action | Out-Null; throw "Expected HTTP $Status ($Code), request succeeded." }
    catch {
        $failure = $_; $response = $failure.Exception.Response
        if ($null -eq $response -or $response.StatusCode.value__ -ne $Status) { throw }
        $body = Read-ErrorBody $failure
        if ([string]::IsNullOrWhiteSpace($body)) { throw "HTTP error did not contain JSON." }
        $payload = $body | ConvertFrom-Json
        if ($payload.code -ne $Code) { throw "Expected '$Code', got '$($payload.code)'." }
        if (-not $payload.correlationId) { throw "MobileApiError did not contain correlationId." }
    }
}

function Register([string]$Username, [string]$Phone) {
    Invoke-Json "Post" "$gateway/api/mobile/v1/auth/register" @{
        name="B3 Test"; username=$Username; password="Pass123!"
        phoneNumber=$Phone; email="$Username@example.test"
    }
}

try {
    $branches = Invoke-Json "Get" "$gateway/api/mobile/v1/branches"
    if ($branches.Count -lt 1) { throw "No active branch returned." }
    $branchId = $branches[0].branchId
    $detail = Invoke-Json "Get" "$gateway/api/mobile/v1/branches/$branchId"
    $areas = Invoke-Json "Get" "$gateway/api/mobile/v1/branches/$branchId/areas"
    $layout = Invoke-Json "Get" "$gateway/api/mobile/v1/branches/$branchId/table-layout"
    $nearby = Invoke-Json "Get" "$gateway/api/mobile/v1/branches/nearby?latitude=10.77&longitude=106.70"
    if ($detail.branchId -ne $branchId -or $areas.Count -lt 1 -or $layout.tables.Count -lt 1 -or $nearby.Count -lt 1) {
        throw "Branch discovery response is incomplete."
    }
    Write-Output "PASS branch detail, nearby, areas and table layout discovery"

    $reservedAt = [DateTimeOffset]::UtcNow.AddDays(7).ToString("O")
    $encodedAt = [Uri]::EscapeDataString($reservedAt)
    $availability = Invoke-Json "Get" "$gateway/api/mobile/v1/branches/$branchId/availability?reservedAt=$encodedAt&partySize=4"
    if ($availability.availableTables.Count -lt 2) { throw "Not enough test tables are available." }
    $table1 = $availability.availableTables[0].tableId
    $table2 = $availability.availableTables[1].tableId
    Write-Output "PASS reservation availability and suitable table filtering"

    Assert-HttpError 401 "unauthorized" { Invoke-Json "Get" "$gateway/api/mobile/v1/reservations" }
    $account1 = Register $username1 "08$tail"
    $account2 = Register $username2 "09$tail"
    Assert-HttpError 400 "idempotency_key_required" {
        Invoke-Json "Post" "$gateway/api/mobile/v1/reservations" @{ branchId=$branchId; tableId=$table1; partySize=4; reservedAt=$reservedAt } $account1.accessToken
    }
    Write-Output "PASS reservation endpoints require Bearer and mutation idempotency key"

    $createBody = @{ branchId=$branchId; tableId=$table1; partySize=4; reservedAt=$reservedAt; note="Window seat" }
    $created = Invoke-Json "Post" "$gateway/api/mobile/v1/reservations" $createBody $account1.accessToken "create-1"
    $reservationId = $created.reservationId
    if (-not $reservationId -or $created.customerId -ne $account1.user.customerId -or $created.status -ne "Confirmed") {
        throw "Reservation was not created as confirmed for the signed-in customer."
    }
    $retry = Invoke-Json "Post" "$gateway/api/mobile/v1/reservations" $createBody $account1.accessToken "create-1"
    if ($retry.reservationId -ne $reservationId) { throw "Idempotent retry created another reservation." }
    Assert-HttpError 409 "idempotency_conflict" {
        Invoke-Json "Post" "$gateway/api/mobile/v1/reservations" @{ branchId=$branchId; tableId=$table1; partySize=5; reservedAt=$reservedAt; note="Window seat" } $account1.accessToken "create-1"
    }
    Write-Output "PASS create reservation, stable retry and idempotency conflict detection"

    Assert-HttpError 409 "reservation_slot_unavailable" {
        Invoke-Json "Post" "$gateway/api/mobile/v1/reservations" $createBody $account2.accessToken "create-conflict"
    }
    Assert-HttpError 403 "forbidden" { Invoke-Json "Get" "$gateway/api/mobile/v1/reservations/$reservationId" $null $account2.accessToken }
    Write-Output "PASS overlapping table conflict and cross-customer ownership protection"

    $list = Invoke-Json "Get" "$gateway/api/mobile/v1/reservations?scope=upcoming&limit=1" $null $account1.accessToken
    $detailReservation = Invoke-Json "Get" "$gateway/api/mobile/v1/reservations/$reservationId" $null $account1.accessToken
    $deposit = Invoke-Json "Get" "$gateway/api/mobile/v1/reservations/$reservationId/deposit" $null $account1.accessToken
    if ($list.items.Count -ne 1 -or $detailReservation.reservationId -ne $reservationId -or $deposit.status -ne "NOT_REQUIRED") {
        throw "Reservation list/detail/deposit projection failed."
    }
    Write-Output "PASS reservation list, detail and deposit policy"

    $newAt = [DateTimeOffset]::UtcNow.AddDays(8).ToString("O")
    $updated = Invoke-Json "Put" "$gateway/api/mobile/v1/reservations/$reservationId" @{
        tableId=$table2; partySize=4; reservedAt=$newAt; note="Changed once"
    } $account1.accessToken "update-1"
    if ($updated.branchId -ne $branchId -or $updated.tableId -ne $table2 -or $updated.note -ne "Changed once") {
        throw "Reservation update failed or changed branch."
    }
    Write-Output "PASS change reservation while preserving original branch"

    $menu = Invoke-Json "Get" "$gateway/api/mobile/v1/branches/$branchId/menu"
    $dish = $menu.categories[0].dishes | Where-Object { $_.available } | Select-Object -First 1
    if ($null -eq $dish) { throw "No available dish for pre-order test." }
    $draft = Invoke-Json "Put" "$gateway/api/mobile/v1/reservations/$reservationId/preorder" @{
        items=@(@{ dishId=$dish.dishId; quantity=2; note="Less spicy" })
    } $account1.accessToken
    if ($draft.status -ne "DRAFT" -or $draft.items.Count -ne 1 -or $draft.subtotal -le 0) { throw "Pre-order draft failed." }
    $submitted = Invoke-Json "Post" "$gateway/api/mobile/v1/reservations/$reservationId/preorder/submit" $null $account1.accessToken "preorder-1"
    $submittedRetry = Invoke-Json "Post" "$gateway/api/mobile/v1/reservations/$reservationId/preorder/submit" $null $account1.accessToken "preorder-1"
    if ($submitted.status -ne "SUBMITTED" -or $submittedRetry.status -ne "SUBMITTED" -or
        $submittedRetry.submittedAtUtc.Ticks -ne $submitted.submittedAtUtc.Ticks) {
        throw "Pre-order submission was not idempotent."
    }
    Write-Output "PASS authoritative menu snapshot, pre-order draft and idempotent submit"

    $cancelled = Invoke-Json "Post" "$gateway/api/mobile/v1/reservations/$reservationId/cancel" $null $account1.accessToken
    if ($cancelled.status -ne "Cancelled") { throw "Reservation cancellation failed." }
    Assert-HttpError 409 "reservation_state_conflict" {
        Invoke-Json "Put" "$gateway/api/mobile/v1/reservations/$reservationId" @{ tableId=$table2; partySize=4; reservedAt=$newAt } $account1.accessToken "update-after-cancel"
    }
    Write-Output "PASS cancellation and terminal-state mutation guard"
    Write-Output "B3 runtime checks passed."
}
finally {
    if (-not $KeepTestData -and (docker inspect selfrestaurant-db 2>$null)) {
        $sql = "SET QUOTED_IDENTIFIER ON; USE RESTAURANT_ORDERS; DELETE FROM dbo.ReservationPreOrders WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username IN ('$username1','$username2')); USE RESTAURANT_CUSTOMERS; DELETE FROM dbo.Reservations WHERE CustomerId IN (SELECT CustomerID FROM RESTAURANT_IDENTITY.dbo.Customers WHERE Username IN ('$username1','$username2')); USE RESTAURANT_IDENTITY; DELETE r FROM dbo.MobileRefreshTokens r JOIN dbo.Customers c ON c.CustomerID=r.CustomerId WHERE c.Username IN ('$username1','$username2'); DELETE p FROM dbo.PasswordResetTokens p JOIN dbo.Customers c ON c.CustomerID=p.CustomerID WHERE c.Username IN ('$username1','$username2'); DELETE FROM dbo.CustomerSettings WHERE CustomerId IN (SELECT CustomerID FROM dbo.Customers WHERE Username IN ('$username1','$username2')); DELETE FROM dbo.Customers WHERE Username IN ('$username1','$username2');"
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sql))
        $command = "echo $encoded | base64 -d | /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -C"
        docker exec selfrestaurant-db /bin/bash -lc $command 2>$null | Out-Null
    }
}
