$dotnet='C:\Program Files\dotnet\dotnet.exe'
Write-Host "Exists=$(Test-Path $dotnet)"
Write-Host "Running: $dotnet --version"
& $dotnet --version
Write-Host "DollarQuestion=$?"
Write-Host "LastExitCode=$LASTEXITCODE"
