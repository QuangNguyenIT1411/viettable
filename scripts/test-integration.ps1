param(
    [switch]$InstallApk
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$backendRoot = Join-Path $projectRoot 'SelfRestaurant-master'
$apkPath = Join-Path $projectRoot 'app\build\outputs\apk\debug\app-debug.apk'
$adbPath = 'D:\Quang\Android\platform-tools\adb.exe'

Write-Host 'Checking Docker services...'
Push-Location $backendRoot
try {
    docker compose ps --status running | Out-Host
} finally {
    Pop-Location
}

$health = Invoke-WebRequest -UseBasicParsing 'http://localhost:5100/health' -TimeoutSec 10
if ($health.StatusCode -ne 200) { throw "Gateway health returned $($health.StatusCode)." }
Write-Host 'PASS Gateway health'

$branches = @(Invoke-RestMethod 'http://localhost:5100/api/mobile/v1/branches' -TimeoutSec 10 | ForEach-Object { $_ })
if ($branches.Count -lt 1) { throw 'Gateway returned no branches.' }
Write-Host "PASS public branch catalog ($($branches.Count) branches)"

$branchId = $branches[0].branchId
$menu = Invoke-RestMethod "http://localhost:5100/api/mobile/v1/branches/$branchId/menu" -TimeoutSec 10
if (@($menu.categories).Count -lt 1) { throw 'Gateway returned an empty menu.' }
Write-Host "PASS branch menu ($(@($menu.categories).Count) categories)"

if (-not $env:JAVA_HOME) {
    $jdkCandidates = @(
        'D:\Quang\Android1\jbr',
        'C:\Program Files\Android\Android Studio\jbr',
        'C:\Program Files\Android\openjdk\jdk-21.0.8'
    )
    $env:JAVA_HOME = $jdkCandidates | Where-Object { Test-Path (Join-Path $_ 'bin\java.exe') } | Select-Object -First 1
}
if (-not $env:JAVA_HOME) { throw 'JDK was not found. Set JAVA_HOME before running this script.' }

$gradleArgs = @(':app:assembleDebug', ':app:testDebugUnitTest')
if ($InstallApk) {
    if (-not (Test-Path $adbPath)) { throw "adb was not found at $adbPath." }
    $devices = & $adbPath devices
    if (-not ($devices -match "(?m)^\S+\s+device$")) {
        throw 'No Android emulator or authorized device is connected.'
    }
    & $adbPath reverse tcp:5100 tcp:5100 | Out-Null
    $gradleArgs += '-PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100'
}

Push-Location $projectRoot
try {
    & .\gradlew.bat @gradleArgs
    if ($LASTEXITCODE -ne 0) { throw "Gradle failed with exit code $LASTEXITCODE." }
} finally {
    Pop-Location
}
if (-not (Test-Path $apkPath)) { throw 'Debug APK was not generated.' }
Write-Host "PASS Android build and unit tests: $apkPath"

if ($InstallApk) {
    & $adbPath install -r $apkPath
    if ($LASTEXITCODE -ne 0) { throw "APK installation failed with exit code $LASTEXITCODE." }
    & $adbPath shell am force-stop com.restaurant.viettable
    & $adbPath shell am start -W -n com.restaurant.viettable/.MainActivity | Out-Host
    Write-Host 'PASS APK installed on the connected Android device'
}

Write-Host 'Integration checks passed.'
