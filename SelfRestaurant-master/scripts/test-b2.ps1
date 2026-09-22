param(
    [string]$GatewayBaseUrl = "http://localhost:5100",
    [switch]$KeepTestCustomers
)

$ErrorActionPreference = "Stop"
$gatewayUrl = $GatewayBaseUrl.TrimEnd('/')
$authUrl = "$gatewayUrl/api/mobile/v1/auth"
$meUrl = "$gatewayUrl/api/mobile/v1/me"
$suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$username1 = "b2test_a_$suffix"
$username2 = "b2test_b_$suffix"
$email1 = "$username1@example.test"
$email2 = "$username2@example.test"
$suffixText = $suffix.ToString()
$phoneTail = $suffixText.Substring($suffixText.Length - 8)
$phone1 = "08$phoneTail"
$phone2 = "09$phoneTail"

function Invoke-Json([string]$Method, [string]$Uri, [object]$Body = $null, [string]$AccessToken = "") {
    $headers = @{ "X-Correlation-Id" = "b2-$suffix" }
    if ($AccessToken) { $headers.Authorization = "Bearer $AccessToken" }
    $parameters = @{
        ErrorAction = "Stop"
        Method = $Method
        Uri = $Uri
        Headers = $headers
        ContentType = "application/json"
    }
    if ($null -ne $Body) { $parameters.Body = ($Body | ConvertTo-Json -Depth 8) }
    Invoke-RestMethod @parameters
}

function Assert-HttpError([int]$ExpectedStatus, [string]$ExpectedCode, [scriptblock]$Action) {
    try {
        & $Action | Out-Null
        throw "Expected HTTP $ExpectedStatus ($ExpectedCode), but the request succeeded."
    }
    catch {
        $failure = $_
        $response = $failure.Exception.Response
        if ($null -eq $response -or $response.StatusCode.value__ -ne $ExpectedStatus) { throw }
        $responseBody = $failure.ErrorDetails.Message
        if ([string]::IsNullOrWhiteSpace($responseBody) -and $null -ne $response.Content) {
            if ($response.Content -is [System.Net.Http.HttpContent]) {
                $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            }
            else {
                $responseBody = $response.Content
            }
        }
        if ([string]::IsNullOrWhiteSpace($responseBody) -and $null -ne $response -and
            $null -ne $response.PSObject.Methods["GetResponseStream"]) {
            $stream = $response.GetResponseStream()
            if ($null -ne $stream) {
                $reader = [System.IO.StreamReader]::new($stream)
                try { $responseBody = $reader.ReadToEnd() } finally { $reader.Dispose() }
            }
        }
        if ([string]::IsNullOrWhiteSpace($responseBody)) { throw "HTTP error response did not contain JSON." }
        $payload = $responseBody | ConvertFrom-Json
        if ($payload.code -ne $ExpectedCode) {
            throw "Expected error code '$ExpectedCode', got '$($payload.code)'."
        }
        if (-not $payload.correlationId) { throw "MobileApiError did not contain correlationId." }
    }
}

function Register-TestCustomer([string]$Username, [string]$Email, [string]$Phone, [string]$Name) {
    Invoke-Json "Post" "$authUrl/register" @{
        name = $Name
        username = $Username
        password = "Pass123!"
        phoneNumber = $Phone
        email = $Email
    }
}

