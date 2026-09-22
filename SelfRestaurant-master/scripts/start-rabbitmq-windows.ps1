param(
    [switch]$Detached = $true
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "docker command not found on Windows host. Please start Docker Desktop first."
}

$args = @("compose", "up")
if ($Detached) {
    $args += "-d"
}
$args += "rabbitmq"

Write-Host "Starting RabbitMQ via docker compose..."
& docker @args
if ($LASTEXITCODE -ne 0) {
    throw "docker compose up rabbitmq failed: $LASTEXITCODE"
}

Write-Host "RabbitMQ requested. Management UI: http://localhost:15672"
