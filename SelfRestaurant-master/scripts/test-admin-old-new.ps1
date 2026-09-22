param(
    [string]$OldBaseUrl = "http://localhost:5088",
    [string]$NewBaseUrl = "http://localhost:5100",
    [string]$AdminUsername = "admin",
    [string]$AdminPassword = "123456",
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
$logPath = Join-Path $logDir "admin_flow_test_$timestamp.log"
$summaryPath = Join-Path $logDir "admin_flow_summary_$timestamp.json"

function Write-Log {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Write-Host $line
    Add-Content -Path $logPath -Value $line
}

function Add-Result {
    param(
        [Parameter(Mandatory = $true)][string]$Env,
        [Parameter(Mandatory = $true)][string]$Feature,
        [Parameter(Mandatory = $true)][bool]$Pass,
        [Parameter(Mandatory = $true)][string]$Detail
    )

    $script:results.Add([pscustomobject]@{
        env = $Env
        feature = $Feature
        pass = $Pass
        detail = $Detail
    }) | Out-Null

    $state = if ($Pass) { "PASS" } else { "FAIL" }
    Write-Log "[$Env][$Feature][$state] $Detail"
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

    throw "Missing anti-forgery token."
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
        if ($slice -ne $content) {
            $candidates += $slice
        }
    }

    foreach ($candidate in $candidates) {
        try {
            return ($candidate | ConvertFrom-Json -Depth 30)
        }
        catch {
            continue
        }
    }

    $fallback = [ordered]@{
        success = [bool]([regex]::IsMatch($content, '"success"\s*:\s*true', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase))
        message = $null
        raw = $content
    }

    $m = [regex]::Match($content, '"message"\s*:\s*"([^"]*)"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($m.Success) {
        $fallback.message = $m.Groups[1].Value
    }

    return [pscustomobject]$fallback
}

function Assert-JsonSuccess {
    param(
        [Parameter(Mandatory = $true)]$Json,
        [Parameter(Mandatory = $true)][string]$Context
    )

    $ok = $false
    if ($null -ne $Json.PSObject.Properties["success"]) {
        $ok = [bool]$Json.success
    }
    elseif ($null -ne $Json.PSObject.Properties["Success"]) {
        $ok = [bool]$Json.Success
    }

    if (-not $ok) {
        $msg = $null
        if ($null -ne $Json.PSObject.Properties["message"]) {
            $msg = [string]$Json.message
        }
        elseif ($null -ne $Json.PSObject.Properties["Message"]) {
            $msg = [string]$Json.Message
        }
        elseif ($null -ne $Json.PSObject.Properties["raw"]) {
            $msg = [string]$Json.raw
        }
        throw "$Context failed: $msg"
    }
}

function Start-OldIisExpress {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$SitePath,
        [Parameter(Mandatory = $true)][string]$IisExe
    )

    if (!(Test-Path $IisExe)) {
        throw "IIS Express not found: $IisExe"
    }

    if (!(Test-Path $SitePath)) {
        throw "Old site not found: $SitePath"
    }

    Write-Log "Starting old IIS Express..."
    Get-Process iisexpress -ErrorAction SilentlyContinue | Stop-Process -Force

    $port = ([Uri]$BaseUrl).Port
    $proc = Start-Process -FilePath $IisExe -ArgumentList "/path:$SitePath", "/port:$port" -PassThru

    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 800
        try {
            $r = Invoke-WebRequest -Uri "$BaseUrl/" -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 500) {
                Write-Log "Old IIS ready: $BaseUrl (PID=$($proc.Id))"
                return $proc
            }
        }
        catch {
            continue
        }
    }

    throw "Cannot start old IIS on $BaseUrl"
}

function Wait-NewGateway {
    param([Parameter(Mandatory = $true)][string]$BaseUrl)

    Write-Log "Checking new gateway: $BaseUrl"
    for ($i = 0; $i -lt 40; $i++) {
        try {
            $r = Invoke-WebRequest -Uri "$BaseUrl/" -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -eq 200) {
                Write-Log "New gateway ready."
                return
            }
        }
        catch {
        }
        Start-Sleep -Milliseconds 700
    }

    throw "New gateway is not reachable at $BaseUrl"
}

function Login-AdminOld {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$Password
    )

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/LogIn" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html $page.Content

    $resp = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/LogIn" -Method Post -WebSession $session -UseBasicParsing -Headers @{ "X-Requested-With" = "XMLHttpRequest" } -Body @{
        "__RequestVerificationToken" = $token
        "username" = $Username
        "password" = $Password
        "rememberMe" = "false"
    }

    $json = Convert-ResponseToJson -Response $resp
    Assert-JsonSuccess -Json $json -Context "old admin login"

    $dash = Invoke-WebRequest -Uri "$BaseUrl/Admin/Dashboard" -WebSession $session -UseBasicParsing
    if ($dash.StatusCode -ne 200) {
        throw "Old admin dashboard not reachable after login"
    }

    return $session
}

