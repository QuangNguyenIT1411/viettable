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
$logPath = Join-Path $logDir "chef_customer_flow_test_$timestamp.log"

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
        if ($null -ne $prop) {
            return $prop.Value
        }
    }

    return $null
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
        if ($m.Success) {
            return $m.Groups[1].Value
        }
    }

    throw "Không tìm thấy __RequestVerificationToken trong HTML."
}

function Convert-ResponseToJson {
    param([Parameter(Mandatory = $true)]$Response)

    $content = if ($null -eq $Response.Content) { "" } else { [string]$Response.Content }
    $content = $content.Trim()

    if ([string]::IsNullOrWhiteSpace($content)) {
        throw "Response rong, khong the parse JSON."
    }

    $candidates = @($content)
    $start = $content.IndexOf("{")
    $end = $content.LastIndexOf("}")
    if ($start -ge 0 -and $end -gt $start) {
        $slice = $content.Substring($start, $end - $start + 1)
        if ($slice -ne $content) {
            $candidates += $slice
        }
    }

    foreach ($candidate in $candidates) {
        try {
            return ($candidate | ConvertFrom-Json -Depth 20)
        }
        catch {
            continue
        }
    }

    try {
        Add-Type -AssemblyName System.Web.Extensions -ErrorAction SilentlyContinue
        $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
        $obj = $serializer.DeserializeObject($content)
        return (($obj | ConvertTo-Json -Depth 20) | ConvertFrom-Json -Depth 20)
    }
    catch {
        $fallback = [ordered]@{
            success = [bool]([regex]::IsMatch($content, '"success"\s*:\s*true', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase))
            message = $null
            orderId = $null
            statusCode = $null
            raw = $content
        }

        $m1 = [regex]::Match($content, '"message"\s*:\s*"([^"]*)"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($m1.Success) { $fallback.message = $m1.Groups[1].Value }

        $m2 = [regex]::Match($content, '"orderId"\s*:\s*(\d+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($m2.Success) { $fallback.orderId = [int]$m2.Groups[1].Value }

        $m3 = [regex]::Match($content, '"statusCode"\s*:\s*"([^"]+)"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($m3.Success) { $fallback.statusCode = $m3.Groups[1].Value }

        return [pscustomobject]$fallback
    }
}

function Get-OrderStatusCodeFromPayload {
    param([Parameter(Mandatory = $true)][object]$Payload)

    $orderObj = Get-PropValue -Object $Payload -CandidateNames @("order")
    if ($null -ne $orderObj) {
        $statusCode = Get-PropValue -Object $orderObj -CandidateNames @("StatusCode", "statusCode")
        if ($null -ne $statusCode) {
            return "$statusCode".Trim().ToUpperInvariant()
        }
    }

    $statusCode = Get-PropValue -Object $Payload -CandidateNames @("StatusCode", "statusCode")
    if ($null -ne $statusCode) {
        return "$statusCode".Trim().ToUpperInvariant()
    }

    $raw = Get-PropValue -Object $Payload -CandidateNames @("raw")
    if ($null -ne $raw) {
        $m = [regex]::Match([string]$raw, '"statusCode"\s*:\s*"([^"]+)"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($m.Success) {
            return $m.Groups[1].Value.Trim().ToUpperInvariant()
        }
    }

    return $null
}

function Start-OldIisExpress {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$SitePath,
        [Parameter(Mandatory = $true)][string]$IisExe
    )

    if (!(Test-Path $IisExe)) {
        throw "Không tìm thấy IIS Express tại $IisExe"
    }

    if (!(Test-Path $SitePath)) {
        throw "Không tìm thấy site bản cũ tại $SitePath"
    }

    Write-Log "Khởi động IIS Express cho bản cũ..."
    Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force

    $uri = [Uri]$BaseUrl
    $port = $uri.Port
    $proc = Start-Process -FilePath $IisExe -ArgumentList "/path:$SitePath", "/port:$port" -PassThru

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        try {
            $r = Invoke-WebRequest -Uri "$BaseUrl/" -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 500) {
                Write-Log "IIS Express đã chạy tại $BaseUrl (PID=$($proc.Id))."
                return $proc
            }
        }
        catch {
            continue
        }
    }

    throw "Không thể khởi động bản cũ tại $BaseUrl"
}

