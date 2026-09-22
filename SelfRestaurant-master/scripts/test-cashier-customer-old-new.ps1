param(
    [string]$OldBaseUrl = "http://localhost:5088",
    [string]$NewBaseUrl = "http://localhost:5100",
    [string]$OrdersApiBaseUrl = "http://localhost:5102",
    [int]$BranchId = 1,
    [int]$DishId = 1,
    [switch]$KeepOldIis,
    [switch]$SkipOld
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$siblingRoot = Split-Path -Parent $repoRoot
$oldSitePath = Join-Path $siblingRoot "SelfRestaurant-main_OLD\SelfRestaurant"
$iisExpressExe = "C:\Program Files\IIS Express\iisexpress.exe"

$logDir = Join-Path $repoRoot ".runlogs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logPath = Join-Path $logDir "cashier_customer_flow_test_$timestamp.log"

function Write-Log {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Write-Host $line
    Add-Content -Path $logPath -Value $line
}

function Get-PropValue {
    param(
        [Parameter(Mandatory = $true)][object]$Object,
        [Parameter(Mandatory = $true)][string[]]$CandidateNames
    )
    foreach ($name in $CandidateNames) {
        $prop = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $name } | Select-Object -First 1
        if ($null -ne $prop) { return $prop.Value }
    }
    return $null
}

function Convert-ResponseToJson {
    param([Parameter(Mandatory = $true)]$Response)

    $content = if ($null -eq $Response.Content) { "" } else { [string]$Response.Content }
    $content = $content.Trim()
    if ([string]::IsNullOrWhiteSpace($content)) {
        return [pscustomobject]@{ success = $false; message = "Empty response"; raw = "" }
    }

    $candidates = @($content)
    $start = $content.IndexOf("{")
    $end = $content.LastIndexOf("}")
    if ($start -ge 0 -and $end -gt $start) {
        $slice = $content.Substring($start, $end - $start + 1)
        if ($slice -ne $content) { $candidates += $slice }
    }

    foreach ($candidate in $candidates) {
        try { return ($candidate | ConvertFrom-Json -Depth 30) } catch { continue }
    }

    $fallback = [ordered]@{
        success = [bool]([regex]::IsMatch($content, '"success"\s*:\s*true', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase))
        message = $null
        raw = $content
    }

    $m = [regex]::Match($content, '"message"\s*:\s*"([^"]*)"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($m.Success) { $fallback.message = $m.Groups[1].Value }

    return [pscustomobject]$fallback
}

function Assert-JsonSuccess {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [Parameter(Mandatory = $true)][string]$Context
    )
    $ok = Get-PropValue -Object $Json -CandidateNames @("success", "Success")
    if (-not [bool]$ok) {
        $msg = Get-PropValue -Object $Json -CandidateNames @("message", "Message", "raw")
        throw "$Context failed: $msg"
    }
}

function Parse-IntText {
    param([Parameter(Mandatory = $true)][string]$Text)
    $digits = [regex]::Replace($Text, "[^\d]", "")
    if ([string]::IsNullOrWhiteSpace($digits)) { return 0 }
    return [int]$digits
}

