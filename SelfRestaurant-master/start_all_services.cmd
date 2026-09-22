@echo off
echo Starting all services...
echo.

echo Starting Identity service (port 5100)...
start "Identity" cmd /k "dotnet run --project src\Services\SelfRestaurant.Identity.Api --urls=http://localhost:5100"
timeout /t 5 /nobreak >nul

echo Starting Orders service (port 5200)...
start "Orders" cmd /k "dotnet run --project src\Services\SelfRestaurant.Orders.Api --urls=http://localhost:5200"
timeout /t 5 /nobreak >nul

echo Starting Catalog service (port 5300)...
start "Catalog" cmd /k "dotnet run --project src\Services\SelfRestaurant.Catalog.Api --urls=http://localhost:5300"
timeout /t 5 /nobreak >nul

echo Starting Billing service (port 5400)...
start "Billing" cmd /k "dotnet run --project src\Services\SelfRestaurant.Billing.Api --urls=http://localhost:5400"
timeout /t 5 /nobreak >nul

echo Starting Customers service (port 5500)...
start "Customers" cmd /k "dotnet run --project src\Services\SelfRestaurant.Customers.Api --urls=http://localhost:5500"
timeout /t 5 /nobreak >nul

echo Starting Gateway (port 7100)...
start "Gateway" cmd /k "dotnet run --project src\Gateway\SelfRestaurant.Gateway.Api --urls=http://localhost:7100"

echo.
echo All services are starting...
echo Wait 30 seconds for all services to be ready.
echo.
echo Admin panel: http://localhost:7100/Admin
echo.
pause