function Login-CustomerLegacy {
    param([Parameter(Mandatory = $true)][string]$BaseUrl)

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/Customer/Login" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html $loginPage.Content

    $resp = Invoke-WebRequest `
        -Uri "$BaseUrl/Customer/Login" `
        -Method Post `
        -WebSession $session `
        -UseBasicParsing `
        -Headers @{ "X-Requested-With" = "XMLHttpRequest" } `
        -Body @{
            "__RequestVerificationToken" = $token
            "username" = "lan.nguyen"
            "password" = "123456"
            "rememberMe" = "false"
            "returnUrl" = ""
        }

    $json = Convert-ResponseToJson -Response $resp
    if (-not $json.success) {
        throw "Đăng nhập khách (cũ) thất bại: $($json.message)"
    }

    return $session
}

function Login-CustomerNew {
    param([Parameter(Mandatory = $true)][string]$BaseUrl)

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/Customer/Login?mode=login" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html $loginPage.Content

    $resp = Invoke-WebRequest `
        -Uri "$BaseUrl/Customer/Login" `
        -Method Post `
        -WebSession $session `
        -UseBasicParsing `
        -Headers @{ "X-Requested-With" = "XMLHttpRequest" } `
        -Body @{
            "__RequestVerificationToken" = $token
            "mode" = "login"
            "Login.Username" = "lan.nguyen"
            "Login.Password" = "123456"
            "Login.ReturnUrl" = ""
        }

    $json = Convert-ResponseToJson -Response $resp
    if (-not $json.success) {
        throw "Đăng nhập khách (mới) thất bại: $($json.message)"
    }

    return $session
}

function Login-ChefLegacy {
    param([Parameter(Mandatory = $true)][string]$BaseUrl)

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/LogIn" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html $loginPage.Content

    $resp = Invoke-WebRequest `
        -Uri "$BaseUrl/Staff/Account/LogIn" `
        -Method Post `
        -WebSession $session `
        -UseBasicParsing `
        -Headers @{ "X-Requested-With" = "XMLHttpRequest" } `
        -Body @{
            "__RequestVerificationToken" = $token
            "username" = "chef_hung"
            "password" = "123456"
            "rememberMe" = "false"
        }

    $json = Convert-ResponseToJson -Response $resp
    if (-not $json.success) {
        throw "Đăng nhập chef (cũ) thất bại: $($json.message)"
    }

    return $session
}

function Login-ChefNew {
    param([Parameter(Mandatory = $true)][string]$BaseUrl)

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/Login" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html $loginPage.Content

    $resp = Invoke-WebRequest `
        -Uri "$BaseUrl/Staff/Account/Login" `
        -Method Post `
        -WebSession $session `
        -UseBasicParsing `
        -Headers @{ "X-Requested-With" = "XMLHttpRequest" } `
        -Body @{
            "__RequestVerificationToken" = $token
            "username" = "chef_hung"
            "password" = "123456"
            "rememberMe" = "false"
        }

    $json = Convert-ResponseToJson -Response $resp
    if (-not $json.success) {
        throw "Đăng nhập chef (mới) thất bại: $($json.message)"
    }

    return $session
}

function Get-TableForTest {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)]$CustomerSession,
        [Parameter(Mandatory = $true)][int]$BranchId
    )

    $resp = Invoke-WebRequest -Uri "$BaseUrl/Home/GetBranchTables?branchId=$BranchId" -WebSession $CustomerSession -UseBasicParsing
    $json = Convert-ResponseToJson -Response $resp

    if (-not $json.success) {
        throw "Không lấy được danh sách bàn: $($json.message)"
    }

    $tables = @()
    $parsedTables = Get-PropValue -Object $json -CandidateNames @("tables", "Tables")
    if ($null -ne $parsedTables) {
        $tables = @($parsedTables)
    }

    if ($tables.Count -eq 0) {
        $raw = Get-PropValue -Object $json -CandidateNames @("raw")
        if (-not [string]::IsNullOrWhiteSpace([string]$raw)) {
            $availableMatch = [regex]::Match([string]$raw, '"TableID"\s*:\s*(\d+)[^{}]*?"IsAvailable"\s*:\s*true', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if ($availableMatch.Success) {
                return [int]$availableMatch.Groups[1].Value
            }

            $anyMatch = [regex]::Match([string]$raw, '"TableID"\s*:\s*(\d+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if ($anyMatch.Success) {
                return [int]$anyMatch.Groups[1].Value
            }
        }
    }

    if ($tables.Count -eq 0) {
        throw "Chi nhánh không có bàn để test."
    }

    $available = $tables | Where-Object {
        $flag = Get-PropValue -Object $_ -CandidateNames @("IsAvailable", "isAvailable")
        [bool]$flag
    } | Select-Object -First 1

    $chosen = if ($null -ne $available) { $available } else { $tables[0] }
    $tableId = Get-PropValue -Object $chosen -CandidateNames @("TableID", "tableId")
    if ($null -eq $tableId -or [int]$tableId -le 0) {
        throw "Không đọc được TableID từ payload."
    }

    return [int]$tableId
}