function Parse-DecimalText {
    param([Parameter(Mandatory = $true)][string]$Text)
    $clean = ($Text -replace "[^\d\.,-]", "").Trim()
    if ([string]::IsNullOrWhiteSpace($clean)) { return [decimal]0 }
    $normalized = $clean.Replace(",", ".")
    try {
        return [decimal]::Parse($normalized, [System.Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        return [decimal]0
    }
}

function Get-AntiForgeryToken {
    param([Parameter(Mandatory = $true)][string]$Html)
    $patterns = @(
        'name="__RequestVerificationToken"[^>]*value="([^"]+)"',
        "name='__RequestVerificationToken'[^>]*value='([^']+)'",
        'value="([^"]+)"[^>]*name="__RequestVerificationToken"'
    )
    foreach ($pattern in $patterns) {
        $m = [regex]::Match($Html, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($m.Success) { return $m.Groups[1].Value }
    }
    throw "Missing anti-forgery token."
}

function Start-OldIisExpress {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$SitePath,
        [Parameter(Mandatory = $true)][string]$IisExe
    )
    if (!(Test-Path $IisExe)) { throw "IIS Express not found: $IisExe" }
    if (!(Test-Path $SitePath)) { throw "Old site not found: $SitePath" }

    Write-Log "Starting old IIS Express..."
    Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force

    $uri = [Uri]$BaseUrl
    $proc = Start-Process -FilePath $IisExe -ArgumentList "/path:$SitePath", "/port:$($uri.Port)" -PassThru

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 800
        try {
            $r = Invoke-WebRequest -Uri "$BaseUrl/" -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 500) {
                Write-Log "Old IIS is up at $BaseUrl (PID=$($proc.Id))."
                return $proc
            }
        } catch { continue }
    }

    throw "Cannot start old IIS at $BaseUrl"
}

function Login-CustomerOld {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$Password
    )
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page = Invoke-WebRequest -Uri "$BaseUrl/Customer/Login" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html $page.Content
    $resp = Invoke-WebRequest -Uri "$BaseUrl/Customer/Login" -Method Post -WebSession $session -UseBasicParsing -Headers @{ "X-Requested-With" = "XMLHttpRequest" } -Body @{
        "__RequestVerificationToken" = $token
        "username" = $Username
        "password" = $Password
        "rememberMe" = "false"
        "returnUrl" = ""
    }
    $json = Convert-ResponseToJson -Response $resp
    Assert-JsonSuccess -Json $json -Context "[OLD] customer login"
    return $session
}

function Login-CustomerNew {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$Password
    )
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page = Invoke-WebRequest -Uri "$BaseUrl/Customer/Login?mode=login" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html $page.Content
    $resp = Invoke-WebRequest -Uri "$BaseUrl/Customer/Login" -Method Post -WebSession $session -UseBasicParsing -Headers @{ "X-Requested-With" = "XMLHttpRequest" } -Body @{
        "__RequestVerificationToken" = $token
        "mode" = "login"
        "Login.Username" = $Username
        "Login.Password" = $Password
        "Login.ReturnUrl" = ""
    }
    $json = Convert-ResponseToJson -Response $resp
    Assert-JsonSuccess -Json $json -Context "[NEW] customer login"
    return $session
}

function Login-CashierOld {
    param([Parameter(Mandatory = $true)][string]$BaseUrl)
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/LogIn" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html $page.Content
    $resp = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/LogIn" -Method Post -WebSession $session -UseBasicParsing -Headers @{ "X-Requested-With" = "XMLHttpRequest" } -Body @{
        "__RequestVerificationToken" = $token
        "username" = "cashier_lan"
        "password" = "123456"
        "rememberMe" = "false"
    }
    $json = Convert-ResponseToJson -Response $resp
    Assert-JsonSuccess -Json $json -Context "[OLD] cashier login"
    return $session
}

function Login-CashierNew {
    param([Parameter(Mandatory = $true)][string]$BaseUrl)
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/Login" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html $page.Content
    $resp = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/Login" -Method Post -WebSession $session -UseBasicParsing -Headers @{ "X-Requested-With" = "XMLHttpRequest" } -Body @{
        "__RequestVerificationToken" = $token
        "username" = "cashier_lan"
        "password" = "123456"
        "rememberMe" = "false"
    }
    $json = Convert-ResponseToJson -Response $resp
    Assert-JsonSuccess -Json $json -Context "[NEW] cashier login"
    return $session
}

function Get-CustomerPointsOld {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)]$CustomerSession
    )
    $resp = Invoke-WebRequest -Uri "$BaseUrl/Customer/Dashboard" -WebSession $CustomerSession -UseBasicParsing
    $html = [string]$resp.Content

    $m = [regex]::Match($html, 'class="stat-value">\s*([0-9\.,]+)\s*</div>\s*<div class="stat-label">', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if ($m.Success) { return (Parse-IntText -Text $m.Groups[1].Value) }
    throw "[OLD] cannot parse customer points from dashboard"
}

