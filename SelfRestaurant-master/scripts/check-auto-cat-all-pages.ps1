param(
    [string]$GatewayBaseUrl = "http://localhost:5100",
    [string]$CatalogBaseUrl = "http://localhost:5101"
)

$ErrorActionPreference = "Stop"
$patterns = @("AUTO_CAT_", "DBG_CAT_")

function Get-AntiForgeryToken {
    param([string]$Html)

    $regexes = @(
        'name="__RequestVerificationToken"[^>]*value="([^"]+)"',
        "name='__RequestVerificationToken'[^>]*value='([^']+)'",
        'value="([^"]+)"[^>]*name="__RequestVerificationToken"'
    )

    foreach ($pattern in $regexes) {
        $m = [regex]::Match($Html, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($m.Success) { return $m.Groups[1].Value }
    }

    throw "Cannot find anti-forgery token"
}

function Login-Customer {
    param([string]$BaseUrl, [string]$Username, [string]$Password)

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/Customer/Login?mode=login" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html ([string]$loginPage.Content)

    $resp = Invoke-WebRequest -Uri "$BaseUrl/Customer/Login" -Method Post -WebSession $session -UseBasicParsing -Headers @{ "X-Requested-With" = "XMLHttpRequest" } -Body @{
        "__RequestVerificationToken" = $token
        "mode" = "login"
        "Login.Username" = $Username
        "Login.Password" = $Password
        "Login.ReturnUrl" = ""
    }

    $json = $resp.Content | ConvertFrom-Json
    if (-not [bool]$json.success) {
        throw "Customer login failed: $($json.message)"
    }

    return $session
}

function Login-Staff {
    param([string]$BaseUrl, [string]$Username, [string]$Password)

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/Login" -WebSession $session -UseBasicParsing
    $token = Get-AntiForgeryToken -Html ([string]$loginPage.Content)

    $resp = Invoke-WebRequest -Uri "$BaseUrl/Staff/Account/Login" -Method Post -WebSession $session -UseBasicParsing -Headers @{ "X-Requested-With" = "XMLHttpRequest" } -Body @{
        "__RequestVerificationToken" = $token
        "username" = $Username
        "password" = $Password
        "rememberMe" = "false"
    }

    $json = $resp.Content | ConvertFrom-Json
    if (-not [bool]$json.success) {
        throw "Staff login failed ($Username): $($json.message)"
    }

    return $session
}

function Test-ContentNoAutoCat {
    param([string]$Name, [string]$Content)

    $bad = @()
    foreach ($p in $patterns) {
        if ($Content -match [regex]::Escape($p)) {
            $bad += $p
        }
    }

    if ($bad.Count -gt 0) {
        return [pscustomobject]@{ Name = $Name; Pass = $false; Detail = "contains $($bad -join ', ')" }
    }

    return [pscustomobject]@{ Name = $Name; Pass = $true; Detail = "ok" }
}

function Get-Page {
    param([string]$Url, $Session)
    if ($null -eq $Session) {
        return Invoke-WebRequest -Uri $Url -UseBasicParsing
    }
    return Invoke-WebRequest -Uri $Url -WebSession $Session -UseBasicParsing
}

$results = New-Object System.Collections.Generic.List[object]

# API level check for menu categories
$menuApi = Invoke-WebRequest -Uri "$CatalogBaseUrl/api/branches/1/menu" -UseBasicParsing
$menuJson = $menuApi.Content | ConvertFrom-Json
$categoryNames = @()
if ($null -ne $menuJson.categories) {
    $categoryNames = @($menuJson.categories | ForEach-Object { [string]$_.categoryName })
}
$badCategories = @($categoryNames | Where-Object { $_ -match '(?i)^(AUTO_CAT_|DBG_CAT_)' })
if ($badCategories.Count -gt 0) {
    $results.Add([pscustomobject]@{ Name = "API /api/menu categories"; Pass = $false; Detail = "bad categories: $($badCategories -join '; ')" }) | Out-Null
}
else {
    $results.Add([pscustomobject]@{ Name = "API /api/menu categories"; Pass = $true; Detail = "ok" }) | Out-Null
}

$customerSession = Login-Customer -BaseUrl $GatewayBaseUrl -Username "lan.nguyen" -Password "123456"
$chefSession = Login-Staff -BaseUrl $GatewayBaseUrl -Username "chef_hung" -Password "123456"
$cashierSession = Login-Staff -BaseUrl $GatewayBaseUrl -Username "cashier_lan" -Password "123456"
$adminSession = Login-Staff -BaseUrl $GatewayBaseUrl -Username "admin" -Password "123456"

$pages = @(
    @{ Name = "Customer Menu"; Url = "$GatewayBaseUrl/Menu?tableId=2&BranchId=1&tableNumber=2"; Session = $customerSession },
    @{ Name = "Chef Index"; Url = "$GatewayBaseUrl/Staff/Chef/Index"; Session = $chefSession },
    @{ Name = "Cashier Index"; Url = "$GatewayBaseUrl/Staff/Cashier"; Session = $cashierSession },
    @{ Name = "Admin Categories"; Url = "$GatewayBaseUrl/Admin/Categories"; Session = $adminSession },
    @{ Name = "Admin Dishes"; Url = "$GatewayBaseUrl/Admin/Dishes"; Session = $adminSession },
    @{ Name = "Admin Employees"; Url = "$GatewayBaseUrl/Admin/Employees"; Session = $adminSession },
    @{ Name = "Admin Customers"; Url = "$GatewayBaseUrl/Admin/Customers"; Session = $adminSession }
)

foreach ($page in $pages) {
    try {
        $resp = Get-Page -Url $page.Url -Session $page.Session
        if ($resp.StatusCode -ne 200) {
            $results.Add([pscustomobject]@{ Name = $page.Name; Pass = $false; Detail = "status=$($resp.StatusCode)" }) | Out-Null
            continue
        }

        $results.Add((Test-ContentNoAutoCat -Name $page.Name -Content ([string]$resp.Content))) | Out-Null
    }
    catch {
        $results.Add([pscustomobject]@{ Name = $page.Name; Pass = $false; Detail = $_.Exception.Message }) | Out-Null
    }
}

$pass = @($results | Where-Object { $_.Pass }).Count
$total = $results.Count
$summary = [pscustomobject]@{
    pass = $pass
    total = $total
    allPass = ($pass -eq $total)
    generatedAt = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    results = $results
}

$summary | ConvertTo-Json -Depth 10