function Get-BranchTableIds {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)]$CustomerSession,
        [Parameter(Mandatory = $true)][int]$BranchId
    )

    $resp = Invoke-WebRequest -Uri "$BaseUrl/Home/GetBranchTables?branchId=$BranchId" -WebSession $CustomerSession -UseBasicParsing
    $json = Convert-ResponseToJson -Response $resp
    Assert-JsonSuccess -Json $json -Context "GetBranchTables"

    $ids = New-Object System.Collections.Generic.List[int]

    $parsedTables = Get-PropValue -Object $json -CandidateNames @("tables", "Tables")
    if ($null -ne $parsedTables) {
        foreach ($t in @($parsedTables)) {
            $id = Get-PropValue -Object $t -CandidateNames @("TableID", "tableId")
            if ($null -ne $id -and [int]$id -gt 0 -and -not $ids.Contains([int]$id)) {
                $ids.Add([int]$id)
            }
        }
    }

    if ($ids.Count -eq 0) {
        $raw = Get-PropValue -Object $json -CandidateNames @("raw")
        if (-not [string]::IsNullOrWhiteSpace([string]$raw)) {
            $matches = [regex]::Matches([string]$raw, '"TableID"\s*:\s*(\d+)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            foreach ($m in $matches) {
                $id = [int]$m.Groups[1].Value
                if ($id -gt 0 -and -not $ids.Contains($id)) {
                    $ids.Add($id)
                }
            }
        }
    }

    return @($ids.ToArray())
}

function Get-StatusCodeFromOrderItemsPayload {
    param([Parameter(Mandatory = $true)][object]$Payload)

    $summary = Get-PropValue -Object $Payload -CandidateNames @("summary", "Summary")
    if ($null -ne $summary) {
        $status = Get-PropValue -Object $summary -CandidateNames @("statusCode", "StatusCode")
        if ($null -ne $status) {
            return "$status".Trim().ToUpperInvariant()
        }
    }

    $raw = Get-PropValue -Object $Payload -CandidateNames @("raw")
    if ($null -ne $raw) {
        $m = [regex]::Match([string]$raw, '"statusCode"\s*:\s*"([^"]+)"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($m.Success) {
            return $m.Groups[1].Value.Trim().ToUpperInvariant()
        }
    }

    return $null
}

function Assert-JsonSuccess {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $ok = Get-PropValue -Object $Json -CandidateNames @("success", "Success")
    if (-not [bool]$ok) {
        $msg = Get-PropValue -Object $Json -CandidateNames @("message", "Message")
        throw "$Context thất bại: $msg"
    }
}