function Get-CustomerPointsNew {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)]$CustomerSession
    )
    $resp = Invoke-WebRequest -Uri "$BaseUrl/Customer/Dashboard" -WebSession $CustomerSession -UseBasicParsing
    $html = [string]$resp.Content
    $m = [regex]::Match($html, 'Points:\s*([0-9\.,]+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $m.Success) {
        $m = [regex]::Match($html, 'Điểm hiện tại:\s*([0-9\.,]+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    }
    if (-not $m.Success) {
        $m = [regex]::Match(
            $html,
            'stat-value[^>]*>\s*([0-9\.,]+)\s*</div>.*?stat-label[^>]*>\s*Điểm Thưởng',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::Singleline)
    }
    if (-not $m.Success) {
        $m = [regex]::Match($html, 'stat-value[^>]*>\s*([0-9\.,]+)\s*</div>', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    }
    if (-not $m.Success) {
        $m = [regex]::Match($html, 'badge[^>]*>\s*[^<]*?([0-9\.,]+)\s*</span>', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    }
    if ($m.Success) { return (Parse-IntText -Text $m.Groups[1].Value) }
    throw "[NEW] cannot parse customer points from dashboard"
}

function Get-BranchTables {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)]$CustomerSession,
        [Parameter(Mandatory = $true)][int]$BranchId
    )
    $resp = Invoke-WebRequest -Uri "$BaseUrl/Home/GetBranchTables?branchId=$BranchId" -WebSession $CustomerSession -UseBasicParsing
    $json = Convert-ResponseToJson -Response $resp
    Assert-JsonSuccess -Json $json -Context "GetBranchTables"
    $tables = Get-PropValue -Object $json -CandidateNames @("tables", "Tables")
    if ($null -ne $tables) {
        return @($tables)
    }

    $raw = [string](Get-PropValue -Object $json -CandidateNames @("raw"))
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return @()
    }

    $availableIds = New-Object System.Collections.Generic.HashSet[int]
    $availMatches = [regex]::Matches(
        $raw,
        '"TableID"\s*:\s*(\d+)[^{}]*?"IsAvailable"\s*:\s*true',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    foreach ($m in $availMatches) {
        [void]$availableIds.Add([int]$m.Groups[1].Value)
    }

    $list = New-Object System.Collections.Generic.List[object]
    $seen = New-Object System.Collections.Generic.HashSet[int]
    $idMatches = [regex]::Matches($raw, '"TableID"\s*:\s*(\d+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    foreach ($m in $idMatches) {
        $id = [int]$m.Groups[1].Value
        if ($id -le 0 -or $seen.Contains($id)) { continue }
        [void]$seen.Add($id)
        $list.Add([pscustomobject]@{
            TableID = $id
            IsAvailable = $availableIds.Contains($id)
        })
    }

    return @($list.ToArray())
}

function Select-TableId {
    param([Parameter(Mandatory = $true)][object[]]$Tables)
    if ($Tables.Count -eq 0) { throw "No table found for test." }
    $available = $Tables | Where-Object {
        $flag = Get-PropValue -Object $_ -CandidateNames @("IsAvailable", "isAvailable")
        [bool]$flag
    } | Select-Object -First 1
    $pick = if ($null -ne $available) { $available } else { $Tables[0] }
    $id = Get-PropValue -Object $pick -CandidateNames @("TableID", "tableId")
    if ($null -eq $id -or [int]$id -le 0) { throw "Invalid TableID." }
    return [int]$id
}

function Get-OrderStatusCodeFromPayload {
    param([Parameter(Mandatory = $true)]$Payload)
    $orderObj = Get-PropValue -Object $Payload -CandidateNames @("order", "Order")
    if ($null -ne $orderObj) {
        $statusCode = Get-PropValue -Object $orderObj -CandidateNames @("StatusCode", "statusCode")
        if ($null -ne $statusCode) { return "$statusCode".Trim().ToUpperInvariant() }
    }
    $statusCode2 = Get-PropValue -Object $Payload -CandidateNames @("statusCode", "StatusCode")
    if ($null -ne $statusCode2) { return "$statusCode2".Trim().ToUpperInvariant() }
    return $null
}

