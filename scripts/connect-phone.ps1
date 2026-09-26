param(
    [switch]$OpenApp
)

$ErrorActionPreference = 'Stop'
$adb = 'D:\Quang\Android\platform-tools\adb.exe'
if (-not (Test-Path $adb)) { throw "adb was not found at $adb." }

$device = (& $adb devices -l | Select-String '^\S+\s+device\b').Line
if (-not $device) { throw 'No authorized Android device is connected.' }

& $adb reverse tcp:5100 tcp:5100 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not create the ADB reverse tunnel.' }

$status = & $adb shell "curl -sS -m 5 -o /dev/null -w '%{http_code}' http://127.0.0.1:5100/api/mobile/v1/branches"
if ($status -ne '200') { throw "The phone could not reach Gateway through ADB reverse (HTTP $status)." }

Write-Host "PASS connected: $device"
Write-Host 'PASS phone -> Gateway: http://127.0.0.1:5100'

if ($OpenApp) {
    $projectRoot = Split-Path -Parent $PSScriptRoot
    if (-not $env:JAVA_HOME) {
        $jdkCandidates = @(
            'D:\Quang\Android1\jbr',
            'C:\Program Files\Android\Android Studio\jbr',
            'C:\Program Files\Android\openjdk\jdk-21.0.8'
        )
        $env:JAVA_HOME = $jdkCandidates | Where-Object { Test-Path (Join-Path $_ 'bin\java.exe') } | Select-Object -First 1
    }
    if (-not $env:JAVA_HOME) { throw 'JDK was not found. Set JAVA_HOME before building the Samsung debug APK.' }

    Push-Location $projectRoot
    try {
        & .\gradlew.bat :app:assembleDebug '-PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100'
        if ($LASTEXITCODE -ne 0) { throw "Android debug build failed with exit code $LASTEXITCODE." }
    } finally {
        Pop-Location
    }

    $apkPath = Join-Path $projectRoot 'app\build\outputs\apk\debug\app-debug.apk'
    $buildConfigPath = Join-Path $projectRoot 'app\build\generated\source\buildConfig\debug\com\restaurant\viettable\BuildConfig.java'
    if (-not (Test-Path $apkPath) -or -not (Test-Path $buildConfigPath)) {
        throw 'Samsung-targeted debug APK or generated BuildConfig was not found.'
    }
    if ((Get-Content -Raw -LiteralPath $buildConfigPath) -notmatch 'API_BASE_URL\s*=\s*"http://127\.0\.0\.1:5100"') {
        throw 'Debug APK is not configured for the physical-device ADB reverse Gateway URL.'
    }
    & $adb install -r $apkPath
    if ($LASTEXITCODE -ne 0) { throw "APK installation failed with exit code $LASTEXITCODE." }
    & $adb shell am force-stop com.restaurant.viettable
    & $adb shell am start -W -n com.restaurant.viettable/.MainActivity | Out-Host
    Write-Host 'PASS installed Samsung debug APK targeting Gateway through adb reverse: http://127.0.0.1:5100'
}
