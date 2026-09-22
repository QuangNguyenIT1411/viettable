param(
    [string]$GatewayBaseUrl = "http://localhost:5100",
    [switch]$KeepTestCustomer
)

$ErrorActionPreference = "Stop"
$baseUrl = "$($GatewayBaseUrl.TrimEnd('/'))/api/mobile/v1/auth"
$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$username = "b1test_$suffix"
$email = "$username@example.test"
$phone = "08" + ($suffix.ToString().Substring($suffix.ToString().Length - 8))

function Invoke-MobilePost([string]$Path, [object]$Body, [string]$AccessToken = "") {
    $headers = @{ "X-Correlation-Id" = "b1-$suffix" }
    if ($AccessToken) { $headers.Authorization = "Bearer $AccessToken" }
    Invoke-RestMethod -ErrorAction Stop -Method Post -Uri "$baseUrl/$Path" `
        -Headers $headers -ContentType "application/json" -Body ($Body | ConvertTo-Json -Depth 5)
}

function Assert-Rejected401([scriptblock]$Action, [string]$Message) {
    try {
        & $Action | Out-Null
        throw $Message
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw }
    }
}

try {
    $register = Invoke-MobilePost "register" @{ name="B1 Test"; username=$username; password="Pass123!"; phoneNumber=$phone; email=$email }
    if (-not $register.accessToken -or -not $register.refreshToken) { throw "Register did not issue a token pair." }
    Write-Output "PASS register and JWT/refresh token issuance"

    $firstRefresh = $register.refreshToken
    $rotated = Invoke-MobilePost "refresh" @{ refreshToken=$firstRefresh }
    if ($rotated.refreshToken -eq $firstRefresh) { throw "Refresh token was not rotated." }
    Write-Output "PASS refresh token rotation"

    Assert-Rejected401 { Invoke-MobilePost "refresh" @{ refreshToken=$firstRefresh } } "Reused refresh token was accepted."
    Assert-Rejected401 { Invoke-MobilePost "refresh" @{ refreshToken=$rotated.refreshToken } } "Reused-token family remained valid."
    Write-Output "PASS reuse detection and token-family revocation"

    $login = Invoke-MobilePost "login" @{ username=$username; password="Pass123!" }
    Invoke-MobilePost "change-password" @{ currentPassword="Pass123!"; newPassword="Pass456!"; confirmPassword="Pass456!" } $login.accessToken | Out-Null
    Assert-Rejected401 { Invoke-MobilePost "refresh" @{ refreshToken=$login.refreshToken } } "Password change did not revoke sessions."
    Write-Output "PASS Bearer-protected password change and session revocation"

    $login2 = Invoke-MobilePost "login" @{ username=$username; password="Pass456!" }
    Invoke-MobilePost "logout" @{ refreshToken=$login2.refreshToken } | Out-Null
    Assert-Rejected401 { Invoke-MobilePost "refresh" @{ refreshToken=$login2.refreshToken } } "Logout token remained valid."
    Write-Output "PASS logout revocation"

    $forgot = Invoke-MobilePost "forgot-password" @{ usernameOrEmailOrPhone=$username }
    if (-not $forgot.resetToken) { throw "Development forgot-password did not expose its test token." }
    Invoke-MobilePost "reset-password" @{ token=$forgot.resetToken; newPassword="Pass789!"; confirmPassword="Pass789!" } | Out-Null
    $finalLogin = Invoke-MobilePost "login" @{ username=$username; password="Pass789!" }
    if (-not $finalLogin.accessToken) { throw "Login after password reset failed." }
    Write-Output "PASS forgot/reset password and final login"
    Write-Output "B1 runtime checks passed."
}
finally {
    if (-not $KeepTestCustomer -and (docker inspect selfrestaurant-db 2>$null)) {
        $cleanupSql = "USE RESTAURANT_IDENTITY; DELETE r FROM dbo.MobileRefreshTokens r JOIN dbo.Customers c ON c.CustomerID=r.CustomerId WHERE c.Username='$username'; DELETE p FROM dbo.PasswordResetTokens p JOIN dbo.Customers c ON c.CustomerID=p.CustomerID WHERE c.Username='$username'; DELETE FROM dbo.Customers WHERE Username='$username';"
        $encodedCleanup = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($cleanupSql))
        $cleanupCommand = "echo $encodedCleanup | base64 -d | /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -C"
        docker exec selfrestaurant-db /bin/bash -lc $cleanupCommand 2>$null | Out-Null
    }
}