function Get-OrderIdFromPayload {
    param([Parameter(Mandatory = $true)]$Payload)
    $id = Get-PropValue -Object $Payload -CandidateNames @("orderId", "OrderId", "orderID", "OrderID")
    if ($null -ne $id -and [int]$id -gt 0) {
        return [int]$id
    }

    $raw = [string](Get-PropValue -Object $Payload -CandidateNames @("raw"))
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        $m = [regex]::Match($raw, '"orderI[Dd]"\s*:\s*(\d+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($m.Success) {
            return [int]$m.Groups[1].Value
        }
    }

    return 0
}

function Get-SubtotalFromOrderItemsPayload {
    param([Parameter(Mandatory = $true)]$Payload)

    $direct = Get-PropValue -Object $Payload -CandidateNames @("subtotal", "Subtotal")
    if ($null -ne $direct) {
        return [decimal]$direct
    }

    $summary = Get-PropValue -Object $Payload -CandidateNames @("summary", "Summary")
    if ($null -ne $summary) {
        $sub = Get-PropValue -Object $summary -CandidateNames @("subtotal", "Subtotal")
        if ($null -ne $sub) {
            return [decimal]$sub
        }
    }

    $raw = [string](Get-PropValue -Object $Payload -CandidateNames @("raw"))
    if (-not [string]::IsNullOrWhiteSpace($raw)) {
        $m = [regex]::Match($raw, '"subtotal"\s*:\s*([0-9\.,]+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($m.Success) {
            return (Parse-DecimalText -Text $m.Groups[1].Value)
        }
    }

    return [decimal]0
}

