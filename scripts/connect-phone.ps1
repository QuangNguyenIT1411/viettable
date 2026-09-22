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
    & $adb shell am force-stop com.restaurant.viettable
    & $adb shell am start -W -n com.restaurant.viettable/.MainActivity | Out-Host
}