try {
    Assert-HttpError 401 "unauthorized" { Invoke-Json "Get" $meUrl }
    Write-Output "PASS profile endpoints require a customer Bearer token"

    $account1 = Register-TestCustomer $username1 $email1 $phone1 "B2 Customer A"
    $account2 = Register-TestCustomer $username2 $email2 $phone2 "B2 Customer B"
    if (-not $account1.accessToken -or -not $account2.accessToken) { throw "B1 registration did not issue access tokens." }

    $profile = Invoke-Json "Get" $meUrl $null $account1.accessToken
    if ($profile.username -ne $username1 -or $profile.name -ne "B2 Customer A") {
        throw "GET /me returned the wrong customer."
    }
    Write-Output "PASS read current customer profile from JWT subject"

    $updatedEmail = "updated_$email1"
    $updatedPhone = "+849$phoneTail"
    $updated = Invoke-Json "Put" $meUrl @{
        name = "B2 Customer Updated"
        phoneNumber = $updatedPhone
        email = $updatedEmail
        gender = "Nam"
        dateOfBirth = "1995-04-12"
        address = "Quan 1, TP. Ho Chi Minh"
    } $account1.accessToken
    $persisted = Invoke-Json "Get" $meUrl $null $account1.accessToken
    if ($updated.username -ne $username1 -or $persisted.name -ne "B2 Customer Updated" -or
        $persisted.phoneNumber -ne $updatedPhone -or $persisted.email -ne $updatedEmail -or
        $persisted.dateOfBirth -ne "1995-04-12") {
        throw "Profile update was not persisted correctly."
    }
    Write-Output "PASS update and persist profile while keeping username immutable"

    Assert-HttpError 400 "validation_failed" {
        Invoke-Json "Put" $meUrl @{ name="Invalid"; phoneNumber="123"; email=$updatedEmail } $account1.accessToken
    }
    Assert-HttpError 409 "email_already_exists" {
        Invoke-Json "Put" $meUrl @{ name="Duplicate"; phoneNumber=$updatedPhone; email=$email2 } $account1.accessToken
    }
    Write-Output "PASS profile validation and duplicate ownership checks"

    $loyalty = Invoke-Json "Get" "$meUrl/loyalty" $null $account1.accessToken
    if ($loyalty.availablePoints -ne 0 -or $loyalty.tierCode -ne "MEMBER" -or
        $loyalty.nextTierCode -ne "SILVER" -or $loyalty.nextTierAtPoints -ne 1000 -or
        $loyalty.progressPercent -ne 0 -or -not $loyalty.cardNumber) {
        throw "Default loyalty projection is incorrect."
    }
    Write-Output "PASS loyalty card, tier and next-tier progress projection"

    $defaults = Invoke-Json "Get" "$meUrl/settings" $null $account1.accessToken
    if (-not $defaults.pushNotificationsEnabled -or -not $defaults.smsReservationRemindersEnabled -or
        $defaults.biometricLoginEnabled -or $defaults.languageCode -ne "vi-VN" -or
        $null -ne $defaults.defaultBranchId) {
        throw "Default mobile settings are incorrect."
    }

    $saved = Invoke-Json "Put" "$meUrl/settings" @{
        pushNotificationsEnabled = $false
        smsReservationRemindersEnabled = $false
        biometricLoginEnabled = $true
        languageCode = "en-US"
        defaultBranchId = 1
    } $account1.accessToken
    $savedAgain = Invoke-Json "Get" "$meUrl/settings" $null $account1.accessToken
    if ($saved.pushNotificationsEnabled -or $savedAgain.smsReservationRemindersEnabled -or
        -not $savedAgain.biometricLoginEnabled -or $savedAgain.languageCode -ne "en-US" -or
        $savedAgain.defaultBranchId -ne 1) {
        throw "Mobile settings were not persisted correctly."
    }
    Write-Output "PASS create defaults and persist notification, biometric, language and branch settings"

    Assert-HttpError 400 "unsupported_language" {
        Invoke-Json "Put" "$meUrl/settings" @{
            pushNotificationsEnabled=$true; smsReservationRemindersEnabled=$true
            biometricLoginEnabled=$false; languageCode="fr-FR"
        } $account1.accessToken
    }
    Assert-HttpError 404 "branch_not_found" {
        Invoke-Json "Put" "$meUrl/settings" @{
            pushNotificationsEnabled=$true; smsReservationRemindersEnabled=$true
            biometricLoginEnabled=$false; languageCode="vi-VN"; defaultBranchId=999999
        } $account1.accessToken
    }
    Write-Output "PASS language and Catalog-owned default branch validation"
    Write-Output "B2 runtime checks passed."
}
finally {
    if (-not $KeepTestCustomers -and (docker inspect selfrestaurant-db 2>$null)) {
        $cleanupSql = "USE RESTAURANT_IDENTITY; DELETE r FROM dbo.MobileRefreshTokens r JOIN dbo.Customers c ON c.CustomerID=r.CustomerId WHERE c.Username IN ('$username1','$username2'); DELETE p FROM dbo.PasswordResetTokens p JOIN dbo.Customers c ON c.CustomerID=p.CustomerID WHERE c.Username IN ('$username1','$username2'); DELETE FROM dbo.CustomerSettings WHERE CustomerId IN (SELECT CustomerID FROM dbo.Customers WHERE Username IN ('$username1','$username2')); DELETE FROM dbo.Customers WHERE Username IN ('$username1','$username2');"
        $encodedCleanup = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($cleanupSql))
        $cleanupCommand = "echo $encodedCleanup | base64 -d | /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P `"`$MSSQL_SA_PASSWORD`" -C"
        docker exec selfrestaurant-db /bin/bash -lc $cleanupCommand 2>$null | Out-Null
    }
}