function Extract-RegexGroup {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Pattern
    )
    $m = [regex]::Match($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

function Test-OldCashierCustomerFlow {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][int]$BranchId,
        [Parameter(Mandatory = $true)][int]$DishId
    )
    $customerUsername = "lan.nguyen"
    $customerPassword = "123456"
    $customerPhone = "0912345678"
    $note = "AUTO_CASHIER_OLD_$timestamp"

    Write-Log "[OLD] customer login..."
    $customerSession = Login-CustomerOld -BaseUrl $BaseUrl -Username $customerUsername -Password $customerPassword
    $pointsBefore = Get-CustomerPointsOld -BaseUrl $BaseUrl -CustomerSession $customerSession
    Write-Log "[OLD] points before payment: $pointsBefore"

    $tables = @(Get-BranchTables -BaseUrl $BaseUrl -CustomerSession $customerSession -BranchId $BranchId)
    $candidateIds = @()
    foreach ($t in $tables) {
        $tid = Get-PropValue -Object $t -CandidateNames @("TableID", "tableId")
        if ($null -ne $tid -and [int]$tid -gt 0) { $candidateIds += [int]$tid }
    }
    $candidateIds = $candidateIds | Select-Object -Unique
    if ($candidateIds.Count -eq 0) {
        throw "[OLD] no candidate table found"
    }

    $tableId = 0
    $orderId = 0
    foreach ($candidateId in $candidateIds) {
        try {
            $addResp = Invoke-WebRequest -Uri "$BaseUrl/Order/AddItem" -Method Post -WebSession $customerSession -UseBasicParsing -Body @{
                tableId = $candidateId
                dishId = $DishId
                quantity = 2
                note = $note
            }
            $addJson = Convert-ResponseToJson -Response $addResp
            Assert-JsonSuccess -Json $addJson -Context "[OLD] AddItem"

            $candidateOrderId = Get-OrderIdFromPayload -Payload $addJson
            if ($candidateOrderId -le 0) { continue }

            $scanResp = Invoke-WebRequest -Uri "$BaseUrl/Order/ScanLoyaltyCard" -Method Post -WebSession $customerSession -UseBasicParsing -Body @{
                tableId = $candidateId
                phoneNumber = $customerPhone
            }
            $scanJson = Convert-ResponseToJson -Response $scanResp
            Assert-JsonSuccess -Json $scanJson -Context "[OLD] ScanLoyaltyCard"

            $sendResp = Invoke-WebRequest -Uri "$BaseUrl/Order/SendToKitchen" -Method Post -WebSession $customerSession -UseBasicParsing -Body @{ tableId = $candidateId }
            $sendJson = Convert-ResponseToJson -Response $sendResp
            $sendOk = Get-PropValue -Object $sendJson -CandidateNames @("success", "Success")
            if ([bool]$sendOk) {
                $tableId = $candidateId
                $orderId = $candidateOrderId
                break
            }
        }
        catch {
            continue
        }
    }

    if ($orderId -le 0 -or $tableId -le 0) {
        throw "[OLD] cannot create and submit a payable order on any table"
    }
    $subtotal = [decimal]0
    Write-Log "[OLD] table=$tableId order=$orderId"

    Write-Log "[OLD] cashier login..."
    $cashierSession = Login-CashierOld -BaseUrl $BaseUrl

    $paymentAmount = 1000000
    $payResp = Invoke-WebRequest -Uri "$BaseUrl/Staff/Cashier/ProcessPayment" -Method Post -WebSession $cashierSession -UseBasicParsing -ContentType "application/json" -Body (@{
        OrderID = $orderId
        Discount = 0
        PointsUsed = 0
        PaymentMethod = "CASH"
        PaymentAmount = $paymentAmount
    } | ConvertTo-Json)
    $payJson = Convert-ResponseToJson -Response $payResp
    Assert-JsonSuccess -Json $payJson -Context "[OLD] ProcessPayment"

    $statusResp = Invoke-WebRequest -Uri "$BaseUrl/Home/GetOrderStatus?orderId=$orderId" -Method Get -WebSession $customerSession -UseBasicParsing
    $statusRaw = [string]$statusResp.Content
    $statusJson = Convert-ResponseToJson -Response $statusResp
    Assert-JsonSuccess -Json $statusJson -Context "[OLD] GetOrderStatus after payment"
    $statusCode = Get-OrderStatusCodeFromPayload -Payload $statusJson
    if ([string]::IsNullOrWhiteSpace($statusCode)) {
        $statusCode = Extract-RegexGroup -Text $statusRaw -Pattern '"StatusCode"\s*:\s*"([A-Z_]+)"'
        if ([string]::IsNullOrWhiteSpace($statusCode)) {
            $statusCode = Extract-RegexGroup -Text $statusRaw -Pattern '"statusCode"\s*:\s*"([A-Z_]+)"'
        }
    }
    if ($null -ne $statusCode) { $statusCode = "$statusCode".Trim().ToUpperInvariant() }

    $pointsAfter = Get-CustomerPointsOld -BaseUrl $BaseUrl -CustomerSession $customerSession
    Write-Log "[OLD] points after payment: $pointsAfter"

    return [ordered]@{
        Environment = "OLD_MVC"
        TableId = $tableId
        OrderId = $orderId
        Subtotal = $subtotal
        StatusAfterPayment = $statusCode
        PointsBefore = $pointsBefore
        PointsAfter = $pointsAfter
        PointsIncreased = ($pointsAfter -gt $pointsBefore)
        Pass = ($statusCode -eq "COMPLETED" -and $pointsAfter -gt $pointsBefore)
    }
}