function Test-LegacyFlow {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][int]$BranchId,
        [Parameter(Mandatory = $true)][int]$DishId
    )

    Write-Log "[OLD] Đăng nhập khách hàng..."
    $customerSession = Login-CustomerLegacy -BaseUrl $BaseUrl

    $candidateTableIds = Get-BranchTableIds -BaseUrl $BaseUrl -CustomerSession $customerSession -BranchId $BranchId
    if ($candidateTableIds.Count -eq 0) {
        throw "[OLD] Không tìm thấy bàn để test."
    }

    $tableId = $null
    foreach ($candidateId in $candidateTableIds) {
        try {
            Invoke-WebRequest `
                -Uri "$BaseUrl/Menu/ResetTable" `
                -Method Post `
                -WebSession $customerSession `
                -UseBasicParsing `
                -Body @{
                    tableId = $candidateId
                    branchId = $BranchId
                } | Out-Null

            $orderItemsResp = Invoke-WebRequest -Uri "$BaseUrl/Order/GetOrderItems?tableId=$candidateId" -Method Get -WebSession $customerSession -UseBasicParsing
            $orderItemsJson = Convert-ResponseToJson -Response $orderItemsResp
            $hasOrder = [bool](Get-PropValue -Object $orderItemsJson -CandidateNames @("success", "Success"))
            if (-not $hasOrder) {
                $tableId = [int]$candidateId
                break
            }

            $statusCode = Get-StatusCodeFromOrderItemsPayload -Payload $orderItemsJson
            if ([string]::IsNullOrWhiteSpace($statusCode) -or $statusCode -eq "PENDING") {
                $tableId = [int]$candidateId
                break
            }
        }
        catch {
            continue
        }
    }

    if ($null -eq $tableId) {
        throw "[OLD] Không tìm được bàn trống/pending để test."
    }

    Write-Log "[OLD] Dùng bàn ID=$tableId"

    $addResp = Invoke-WebRequest `
        -Uri "$BaseUrl/Order/AddItem" `
        -Method Post `
        -WebSession $customerSession `
        -UseBasicParsing `
        -Body @{
            tableId = $tableId
            dishId = $DishId
            quantity = 2
            note = "AUTO_OLD_CHEF_FLOW_$timestamp"
        }
    $addJson = Convert-ResponseToJson -Response $addResp
    Assert-JsonSuccess -Json $addJson -Context "[OLD] AddItem"

    $orderId = [int](Get-PropValue -Object $addJson -CandidateNames @("orderId", "OrderId"))
    if ($orderId -le 0) {
        throw "[OLD] Không lấy được orderId sau AddItem."
    }
    Write-Log "[OLD] Tạo orderId=$orderId"

    $submitResp = Invoke-WebRequest `
        -Uri "$BaseUrl/Order/SendToKitchen" `
        -Method Post `
        -WebSession $customerSession `
        -UseBasicParsing `
        -Body @{ tableId = $tableId }
    $submitJson = Convert-ResponseToJson -Response $submitResp
    Assert-JsonSuccess -Json $submitJson -Context "[OLD] SendToKitchen"

    Write-Log "[OLD] Đăng nhập chef..."
    $chefSession = Login-ChefLegacy -BaseUrl $BaseUrl

    $chefIndex = Invoke-WebRequest -Uri "$BaseUrl/Staff/Chef/Index" -WebSession $chefSession -UseBasicParsing
    $chefPageContainsOrder = $chefIndex.Content -match [regex]::Escape("id=""order-$orderId""")

    $toPreparingResp = Invoke-WebRequest `
        -Uri "$BaseUrl/Staff/Chef/UpdateOrderStatus" `
        -Method Post `
        -WebSession $chefSession `
        -UseBasicParsing `
        -Body @{
            orderId = $orderId
            statusCode = "PREPARING"
        }
    $toPreparingJson = Convert-ResponseToJson -Response $toPreparingResp
    Assert-JsonSuccess -Json $toPreparingJson -Context "[OLD] Chef Update PREPARING"

    $statusAfterPreparingResp = Invoke-WebRequest `
        -Uri "$BaseUrl/Home/GetOrderStatus?orderId=$orderId" `
        -Method Get `
        -WebSession $customerSession `
        -UseBasicParsing
    $statusAfterPreparingJson = Convert-ResponseToJson -Response $statusAfterPreparingResp
    Assert-JsonSuccess -Json $statusAfterPreparingJson -Context "[OLD] Customer GetOrderStatus sau PREPARING"
    $statusAfterPreparing = Get-OrderStatusCodeFromPayload -Payload $statusAfterPreparingJson

    $toReadyResp = Invoke-WebRequest `
        -Uri "$BaseUrl/Staff/Chef/UpdateOrderStatus" `
        -Method Post `
        -WebSession $chefSession `
        -UseBasicParsing `
        -Body @{
            orderId = $orderId
            statusCode = "READY"
        }
    $toReadyJson = Convert-ResponseToJson -Response $toReadyResp
    Assert-JsonSuccess -Json $toReadyJson -Context "[OLD] Chef Update READY"

    $statusAfterReadyResp = Invoke-WebRequest `
        -Uri "$BaseUrl/Home/GetOrderStatus?orderId=$orderId" `
        -Method Get `
        -WebSession $customerSession `
        -UseBasicParsing
    $statusAfterReadyJson = Convert-ResponseToJson -Response $statusAfterReadyResp
    Assert-JsonSuccess -Json $statusAfterReadyJson -Context "[OLD] Customer GetOrderStatus sau READY"
    $statusAfterReady = Get-OrderStatusCodeFromPayload -Payload $statusAfterReadyJson

    return [ordered]@{
        Environment = "OLD_MVC"
        TableId = $tableId
        OrderId = $orderId
        ChefPageContainsOrder = [bool]$chefPageContainsOrder
        StatusAfterPreparing = $statusAfterPreparing
        StatusAfterReady = $statusAfterReady
        Pass = ([bool]$chefPageContainsOrder -and $statusAfterPreparing -eq "PREPARING" -and $statusAfterReady -eq "READY")
    }
}

function Test-NewFlow {
    param(
        [Parameter(Mandatory = $true)][string]$GatewayBaseUrl,
        [Parameter(Mandatory = $true)][string]$OrdersBaseUrl,
        [Parameter(Mandatory = $true)][int]$BranchId,
        [Parameter(Mandatory = $true)][int]$DishId
    )

    Write-Log "[NEW] Đăng nhập khách hàng..."
    $customerSession = Login-CustomerNew -BaseUrl $GatewayBaseUrl

    $tableId = Get-TableForTest -BaseUrl $GatewayBaseUrl -CustomerSession $customerSession -BranchId $BranchId
    Write-Log "[NEW] Dùng bàn ID=$tableId"

    Invoke-WebRequest -Uri "$OrdersBaseUrl/api/tables/$tableId/reset" -Method Post -UseBasicParsing | Out-Null

    $addResp = Invoke-WebRequest `
        -Uri "$OrdersBaseUrl/api/tables/$tableId/order/items" `
        -Method Post `
        -UseBasicParsing `
        -ContentType "application/json" `
        -Body (@{
            dishId = $DishId
            quantity = 2
            note = "AUTO_NEW_CHEF_FLOW_$timestamp"
        } | ConvertTo-Json -Depth 5)
    $addJson = Convert-ResponseToJson -Response $addResp
    $orderId = [int](Get-PropValue -Object $addJson -CandidateNames @("orderId", "OrderId"))
    if ($orderId -le 0) {
        throw "[NEW] Không lấy được orderId sau AddItem."
    }
    Write-Log "[NEW] Tạo orderId=$orderId"

    $submitResp = Invoke-WebRequest `
        -Uri "$GatewayBaseUrl/Order/SendToKitchen" `
        -Method Post `
        -WebSession $customerSession `
        -UseBasicParsing `
        -ContentType "application/json" `
        -Body (@{ tableId = $tableId } | ConvertTo-Json)
    $submitJson = Convert-ResponseToJson -Response $submitResp
    Assert-JsonSuccess -Json $submitJson -Context "[NEW] SendToKitchen"

    Write-Log "[NEW] Đăng nhập chef..."
    $chefSession = Login-ChefNew -BaseUrl $GatewayBaseUrl

    $chefIndex = Invoke-WebRequest -Uri "$GatewayBaseUrl/Staff/Chef/Index" -WebSession $chefSession -UseBasicParsing
    $chefPageContainsOrder = $chefIndex.Content -match [regex]::Escape("id=""order-$orderId""")

    $toPreparingResp = Invoke-WebRequest `
        -Uri "$GatewayBaseUrl/Staff/Chef/UpdateOrderStatus" `
        -Method Post `
        -WebSession $chefSession `
        -UseBasicParsing `
        -Body @{
            orderId = $orderId
            statusCode = "PREPARING"
        }
    $toPreparingJson = Convert-ResponseToJson -Response $toPreparingResp
    Assert-JsonSuccess -Json $toPreparingJson -Context "[NEW] Chef Update PREPARING"

    $statusAfterPreparingResp = Invoke-WebRequest `
        -Uri "$GatewayBaseUrl/Home/GetOrderStatus?orderId=$orderId" `
        -Method Get `
        -WebSession $customerSession `
        -UseBasicParsing
    $statusAfterPreparingJson = Convert-ResponseToJson -Response $statusAfterPreparingResp
    Assert-JsonSuccess -Json $statusAfterPreparingJson -Context "[NEW] Customer GetOrderStatus sau PREPARING"
    $statusAfterPreparing = Get-OrderStatusCodeFromPayload -Payload $statusAfterPreparingJson

    $toReadyResp = Invoke-WebRequest `
        -Uri "$GatewayBaseUrl/Staff/Chef/UpdateOrderStatus" `
        -Method Post `
        -WebSession $chefSession `
        -UseBasicParsing `
        -Body @{
            orderId = $orderId
            statusCode = "READY"
        }
    $toReadyJson = Convert-ResponseToJson -Response $toReadyResp
    Assert-JsonSuccess -Json $toReadyJson -Context "[NEW] Chef Update READY"

    $statusAfterReadyResp = Invoke-WebRequest `
        -Uri "$GatewayBaseUrl/Home/GetOrderStatus?orderId=$orderId" `
        -Method Get `
        -WebSession $customerSession `
        -UseBasicParsing
    $statusAfterReadyJson = Convert-ResponseToJson -Response $statusAfterReadyResp
    Assert-JsonSuccess -Json $statusAfterReadyJson -Context "[NEW] Customer GetOrderStatus sau READY"
    $statusAfterReady = Get-OrderStatusCodeFromPayload -Payload $statusAfterReadyJson

    # Customer confirms dish received (READY -> SERVING), then order should disappear from chef READY board
    $menuPage = Invoke-WebRequest -Uri "$GatewayBaseUrl/Menu?tableId=$tableId&BranchId=$BranchId&tableNumber=$tableId" -WebSession $customerSession -UseBasicParsing
    $antiForgeryToken = Get-AntiForgeryToken -Html $menuPage.Content

    $confirmResp = Invoke-WebRequest `
        -Uri "$GatewayBaseUrl/Home/ConfirmOrderReceived" `
        -Method Post `
        -WebSession $customerSession `
        -UseBasicParsing `
        -Headers @{ "X-Requested-With" = "XMLHttpRequest" } `
        -Body @{
            "__RequestVerificationToken" = $antiForgeryToken
            "orderId" = $orderId
        }
    $confirmJson = Convert-ResponseToJson -Response $confirmResp
    Assert-JsonSuccess -Json $confirmJson -Context "[NEW] Customer ConfirmOrderReceived"

    Start-Sleep -Milliseconds 800
    $chefIndexAfterConfirm = Invoke-WebRequest -Uri "$GatewayBaseUrl/Staff/Chef/Index" -WebSession $chefSession -UseBasicParsing
    $chefPageHiddenAfterConfirm = -not ($chefIndexAfterConfirm.Content -match [regex]::Escape("id=""order-$orderId"""))

    return [ordered]@{
        Environment = "NEW_MICROSERVICE"
        TableId = $tableId
        OrderId = $orderId
        ChefPageContainsOrder = [bool]$chefPageContainsOrder
        StatusAfterPreparing = $statusAfterPreparing
        StatusAfterReady = $statusAfterReady
        CustomerConfirmedReceived = $true
        ChefPageHiddenAfterConfirm = [bool]$chefPageHiddenAfterConfirm
        Pass = ([bool]$chefPageContainsOrder -and $statusAfterPreparing -eq "PREPARING" -and $statusAfterReady -eq "READY" -and [bool]$chefPageHiddenAfterConfirm)
    }
}

$iisProcess = $null

try {
    Write-Log "=== BẮT ĐẦU TEST LUỒNG CHEF <-> KHÁCH HÀNG ==="
    $oldResult = $null
    if (-not $SkipOld) {
        $iisProcess = Start-OldIisExpress -BaseUrl $OldBaseUrl -SitePath $oldSitePath -IisExe $iisExpressExe
        $oldResult = Test-LegacyFlow -BaseUrl $OldBaseUrl -BranchId $BranchId -DishId $DishId
        Write-Log ("[OLD] Result: " + ($oldResult | ConvertTo-Json -Compress))
    }
    else {
        Write-Log "[OLD] Bỏ qua theo cờ -SkipOld"
    }

    $newResult = Test-NewFlow -GatewayBaseUrl $NewBaseUrl -OrdersBaseUrl $OrdersApiBaseUrl -BranchId $BranchId -DishId $DishId
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

    $summaryPath = Join-Path $logDir "chef_customer_flow_summary_$timestamp.json"
    $summary | ConvertTo-Json -Depth 10 | Set-Content -Path $summaryPath -Encoding UTF8

    Write-Log ("Tóm tắt đã lưu: $summaryPath")
    Write-Log ("Kết quả chung: BothPass=$($summary.BothPass)")
}
catch {
    Write-Log ("TEST FAILED: " + $_.Exception.Message)
    throw
}
finally {
    if ($null -ne $iisProcess -and -not $KeepOldIis) {
        Write-Log "Dừng IIS Express bản cũ..."
        try {
            Stop-Process -Id $iisProcess.Id -Force -ErrorAction SilentlyContinue
        }
        catch {}
    }
}
