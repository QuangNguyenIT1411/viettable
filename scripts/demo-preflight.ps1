$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'demo-status.ps1') -EnsureReverse -LaunchApp
exit $LASTEXITCODE