function Test-NewCashierCustomerFlow {
    param(
        [Parameter(Mandatory = $true)][string]$GatewayBaseUrl,
        [Parameter(Mandatory = $true)][string]$OrdersBaseUrl,
        [Parameter(Mandatory = $true)][int]$BranchId,
        [Parameter(Mandatory = $true)][int]$DishId
    )
    $customerUsername = "minh.tran"
    $customerPassword = "123456"
    $customerPhone = "0987654321"
    $note = "AUTO_CASHIER_NEW_$timestamp"

    Write-Log "[NEW] customer login..."
    $customerSession = Login-CustomerNew -BaseUrl $GatewayBaseUrl -Username $customerUsername -Password $customerPassword
    $pointsBefore = Get-CustomerPointsNew -BaseUrl $GatewayBaseUrl -CustomerSession $customerSession
    Write-Log "[NEW] points before payment: $pointsBefore"

    $tables = @(Get-BranchTables -BaseUrl $GatewayBaseUrl -CustomerSession $customerSession -BranchId $BranchId)
    $tableId = Select-TableId -Tables $tables
    Write-Log "[NEW] table=$tableId"

    Invoke-WebRequest -Uri "$OrdersBaseUrl/api/tables/$tableId/reset" -Method Post -UseBasicParsing | Out-Null

    $addResp = Invoke-WebRequest -Uri "$OrdersBaseUrl/api/tables/$tableId/order/items" -Method Post -UseBasicParsing -ContentType "application/json" -Body (@{
        dishId = $DishId
        quantity = 2
        note = $note
    } | ConvertTo-Json)
    $addJson = Convert-ResponseToJson -Response $addResp
    $orderId = Get-OrderIdFromPayload -Payload $addJson
    if ($orderId -le 0) { throw "[NEW] missing orderId after AddItem" }

    $subtotal = [decimal]0

    $scanResp = Invoke-WebRequest -Uri "$GatewayBaseUrl/Order/ScanLoyaltyCard" -Method Post -WebSession $customerSession -UseBasicParsing -ContentType "application/json" -Body (@{
        tableId = $tableId
        phoneNumber = $customerPhone
    } | ConvertTo-Json)
    $scanJson = Convert-ResponseToJson -Response $scanResp
    Assert-JsonSuccess -Json $scanJson -Context "[NEW] ScanLoyaltyCard"

    $sendResp = Invoke-WebRequest -Uri "$GatewayBaseUrl/Order/SendToKitchen" -Method Post -WebSession $customerSession -UseBasicParsing -ContentType "application/json" -Body (@{ tableId = $tableId } | ConvertTo-Json)
    $sendJson = Convert-ResponseToJson -Response $sendResp
    Assert-JsonSuccess -Json $sendJson -Context "[NEW] SendToKitchen"

    Write-Log "[NEW] cashier login..."
    $cashierSession = Login-CashierNew -BaseUrl $GatewayBaseUrl

    $paymentAmount = 1000000
    $payResp = Invoke-WebRequest -Uri "$GatewayBaseUrl/Staff/Cashier/ProcessPayment" -Method Post -WebSession $cashierSession -UseBasicParsing -ContentType "application/json" -Body (@{
        OrderID = $orderId
        Discount = 0
        PointsUsed = 0
        PaymentMethod = "CASH"
        PaymentAmount = $paymentAmount
    } | ConvertTo-Json)
    $payRaw = [string]$payResp.Content
    $payJson = Convert-ResponseToJson -Response $payResp
    Assert-JsonSuccess -Json $payJson -Context "[NEW] ProcessPayment"

    $pointsEarned = [int](Get-PropValue -Object $payJson -CandidateNames @("pointsEarned", "PointsEarned"))
    $customerPointsFromPayment = [int](Get-PropValue -Object $payJson -CandidateNames @("customerPoints", "CustomerPoints"))
    if ($pointsEarned -le 0) {
        $pointsEarnedText = Extract-RegexGroup -Text $payRaw -Pattern '"pointsEarned"\s*:\s*(\d+)'
        if ($null -ne $pointsEarnedText) { $pointsEarned = [int]$pointsEarnedText }
    }
    if ($customerPointsFromPayment -le 0) {
        $customerPointsText = Extract-RegexGroup -Text $payRaw -Pattern '"customerPoints"\s*:\s*(\d+)'
        if ($null -ne $customerPointsText) { $customerPointsFromPayment = [int]$customerPointsText }
    }

    $statusResp = Invoke-WebRequest -Uri "$GatewayBaseUrl/Home/GetOrderStatus?orderId=$orderId" -Method Get -WebSession $customerSession -UseBasicParsing
    $statusRaw = [string]$statusResp.Content
    $statusJson = Convert-ResponseToJson -Response $statusResp
    Assert-JsonSuccess -Json $statusJson -Context "[NEW] GetOrderStatus after payment"
    $statusCode = Get-OrderStatusCodeFromPayload -Payload $statusJson
    if ([string]::IsNullOrWhiteSpace($statusCode)) {
        $statusCode = Extract-RegexGroup -Text $statusRaw -Pattern '"StatusCode"\s*:\s*"([A-Z_]+)"'
        if ([string]::IsNullOrWhiteSpace($statusCode)) {
            $statusCode = Extract-RegexGroup -Text $statusRaw -Pattern '"statusCode"\s*:\s*"([A-Z_]+)"'
        }
    }
    if ($null -ne $statusCode) { $statusCode = "$statusCode".Trim().ToUpperInvariant() }

    $pointsAfter = Get-CustomerPointsNew -BaseUrl $GatewayBaseUrl -CustomerSession $customerSession
    Write-Log "[NEW] points after payment: $pointsAfter"

    return [ordered]@{
        Environment = "NEW_MICROSERVICE"
        TableId = $tableId
        OrderId = $orderId
        Subtotal = $subtotal
        StatusAfterPayment = $statusCode
        PointsBefore = $pointsBefore
        PointsAfter = $pointsAfter
        PointsEarnedFromPayment = $pointsEarned
        CustomerPointsFromPayment = $customerPointsFromPayment
        PointsIncreased = ($pointsAfter -gt $pointsBefore)
        Pass = ($statusCode -eq "COMPLETED" -and $pointsAfter -gt $pointsBefore -and $pointsEarned -gt 0)
    }
}

