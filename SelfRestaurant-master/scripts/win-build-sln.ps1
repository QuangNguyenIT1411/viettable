param(
  [string]$Solution = 'SelfRestaurant.Microservices.sln',
  [string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if(!(Test-Path $dotnet)){ throw "dotnet not found at $dotnet" }
& $dotnet build $Solution -c $Configuration
if($LASTEXITCODE -ne 0){ throw "dotnet build failed: $LASTEXITCODE" }
Write-Host 'Build completed.'
