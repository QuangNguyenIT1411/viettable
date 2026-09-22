param(
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "reset-microservice-test-data.ps1"
if (!(Test-Path $scriptPath)) {
    throw "reset-microservice-test-data.ps1 was not found."
}

if ($WhatIf) {
    powershell.exe -ExecutionPolicy Bypass -File $scriptPath -WhatIf
}
else {
    powershell.exe -ExecutionPolicy Bypass -File $scriptPath
}