$iisProcess = $null

try {
    Write-Log "=== START CASHIER <-> CUSTOMER FLOW TEST ==="
    $oldResult = $null
    if (-not $SkipOld) {
        $iisProcess = Start-OldIisExpress -BaseUrl $OldBaseUrl -SitePath $oldSitePath -IisExe $iisExpressExe
        $oldResult = Test-OldCashierCustomerFlow -BaseUrl $OldBaseUrl -BranchId $BranchId -DishId $DishId
        Write-Log ("[OLD] Result: " + ($oldResult | ConvertTo-Json -Compress))
    }
    else {
        Write-Log "[OLD] skipped by -SkipOld"
    }

    $newResult = Test-NewCashierCustomerFlow -GatewayBaseUrl $NewBaseUrl -OrdersBaseUrl $OrdersApiBaseUrl -BranchId $BranchId -DishId $DishId
    Write-Log ("[NEW] Result: " + ($newResult | ConvertTo-Json -Compress))

    $bothPass = if ($SkipOld) {
        [bool]$newResult.Pass
    }
    else {
        [bool]$oldResult.Pass -and [bool]$newResult.Pass
    }

    $summary = [ordered]@{
        Old = $oldResult
        New = $newResult
        BothPass = $bothPass
        SkipOld = [bool]$SkipOld
        GeneratedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    }

    $summaryPath = Join-Path $logDir "cashier_customer_flow_summary_$timestamp.json"
    $summary | ConvertTo-Json -Depth 20 | Set-Content -Path $summaryPath -Encoding UTF8

    Write-Log "Summary saved: $summaryPath"
    Write-Log ("Final result: BothPass=$($summary.BothPass)")
}
catch {
    Write-Log ("TEST FAILED: " + $_.Exception.Message)
    throw
}
finally {
    if ($null -ne $iisProcess -and -not $KeepOldIis) {
        Write-Log "Stopping old IIS Express..."
        try { Stop-Process -Id $iisProcess.Id -Force -ErrorAction SilentlyContinue } catch {}
    }
}
