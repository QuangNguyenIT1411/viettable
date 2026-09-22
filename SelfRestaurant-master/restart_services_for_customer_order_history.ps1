# Script to restart services with new customer order history feature

Write-Host "=== Restarting Services for Customer Order History Feature ===" -ForegroundColor Cyan
Write-Host ""

# Stop any running services
Write-Host "Stopping any running services..." -ForegroundColor Yellow
Get-Process -Name "SelfRestaurant.*" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Start Identity service (port 5100)
Write-Host "Starting Identity service on port 5100..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; dotnet run --project src/Services/SelfRestaurant.Identity.Api --urls=http://localhost:5100"
Start-Sleep -Seconds 5

# Start Orders service (port 5200)
Write-Host "Starting Orders service on port 5200..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; dotnet run --project src/Services/SelfRestaurant.Orders.Api --urls=http://localhost:5200"
Start-Sleep -Seconds 5

# Start Catalog service (port 5300)
Write-Host "Starting Catalog service on port 5300..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; dotnet run --project src/Services/SelfRestaurant.Catalog.Api --urls=http://localhost:5300"
Start-Sleep -Seconds 5

# Start Billing service (port 5400)
Write-Host "Starting Billing service on port 5400..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; dotnet run --project src/Services/SelfRestaurant.Billing.Api --urls=http://localhost:5400"
Start-Sleep -Seconds 5

# Start Customers service (port 5500)
Write-Host "Starting Customers service on port 5500..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; dotnet run --project src/Services/SelfRestaurant.Customers.Api --urls=http://localhost:5500"
Start-Sleep -Seconds 5

# Start Gateway (port 7100)
Write-Host "Starting Gateway on port 7100..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; dotnet run --project src/Gateway/SelfRestaurant.Gateway.Api --urls=http://localhost:7100"
Start-Sleep -Seconds 5

Write-Host ""
Write-Host "=== All services started! ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Services running on:" -ForegroundColor Yellow
Write-Host "  - Identity:  http://localhost:5100" -ForegroundColor White
Write-Host "  - Orders:    http://localhost:5200" -ForegroundColor White
Write-Host "  - Catalog:   http://localhost:5300" -ForegroundColor White
Write-Host "  - Billing:   http://localhost:5400" -ForegroundColor White
Write-Host "  - Customers: http://localhost:5500" -ForegroundColor White
Write-Host "  - Gateway:   http://localhost:7100" -ForegroundColor White
Write-Host ""
Write-Host "Admin panel: http://localhost:7100/Admin" -ForegroundColor Green
Write-Host ""
Write-Host "Press any key to stop all services..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Stop all services
Write-Host "Stopping all services..." -ForegroundColor Red
Get-Process -Name "SelfRestaurant.*" -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host "All services stopped." -ForegroundColor Green
