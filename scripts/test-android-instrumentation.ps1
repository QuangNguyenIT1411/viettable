param(
    [ValidateSet('Smoke', 'Integration', 'Security', 'All')]
    [string]$Tier = 'All',
    [string]$Serial = '',
    [string]$TestUsername = '',
    [string]$TestPassword = '',
    [string]$QrCode = '',
    [switch]$SkipBuild,
    [switch]$RunExistingSessionSecurity
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$adb = 'D:\Quang\Android\platform-tools\adb.exe'
$package = 'com.restaurant.viettable'
$runner = 'com.restaurant.viettable.test/androidx.test.runner.AndroidJUnitRunner'
$mainApk = Join-Path $projectRoot 'app\build\outputs\apk\debug\app-debug.apk'
$testApk = Join-Path $projectRoot 'app\build\outputs\apk\androidTest\debug\app-debug-androidTest.apk'
$createdAccount = $false
$cameraWasGranted = $false

function Invoke-Adb([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments) {
    $prefix = @()
    if ($script:Serial) { $prefix = @('-s', $script:Serial) }
    & $script:adb @prefix @Arguments
    if ($LASTEXITCODE -ne 0) { throw "adb failed: $($Arguments -join ' ')" }
}

function New-FailureArtifacts([string[]]$InstrumentationOutput) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $directory = Join-Path $projectRoot "artifacts\android-tests\$stamp"
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $InstrumentationOutput | Set-Content -Encoding UTF8 (Join-Path $directory 'instrumentation.txt')
    (Invoke-Adb shell dumpsys activity activities) | Select-String 'mResumedActivity|topResumedActivity' |
        Set-Content -Encoding UTF8 (Join-Path $directory 'current-activity.txt')
    $prefix = @()
    if ($script:Serial) { $prefix = @('-s', $script:Serial) }
    $safeLogs = @(& $script:adb @prefix @('logcat','-d','-s','AndroidRuntime:E','ActivityManager:W','VietTableSignalR:I','*:S') 2>&1)
    if ($LASTEXITCODE -eq 0) {
        $safeLogs | Set-Content -Encoding UTF8 (Join-Path $directory 'logcat-sanitized.txt')
    }
    try {
        $info = [Diagnostics.ProcessStartInfo]::new()
        $info.FileName = $script:adb
        $info.Arguments = "-s $script:Serial exec-out screencap -p"
        $info.UseShellExecute = $false
        $info.RedirectStandardOutput = $true
        $process = [Diagnostics.Process]::Start($info)
        $imagePath = Join-Path $directory 'screen.png'
        $image = [IO.File]::Create($imagePath)
        try { $process.StandardOutput.BaseStream.CopyTo($image); $process.WaitForExit(10000) | Out-Null }
        finally { $image.Dispose(); $process.Dispose() }
    } catch { Write-Warning 'Could not capture failure screenshot.' }
    Write-Warning "Failure diagnostics: $directory"
}

if (-not (Test-Path $adb)) { throw "adb not found: $adb" }
$connected = @(& $adb devices -l | Select-String '^\S+\s+device\b' | ForEach-Object { ($_ -split '\s+')[0] })
if (-not $Serial) {
    if ($connected.Count -ne 1) { throw 'Connect exactly one device/emulator or pass -Serial.' }
    $Serial = $connected[0]
} elseif ($connected -notcontains $Serial) { throw "Device '$Serial' is not connected/authorized." }

$ready = Invoke-RestMethod 'http://localhost:5100/readyz' -TimeoutSec 10
if ($ready.status -ne 'ready') { throw 'Gateway is not ready.' }
Write-Host "PASS Gateway ready; device=$Serial; tier=$Tier"

$isEmulator = $Serial -like 'emulator-*'
$androidApi = if ($isEmulator) { 'http://10.0.2.2:5100' } else { 'http://127.0.0.1:5100' }
if (-not $isEmulator) { Invoke-Adb reverse tcp:5100 tcp:5100 | Out-Null }

if (-not $env:JAVA_HOME) {
    $candidates = @('D:\Quang\Android1\jbr', 'C:\Program Files\Android\Android Studio\jbr')
    $env:JAVA_HOME = $candidates | Where-Object { Test-Path (Join-Path $_ 'bin\java.exe') } | Select-Object -First 1
}
if (-not $env:JAVA_HOME) { throw 'Set JAVA_HOME to the Android Studio JDK.' }

if (-not $SkipBuild) {
    Push-Location $projectRoot
    try {
        & .\gradlew.bat :app:assembleDebug :app:testDebugUnitTest :app:assembleDebugAndroidTest `
            "-PVIETTABLE_API_BASE_URL=$androidApi"
        if ($LASTEXITCODE -ne 0) { throw "Gradle failed with exit code $LASTEXITCODE." }
    } finally { Pop-Location }
}
if (-not (Test-Path $mainApk) -or -not (Test-Path $testApk)) { throw 'Debug/test APK is missing.' }
Invoke-Adb install -r $mainApk | Out-Host
Invoke-Adb install -r $testApk | Out-Host

$requiresBackendAccount = $Tier -in @('Smoke', 'Integration', 'All')
if ($requiresBackendAccount) {
    if ([string]::IsNullOrWhiteSpace($TestUsername) -xor [string]::IsNullOrWhiteSpace($TestPassword)) {
        throw 'Provide both -TestUsername and -TestPassword, or neither.'
    }
    if ([string]::IsNullOrWhiteSpace($TestUsername)) {
        $suffix = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds().ToString()
        $TestUsername = "android_ui_$suffix"
        $TestPassword = "Vt9_$([Guid]::NewGuid().ToString('N'))"
        $phone = '09' + $suffix.Substring($suffix.Length - 8)
        $registration = Invoke-RestMethod -Method Post -Uri 'http://localhost:5100/api/mobile/v1/auth/register' `
            -Headers @{ 'X-Correlation-Id'="android-ui-$suffix" } -ContentType 'application/json' `
            -Body (@{ name='Android UI Test'; username=$TestUsername; password=$TestPassword;
                phoneNumber=$phone; email="$TestUsername@example.test" } | ConvertTo-Json)
        if (-not $registration.refreshToken) { throw 'Could not create isolated Android test account.' }
        Invoke-RestMethod -Method Post -Uri 'http://localhost:5100/api/mobile/v1/auth/logout' `
            -ContentType 'application/json' -Body (@{ refreshToken=$registration.refreshToken } | ConvertTo-Json) | Out-Null
        $createdAccount = $true
        Write-Host 'PASS created ephemeral backend account (credential values not printed)'
    }
}

if (($Tier -in @('Integration', 'All')) -and [string]::IsNullOrWhiteSpace($QrCode)) {
    $branches = @(Invoke-RestMethod 'http://localhost:5100/api/mobile/v1/branches' -TimeoutSec 10 |
        ForEach-Object { $_ })
    foreach ($branch in $branches) {
        $tableResponse = Invoke-RestMethod "http://localhost:5101/api/branches/$($branch.branchId)/tables" -TimeoutSec 10
        $fixture = @($tableResponse.tables | Where-Object { -not [string]::IsNullOrWhiteSpace($_.qrCode) } | Select-Object -First 1)
        if ($fixture.Count) { $QrCode = $fixture[0].qrCode; break }
    }
    if ([string]::IsNullOrWhiteSpace($QrCode)) { throw 'No QR fixture is available; pass -QrCode explicitly.' }
    Write-Host 'PASS discovered a QR fixture (value not printed)'
}

$classes = switch ($Tier) {
    'Smoke' { @('com.restaurant.viettable.network.AppLaunchSmokeTest') }
    'Integration' { @(
        'com.restaurant.viettable.network.AuthUiIntegrationTest',
        'com.restaurant.viettable.network.FaqUiIntegrationTest',
        'com.restaurant.viettable.network.QrManualUiIntegrationTest') }
    'Security' { @(
        'com.restaurant.viettable.network.SecureTokenStoreDeviceTest',
        'com.restaurant.viettable.network.AuthSecurityRuntimeTest') }
    'All' { @(
        'com.restaurant.viettable.network.AppLaunchSmokeTest',
        'com.restaurant.viettable.network.AuthUiIntegrationTest',
        'com.restaurant.viettable.network.FaqUiIntegrationTest',
        'com.restaurant.viettable.network.QrManualUiIntegrationTest',
        'com.restaurant.viettable.network.SecureTokenStoreDeviceTest',
        'com.restaurant.viettable.network.AuthSecurityRuntimeTest') }
}

try {
    $cameraLine = (Invoke-Adb shell dumpsys package $package | Select-String 'android.permission.CAMERA: granted=' | Select-Object -First 1).Line
    $cameraWasGranted = $cameraLine -match 'granted=true'
    if ($Tier -in @('Integration', 'All')) {
        Invoke-Adb shell pm revoke $package android.permission.CAMERA | Out-Null
    }
    $instrumentArgs = @('shell','am','instrument','-w','-r','-e','class',($classes -join ','))
    if ($requiresBackendAccount) {
        $instrumentArgs += @('-e','testUsername',$TestUsername,'-e','testPassword',$TestPassword)
    }
    if ($QrCode) { $instrumentArgs += @('-e','testQrCode',$QrCode) }
    if ($RunExistingSessionSecurity) { $instrumentArgs += @('-e','runExistingSessionSecurity','true') }
    $instrumentArgs += $runner
    $output = @(& $adb @('-s',$Serial) @instrumentArgs 2>&1)
    $exit = $LASTEXITCODE
    $output | Out-Host
    if ($exit -ne 0 -or -not ($output -match 'OK \(')) {
        New-FailureArtifacts $output
        throw "Instrumentation failed (adb exit $exit)."
    }
    Write-Host "PASS Android instrumentation tier '$Tier'"
} finally {
    if ($cameraWasGranted) {
        try { Invoke-Adb shell pm grant $package android.permission.CAMERA | Out-Null } catch { Write-Warning 'Could not restore CAMERA permission.' }
    }
    if (-not $isEmulator) { try { Invoke-Adb reverse tcp:5100 tcp:5100 | Out-Null } catch { } }
}

if ($createdAccount) {
    Write-Host 'INFO ephemeral identity row remains because no public account-delete API exists; all issued test sessions were logged out.'
}
