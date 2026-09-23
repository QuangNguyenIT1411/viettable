$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$adb = 'D:\Quang\Android\platform-tools\adb.exe'
$apk = Join-Path $root 'app\build\outputs\apk\debug\app-debug.apk'
if (-not (Test-Path -LiteralPath $adb)) { throw 'ADB executable not found.' }
if (-not (Test-Path -LiteralPath $apk)) { throw 'Debug APK missing. Build with :app:assembleDebug first.' }
$devices = @(& $adb devices -l | Select-String '^\S+\s+device\b' | Where-Object { $_.Line -match 'model:SM_A528B\b' })
if ($devices.Count -ne 1) { throw 'Connect exactly one authorized Samsung SM-A528B.' }
$serial = ($devices[0].Line -split '\s+')[0]
& $adb -s $serial install -r $apk | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'APK installation failed.' }
& $adb -s $serial reverse tcp:5100 tcp:5100 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'ADB reverse failed.' }
& $adb -s $serial shell am start -W -n 'com.restaurant.viettable/.MainActivity' | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'App launch failed.' }
Write-Host 'Debug APK installed and launched; app data/session preserved.'