function Login-AdminNew {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$Password
    )

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $page = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/Login" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html $page.Content

    $resp = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/Login" -Method Post -WebSession $session -UseBasicParsing -Headers @{ "X-Requested-With" = "XMLHttpRequest" } -Body @{
        "__RequestVerificationToken" = $token
        "username" = $Username
        "password" = $Password
        "rememberMe" = "false"
    }

    $json = Convert-ResponseToJson -Response $resp
    Assert-JsonSuccess -Json $json -Context "new admin login"

    $dash = Invoke-WebRequest -Uri "$BaseUrl/Admin/Dashboard" -WebSession $session -UseBasicParsing
    if ($dash.StatusCode -ne 200) {
        throw "New admin dashboard not reachable after login"
    }

    return $session
}

function Get-InputTag {
    param(
        [Parameter(Mandatory = $true)][string]$Html,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $esc = [regex]::Escape($Name)
    $pattern = '<input[^>]*name=[''"]' + $esc + '[''"][^>]*>'
    $m = [regex]::Match($Html, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($m.Success) { return $m.Value }
    return $null
}

function Get-InputValue {
    param(
        [Parameter(Mandatory = $true)][string]$Html,
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$Default = ""
    )

    $tag = Get-InputTag -Html $Html -Name $Name
    if ([string]::IsNullOrWhiteSpace($tag)) {
        return $Default
    }

    $m = [regex]::Match($tag, 'value=[''"]([^''"]*)[''"]', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($m.Success) {
        return $m.Groups[1].Value
    }

    return $Default
}

function Get-TextAreaValue {
    param(
        [Parameter(Mandatory = $true)][string]$Html,
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$Default = ""
    )

    $esc = [regex]::Escape($Name)
    $pattern = '<textarea[^>]*name=[''"]' + $esc + '[''"][^>]*>(.*?)</textarea>'
    $m = [regex]::Match($Html, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if ($m.Success) {
        return $m.Groups[1].Value.Trim()
    }

    return $Default
}

function Get-CheckBoxChecked {
    param(
        [Parameter(Mandatory = $true)][string]$Html,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $tag = Get-InputTag -Html $Html -Name $Name
    if ([string]::IsNullOrWhiteSpace($tag)) {
        return $false
    }

    if (-not [regex]::IsMatch($tag, '(?i)\bchecked\b')) {
        return $false
    }

    if ([regex]::IsMatch($tag, '(?i)\bchecked\s*=\s*["'']?(false|0)["'']?')) {
        return $false
    }

    return $true
}

function Get-SelectValue {
    param(
        [Parameter(Mandatory = $true)][string]$Html,
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$Default = ""
    )

    $esc = [regex]::Escape($Name)
    $pattern = '<select[^>]*name=[''"]' + $esc + '[''"][^>]*>(.*?)</select>'
    $m = [regex]::Match($Html, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $m.Success) {
        return $Default
    }

    $options = $m.Groups[1].Value
    $optionMatches = [regex]::Matches($options, '<option[^>]*>', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    foreach ($opt in $optionMatches) {
        $tag = [string]$opt.Value
        if ($tag -match '(?i)\bselected\b') {
            if ($tag -match '(?i)\bselected\s*=\s*["'']?(false|0)["'']?') {
                continue
            }

            $v = [regex]::Match($tag, 'value=[''"]?([^''"\s>]*)', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            if ($v.Success -and -not [string]::IsNullOrWhiteSpace($v.Groups[1].Value)) {
                return $v.Groups[1].Value
            }
        }
    }

    $firstNumeric = [regex]::Match($options, '<option[^>]*value=[''"]?(\d+)[''"]?[^>]*>', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($firstNumeric.Success) {
        return $firstNumeric.Groups[1].Value
    }

    return $Default
}

function Get-FirstEditId {
    param(
        [Parameter(Mandatory = $true)][string]$Html,
        [Parameter(Mandatory = $true)][string]$ControllerName
    )

    $esc = [regex]::Escape($ControllerName)
    $patterns = @(
        "/Admin/$esc/Edit/(\d+)",
        "/Admin/$esc/Edit\\?id=(\d+)",
        'asp-action=[''"]Edit[''"][^>]*asp-route-id=[''"](\d+)[''"]',
        'asp-route-id=[''"](\d+)[''"][^>]*asp-action=[''"]Edit[''"]'
    )

    foreach ($pattern in $patterns) {
        $matches = [regex]::Matches($Html, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        foreach ($m in $matches) {
            $id = [int]$m.Groups[1].Value
            if ($id -gt 0) {
                return $id
            }
        }
    }

    $idText = [regex]::Match($Html, "ID:\\s*(\\d+)", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($idText.Success) {
        return [int]$idText.Groups[1].Value
    }

    return 0
}

function Get-FirstDishIdBySearch {
    param(
        [Parameter(Mandatory = $true)][string]$Html,
        [Parameter(Mandatory = $true)][string]$DishName
    )

    $escName = [regex]::Escape($DishName)
    $pattern = "(?is)<tr[^>]*>.*?$escName.*?</tr>"
    $row = [regex]::Match($Html, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if (-not $row.Success) {
        return 0
    }

    $patterns = @(
        "/Admin/Dishes/(?:Edit|Delete)/(\d+)",
        "/Admin/Dishes/(?:Edit|Delete)\\?id=(\d+)",
        'asp-action=[''"](?:Edit|Delete|Deactivate)[''"][^>]*asp-route-id=[''"](\d+)[''"]',
        'asp-route-id=[''"](\d+)[''"][^>]*asp-action=[''"](?:Edit|Delete|Deactivate)[''"]'
    )

    foreach ($pattern in $patterns) {
        $idMatch = [regex]::Match($row.Value, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($idMatch.Success) {
            return [int]$idMatch.Groups[1].Value
        }
    }

    return 0
}

function Set-CheckboxField {
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Body,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][bool]$Checked
    )

    if ($Body.Contains($Name)) {
        $Body.Remove($Name)
    }

    if ($Checked) {
        $Body[$Name] = "true"
    }
}

function Test-CustomerManagement {
    param(
        [Parameter(Mandatory = $true)][string]$Env,
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)]$Session,
        [Parameter(Mandatory = $true)][bool]$IsLegacy
    )

    $feature = "Admin-Customers"

    try {
        $createdCustomer = $true
        $createdUsername = "autocus_{0}_{1}" -f $Env.ToLowerInvariant(), (Get-Date).ToString("yyyyMMddHHmmssfff")

        $list = Invoke-WebRequest -Uri "$BaseUrl/Admin/Customers" -WebSession $Session -UseBasicParsing
        if ($list.StatusCode -ne 200) {
            throw "Cannot open customer list"
        }

        $createPage = Invoke-WebRequest -Uri "$BaseUrl/Admin/Customers/Create" -WebSession $Session -UseBasicParsing
        $createToken = Get-AntiForgeryToken -Html ([string]$createPage.Content)
        $createBody = [ordered]@{
            "__RequestVerificationToken" = $createToken
            "Name" = "Auto Customer $Env"
            "Username" = $createdUsername
            "Password" = "123456"
            "PhoneNumber" = ("09{0}" -f ([int](Get-Random -Minimum 10000000 -Maximum 99999999)))
            "Email" = "$createdUsername@example.local"
            "Address" = "Auto Test"
            "Gender" = "Khac"
            "DateOfBirth" = "2000-01-01"
            "LoyaltyPoints" = "0"
        }
        Set-CheckboxField -Body $createBody -Name "IsActive" -Checked $true
        Invoke-WebRequest -Uri "$BaseUrl/Admin/Customers/Create" -Method Post -WebSession $Session -UseBasicParsing -Body $createBody | Out-Null

        $searchUrl = "$BaseUrl/Admin/Customers?search=$([System.Uri]::EscapeDataString($createdUsername))"
        $searchList = Invoke-WebRequest -Uri $searchUrl -WebSession $Session -UseBasicParsing
        $id = Get-FirstEditId -Html ([string]$searchList.Content) -ControllerName "Customers"
        if ($id -le 0) {
            throw "Cannot find customer Edit ID after create ($createdUsername)"
        }

        $editUrl = "$BaseUrl/Admin/Customers/Edit/$id"
        $edit = Invoke-WebRequest -Uri $editUrl -WebSession $Session -UseBasicParsing
        $html = [string]$edit.Content
        $token = Get-AntiForgeryToken -Html $html

        $name = Get-InputValue -Html $html -Name "Name"
        $username = Get-InputValue -Html $html -Name "Username"
        $phone = Get-InputValue -Html $html -Name "PhoneNumber"
        $email = Get-InputValue -Html $html -Name "Email"
        $address = Get-InputValue -Html $html -Name "Address"
        $gender = Get-InputValue -Html $html -Name "Gender"
        $dob = Get-InputValue -Html $html -Name "DateOfBirth"
        $pointsText = Get-InputValue -Html $html -Name "LoyaltyPoints" -Default "0"
        $isActive = Get-CheckBoxChecked -Html $html -Name "IsActive"
        $dateForPost = if ($IsLegacy) { "" } else { $dob }

        $newPhone = "09{0}" -f ([int](Get-Random -Minimum 10000000 -Maximum 99999999))

        $body = [ordered]@{
            "__RequestVerificationToken" = $token
            "Name" = $name
            "Username" = $username
            "Password" = ""
            "PhoneNumber" = $newPhone
            "Email" = $email
            "Address" = $address
            "Gender" = $gender
            "DateOfBirth" = $dateForPost
            "LoyaltyPoints" = $pointsText
        }
        if ($IsLegacy) {
            $body["CustomerID"] = "$id"
        }
        else {
            $body["CustomerId"] = "$id"
        }
        Set-CheckboxField -Body $body -Name "IsActive" -Checked $isActive

        Invoke-WebRequest -Uri $editUrl -Method Post -WebSession $Session -UseBasicParsing -Body $body | Out-Null

        $verify = Invoke-WebRequest -Uri $editUrl -WebSession $Session -UseBasicParsing
        $verifiedPhone = Get-InputValue -Html ([string]$verify.Content) -Name "PhoneNumber"
        if ($verifiedPhone -ne $newPhone) {
            throw "Updated phone mismatch ($verifiedPhone != $newPhone)"
        }

        # Restore
        $restoreToken = Get-AntiForgeryToken -Html ([string]$verify.Content)
        $restoreDate = if ($IsLegacy) { "" } else { (Get-InputValue -Html ([string]$verify.Content) -Name "DateOfBirth") }
        $restoreBody = [ordered]@{
            "__RequestVerificationToken" = $restoreToken
            "Name" = (Get-InputValue -Html ([string]$verify.Content) -Name "Name")
            "Username" = (Get-InputValue -Html ([string]$verify.Content) -Name "Username")
            "Password" = ""
            "PhoneNumber" = $phone
            "Email" = (Get-InputValue -Html ([string]$verify.Content) -Name "Email")
            "Address" = (Get-InputValue -Html ([string]$verify.Content) -Name "Address")
            "Gender" = (Get-InputValue -Html ([string]$verify.Content) -Name "Gender")
            "DateOfBirth" = $restoreDate
            "LoyaltyPoints" = (Get-InputValue -Html ([string]$verify.Content) -Name "LoyaltyPoints" -Default "0")
        }
        if ($IsLegacy) {
            $restoreBody["CustomerID"] = "$id"
        }
        else {
            $restoreBody["CustomerId"] = "$id"
        }

        $activeAfterUpdate = Get-CheckBoxChecked -Html ([string]$verify.Content) -Name "IsActive"
        Set-CheckboxField -Body $restoreBody -Name "IsActive" -Checked $activeAfterUpdate

        Invoke-WebRequest -Uri $editUrl -Method Post -WebSession $Session -UseBasicParsing -Body $restoreBody | Out-Null

        $verifyRestore = Invoke-WebRequest -Uri $editUrl -WebSession $Session -UseBasicParsing
        $restoredPhone = Get-InputValue -Html ([string]$verifyRestore.Content) -Name "PhoneNumber"
        if ($restoredPhone -ne $phone) {
            throw "Restore phone mismatch ($restoredPhone != $phone)"
        }

        if ($createdCustomer) {
            $deactivateToken = Get-AntiForgeryToken -Html ([string]$verifyRestore.Content)
            Invoke-WebRequest -Uri "$BaseUrl/Admin/Customers/Deactivate/$id" -Method Post -WebSession $Session -UseBasicParsing -Body @{
                "__RequestVerificationToken" = $deactivateToken
            } | Out-Null
        }

        $note = if ($createdCustomer) { " (created temp customer)" } else { "" }
        Add-Result -Env $Env -Feature $feature -Pass $true -Detail "customerId=$id phone $phone -> $newPhone -> $restoredPhone$note"
    }
    catch {
        Add-Result -Env $Env -Feature $feature -Pass $false -Detail $_.Exception.Message
    }
}

function Test-EmployeeManagement {
    param(
        [Parameter(Mandatory = $true)][string]$Env,
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)]$Session,
        [Parameter(Mandatory = $true)][bool]$IsLegacy
    )

    $feature = "Admin-Employees"

    try {
        $createdEmployee = $false
        $createdUsername = $null

        $list = Invoke-WebRequest -Uri "$BaseUrl/Admin/Employees" -WebSession $Session -UseBasicParsing
        if ($list.StatusCode -ne 200) {
            throw "Cannot open employee list"
        }

        $id = Get-FirstEditId -Html ([string]$list.Content) -ControllerName "Employees"
        if ($id -le 0) {
            $createdEmployee = $true
            $createdUsername = "autoemp_{0}_{1}" -f $Env.ToLowerInvariant(), (Get-Date).ToString("yyyyMMddHHmmssfff")

            $createPage = Invoke-WebRequest -Uri "$BaseUrl/Admin/Employees/Create" -WebSession $Session -UseBasicParsing
            $createHtml = [string]$createPage.Content
            $createToken = Get-AntiForgeryToken -Html $createHtml
            $branch = if ($IsLegacy) { Get-SelectValue -Html $createHtml -Name "BranchID" -Default "1" } else { Get-SelectValue -Html $createHtml -Name "BranchId" -Default "1" }
            $role = if ($IsLegacy) { Get-SelectValue -Html $createHtml -Name "RoleID" -Default "1" } else { Get-SelectValue -Html $createHtml -Name "RoleId" -Default "1" }

            $createBody = [ordered]@{
                "__RequestVerificationToken" = $createToken
                "Name" = "Auto Employee $Env"
                "Username" = $createdUsername
                "Password" = "123456"
                "Phone" = ("09{0}" -f ([int](Get-Random -Minimum 10000000 -Maximum 99999999)))
                "Email" = "$createdUsername@example.local"
                "Shift" = "Sang"
                "Salary" = "9000000"
            }
            if ($IsLegacy) {
                $createBody["BranchID"] = $branch
                $createBody["RoleID"] = $role
            }
            else {
                $createBody["BranchId"] = $branch
                $createBody["RoleId"] = $role
            }
            Set-CheckboxField -Body $createBody -Name "IsActive" -Checked $true
            Invoke-WebRequest -Uri "$BaseUrl/Admin/Employees/Create" -Method Post -WebSession $Session -UseBasicParsing -Body $createBody | Out-Null

            $searchUrl = "$BaseUrl/Admin/Employees?search=$([System.Uri]::EscapeDataString($createdUsername))"
            $searchList = Invoke-WebRequest -Uri $searchUrl -WebSession $Session -UseBasicParsing
            $id = Get-FirstEditId -Html ([string]$searchList.Content) -ControllerName "Employees"
            if ($id -le 0) {
                throw "Cannot find employee Edit ID after create ($createdUsername)"
            }
        }

        $editUrl = "$BaseUrl/Admin/Employees/Edit/$id"
        $edit = Invoke-WebRequest -Uri $editUrl -WebSession $Session -UseBasicParsing
        $html = [string]$edit.Content
        $token = Get-AntiForgeryToken -Html $html

        $name = Get-InputValue -Html $html -Name "Name"
        $username = Get-InputValue -Html $html -Name "Username"
        $phone = Get-InputValue -Html $html -Name "Phone"
        $email = Get-InputValue -Html $html -Name "Email"
        $shift = Get-InputValue -Html $html -Name "Shift"
        $salary = Get-InputValue -Html $html -Name "Salary" -Default "0"
        $branch = if ($IsLegacy) { Get-SelectValue -Html $html -Name "BranchID" -Default "1" } else { Get-SelectValue -Html $html -Name "BranchId" -Default "1" }
        $role = if ($IsLegacy) { Get-SelectValue -Html $html -Name "RoleID" -Default "1" } else { Get-SelectValue -Html $html -Name "RoleId" -Default "1" }
        $isActive = Get-CheckBoxChecked -Html $html -Name "IsActive"

        $newPhone = "09{0}" -f ([int](Get-Random -Minimum 10000000 -Maximum 99999999))

        $body = [ordered]@{
            "__RequestVerificationToken" = $token
            "Name" = $name
            "Username" = $username
            "Password" = ""
            "Phone" = $newPhone
            "Email" = $email
            "Shift" = $shift
            "Salary" = $salary
        }
        if ($IsLegacy) {
            $body["EmployeeID"] = "$id"
            $body["BranchID"] = $branch
            $body["RoleID"] = $role
        }
        else {
            $body["EmployeeId"] = "$id"
            $body["BranchId"] = $branch
            $body["RoleId"] = $role
        }
        Set-CheckboxField -Body $body -Name "IsActive" -Checked $isActive

        Invoke-WebRequest -Uri $editUrl -Method Post -WebSession $Session -UseBasicParsing -Body $body | Out-Null

        $verify = Invoke-WebRequest -Uri $editUrl -WebSession $Session -UseBasicParsing
        $verifiedPhone = Get-InputValue -Html ([string]$verify.Content) -Name "Phone"
        if ($verifiedPhone -ne $newPhone) {
            throw "Updated phone mismatch ($verifiedPhone != $newPhone)"
        }

        # Restore
        $restoreToken = Get-AntiForgeryToken -Html ([string]$verify.Content)
        $restoreBody = [ordered]@{
            "__RequestVerificationToken" = $restoreToken
            "Name" = (Get-InputValue -Html ([string]$verify.Content) -Name "Name")
            "Username" = (Get-InputValue -Html ([string]$verify.Content) -Name "Username")
            "Password" = ""
            "Phone" = $phone
            "Email" = (Get-InputValue -Html ([string]$verify.Content) -Name "Email")
            "Shift" = (Get-InputValue -Html ([string]$verify.Content) -Name "Shift")
            "Salary" = (Get-InputValue -Html ([string]$verify.Content) -Name "Salary")
        }
        if ($IsLegacy) {
            $restoreBody["EmployeeID"] = "$id"
            $restoreBody["BranchID"] = (Get-SelectValue -Html ([string]$verify.Content) -Name "BranchID" -Default "$branch")
            $restoreBody["RoleID"] = (Get-SelectValue -Html ([string]$verify.Content) -Name "RoleID" -Default "$role")
        }
        else {
            $restoreBody["EmployeeId"] = "$id"
            $restoreBody["BranchId"] = (Get-SelectValue -Html ([string]$verify.Content) -Name "BranchId" -Default "$branch")
            $restoreBody["RoleId"] = (Get-SelectValue -Html ([string]$verify.Content) -Name "RoleId" -Default "$role")
        }

        $activeAfterUpdate = Get-CheckBoxChecked -Html ([string]$verify.Content) -Name "IsActive"
        Set-CheckboxField -Body $restoreBody -Name "IsActive" -Checked $activeAfterUpdate

        Invoke-WebRequest -Uri $editUrl -Method Post -WebSession $Session -UseBasicParsing -Body $restoreBody | Out-Null

        $verifyRestore = Invoke-WebRequest -Uri $editUrl -WebSession $Session -UseBasicParsing
        $restoredPhone = Get-InputValue -Html ([string]$verifyRestore.Content) -Name "Phone"
        if ($restoredPhone -ne $phone) {
            throw "Restore phone mismatch ($restoredPhone != $phone)"
        }

        if ($createdEmployee) {
            $deactivateToken = Get-AntiForgeryToken -Html ([string]$verifyRestore.Content)
            Invoke-WebRequest -Uri "$BaseUrl/Admin/Employees/Deactivate/$id" -Method Post -WebSession $Session -UseBasicParsing -Body @{
                "__RequestVerificationToken" = $deactivateToken
            } | Out-Null
        }

        $note = if ($createdEmployee) { " (created temp employee)" } else { "" }
        Add-Result -Env $Env -Feature $feature -Pass $true -Detail "employeeId=$id phone $phone -> $newPhone -> $restoredPhone$note"
    }
    catch {
        Add-Result -Env $Env -Feature $feature -Pass $false -Detail $_.Exception.Message
    }
}

function Test-Revenue {
    param(
        [Parameter(Mandatory = $true)][string]$Env,
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)]$Session,
        [Parameter(Mandatory = $true)][bool]$IsLegacy
    )

    $feature = "Admin-Revenue"

    try {
        $revUrl = if ($IsLegacy) { "$BaseUrl/Admin/Reports/Revenue" } else { "$BaseUrl/Admin/Reports/Revenue?days=30" }
        $topUrl = if ($IsLegacy) { "$BaseUrl/Admin/Reports/TopDishes" } else { "$BaseUrl/Admin/Reports/TopDishes?days=30&take=10" }

        $rev = Invoke-WebRequest -Uri $revUrl -WebSession $Session -UseBasicParsing
        if ($rev.StatusCode -ne 200) {
            throw "Revenue page status: $($rev.StatusCode)"
        }

        $top = Invoke-WebRequest -Uri $topUrl -WebSession $Session -UseBasicParsing
        if ($top.StatusCode -ne 200) {
            throw "TopDishes page status: $($top.StatusCode)"
        }

        $revHtml = [string]$rev.Content
        $topHtml = [string]$top.Content

        $hasRevenueToken = ($revHtml -match "Doanh thu|Tổng doanh thu|Revenue")
        $hasTopToken = ($topHtml -match "Món|Top|Dishes")

        if (-not ($hasRevenueToken -and $hasTopToken)) {
            throw "Report pages loaded but expected content tokens not found"
        }

        Add-Result -Env $Env -Feature $feature -Pass $true -Detail "Revenue + TopDishes pages loaded"
    }
    catch {
        Add-Result -Env $Env -Feature $feature -Pass $false -Detail $_.Exception.Message
    }
}

function Test-Dishes {
    param(
        [Parameter(Mandatory = $true)][string]$Env,
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)]$Session,
        [Parameter(Mandatory = $true)][bool]$IsLegacy
    )

    $feature = "Admin-Dishes"

    try {
        $createUrl = "$BaseUrl/Admin/Dishes/Create"
        $createPage = Invoke-WebRequest -Uri $createUrl -WebSession $Session -UseBasicParsing
        $createHtml = [string]$createPage.Content

        $token = Get-AntiForgeryToken -Html $createHtml
        $categoryField = if ($IsLegacy) { "CategoryID" } else { "CategoryId" }
        $categoryId = Get-SelectValue -Html $createHtml -Name $categoryField -Default ""
        if ([string]::IsNullOrWhiteSpace($categoryId)) {
            throw "Cannot resolve category for dish create"
        }

        $dishName = "AUTO_ADMIN_TEST_{0}_{1}_{2}" -f $Env, (Get-Date).ToString('yyyyMMdd_HHmmssfff'), (Get-Random -Minimum 100 -Maximum 999)

        $body = [ordered]@{
            "__RequestVerificationToken" = $token
            "Name" = $dishName
            "Price" = "123000"
            "Unit" = "Phan"
            "Description" = "Auto admin flow test"
            "Image" = "/images/placeholder.jpg"
        }

        $body[$categoryField] = $categoryId
        Set-CheckboxField -Body $body -Name "Available" -Checked $true
        Set-CheckboxField -Body $body -Name "IsVegetarian" -Checked $false
        Set-CheckboxField -Body $body -Name "IsDailySpecial" -Checked $false
        Set-CheckboxField -Body $body -Name "IsActive" -Checked $true

        $createResp = Invoke-WebRequest -Uri $createUrl -Method Post -WebSession $Session -UseBasicParsing -Body $body
        $createUri = ""
        try {
            $createUri = [string]$createResp.BaseResponse.ResponseUri.AbsoluteUri
        }
        catch {
            $createUri = ""
        }
        if ($createUri -match '/Admin/Dishes/Create') {
            $createError = [regex]::Match([string]$createResp.Content, '(?is)<div class=\"alert alert-danger[^>]*>(.*?)</div>')
            if ($createError.Success) {
                throw ("Create dish failed: " + ($createError.Groups[1].Value -replace '<[^>]+>', ' ').Trim())
            }
            throw "Create dish did not redirect to list"
        }

        $searchUrl = "$BaseUrl/Admin/Dishes?search=$([System.Uri]::EscapeDataString($dishName))"
        $list = Invoke-WebRequest -Uri $searchUrl -WebSession $Session -UseBasicParsing
        $listHtml = [string]$list.Content

        $id = Get-FirstDishIdBySearch -Html $listHtml -DishName $dishName
        if ($id -le 0) {
            throw "Created dish not found in list"
        }

        $deletePageUrl = "$BaseUrl/Admin/Dishes/Delete/$id"
        $deletePage = Invoke-WebRequest -Uri $deletePageUrl -WebSession $Session -UseBasicParsing
        $deleteToken = Get-AntiForgeryToken -Html ([string]$deletePage.Content)

        $deleteBody = [ordered]@{
            "__RequestVerificationToken" = $deleteToken
        }
        if ($IsLegacy) {
            $deleteBody["DishID"] = "$id"
        }

        Invoke-WebRequest -Uri $deletePageUrl -Method Post -WebSession $Session -UseBasicParsing -Body $deleteBody | Out-Null

        Start-Sleep -Milliseconds 400

        if ($IsLegacy) {
            $reList = Invoke-WebRequest -Uri $searchUrl -WebSession $Session -UseBasicParsing
            $reHtml = [string]$reList.Content
            $rowMatch = [regex]::Match(
                $reHtml,
                "(?is)<tr[^>]*>.*?$([regex]::Escape($dishName)).*?</tr>",
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

            if ($rowMatch.Success) {
                $isStopped = $rowMatch.Value -match "Ngừng bán"
                if (-not $isStopped) {
                    throw "Dish still active after remove action"
                }
                Add-Result -Env $Env -Feature $feature -Pass $true -Detail "dishId=$id name=$dishName created then switched to 'Ngung ban'"
            }
            else {
                Add-Result -Env $Env -Feature $feature -Pass $true -Detail "dishId=$id name=$dishName created then removed from list"
            }
        }
        else {
            $editUrl = "$BaseUrl/Admin/Dishes/Edit/$id"
            $edit = Invoke-WebRequest -Uri $editUrl -WebSession $Session -UseBasicParsing
            $editHtml = [string]$edit.Content
            $isActive = Get-CheckBoxChecked -Html $editHtml -Name "IsActive"
            $available = Get-CheckBoxChecked -Html $editHtml -Name "Available"
            if ($isActive -or $available) {
                throw "Dish not deactivated after delete action (IsActive=$isActive, Available=$available)"
            }
            Add-Result -Env $Env -Feature $feature -Pass $true -Detail "dishId=$id name=$dishName created then deactivated"
        }
    }
    catch {
        Add-Result -Env $Env -Feature $feature -Pass $false -Detail $_.Exception.Message
    }
}

$results = New-Object 'System.Collections.Generic.List[object]'
$oldIisProc = $null

try {
    Wait-NewGateway -BaseUrl $NewBaseUrl

    $oldSession = $null
    if (-not $SkipOld) {
        $oldIisProc = Start-OldIisExpress -BaseUrl $OldBaseUrl -SitePath $oldSitePath -IisExe $iisExpressExe
        Write-Log "Logging in admin on OLD..."
        $oldSession = Login-AdminOld -BaseUrl $OldBaseUrl -Username $AdminUsername -Password $AdminPassword
        Add-Result -Env "OLD" -Feature "Admin-Login" -Pass $true -Detail "login success"
    }
    else {
        Add-Result -Env "OLD" -Feature "Admin-Login" -Pass $true -Detail "skipped by -SkipOld"
    }

    Write-Log "Logging in admin on NEW..."
    $newSession = Login-AdminNew -BaseUrl $NewBaseUrl -Username $AdminUsername -Password $AdminPassword
    Add-Result -Env "NEW" -Feature "Admin-Login" -Pass $true -Detail "login success"

    Write-Log "Testing customer management..."
    if (-not $SkipOld) {
        Test-CustomerManagement -Env "OLD" -BaseUrl $OldBaseUrl -Session $oldSession -IsLegacy $true
    }
    Test-CustomerManagement -Env "NEW" -BaseUrl $NewBaseUrl -Session $newSession -IsLegacy $false

    Write-Log "Testing employee management..."
    if (-not $SkipOld) {
        Test-EmployeeManagement -Env "OLD" -BaseUrl $OldBaseUrl -Session $oldSession -IsLegacy $true
    }
    Test-EmployeeManagement -Env "NEW" -BaseUrl $NewBaseUrl -Session $newSession -IsLegacy $false

    Write-Log "Testing reports..."
    if (-not $SkipOld) {
        Test-Revenue -Env "OLD" -BaseUrl $OldBaseUrl -Session $oldSession -IsLegacy $true
    }
    Test-Revenue -Env "NEW" -BaseUrl $NewBaseUrl -Session $newSession -IsLegacy $false

    Write-Log "Testing dishes add/remove..."
    if (-not $SkipOld) {
        Test-Dishes -Env "OLD" -BaseUrl $OldBaseUrl -Session $oldSession -IsLegacy $true
    }
    Test-Dishes -Env "NEW" -BaseUrl $NewBaseUrl -Session $newSession -IsLegacy $false
}
catch {
    Add-Result -Env "SYSTEM" -Feature "Unhandled" -Pass $false -Detail $_.Exception.Message
}
finally {
    if ($oldIisProc -and -not $KeepOldIis) {
        Write-Log "Stopping old IIS Express..."
        try {
            if (!$oldIisProc.HasExited) {
                Stop-Process -Id $oldIisProc.Id -Force
            }
        }
        catch {
        }
    }
}

$passCount = @($results | Where-Object { $_.pass }).Count
$totalCount = $results.Count
$summary = [pscustomobject]@{
    timestamp = $timestamp
    total = $totalCount
    passed = $passCount
    failed = ($totalCount - $passCount)
    log = $logPath
    results = $results
}

$summary | ConvertTo-Json -Depth 20 | Set-Content -Path $summaryPath -Encoding UTF8
Write-Log "Summary JSON: $summaryPath"
Write-Log "FINAL: $passCount/$totalCount PASS"

$results | Format-Table -AutoSize | Out-String | Write-Output
Write-Output "SUMMARY_JSON=$summaryPath"
Write-Output "SUMMARY_LOG=$logPath"
Write-Output "SUMMARY_PASS=$passCount/$totalCount"
