# Test script to verify ChefId tracking
# This script simulates a chef starting an order item

$baseUrl = "http://localhost:5100"  # Orders service port
$orderId = 19  # Use an existing order ID
$itemId = 98   # Use an existing item ID
$chefId = 3    # Use an existing chef/employee ID

Write-Host "Testing ChefId tracking..." -ForegroundColor Cyan
Write-Host "Order ID: $orderId" -ForegroundColor Yellow
Write-Host "Item ID: $itemId" -ForegroundColor Yellow
Write-Host "Chef ID: $chefId" -ForegroundColor Yellow
Write-Host ""

# Check current state
Write-Host "1. Checking current OrderItem state..." -ForegroundColor Green
$query = "SELECT ItemID, OrderID, DishID, Quantity, ChefId FROM OrderItems WHERE ItemID = $itemId"
sqlcmd -S localhost -d restaurant -Q $query

Write-Host ""
Write-Host "2. Simulating chef starting the item..." -ForegroundColor Green
$url = "$baseUrl/api/orders/$orderId/items/$itemId/chef/start?chefId=$chefId"
Write-Host "URL: $url" -ForegroundColor Gray

try {
    $response = Invoke-WebRequest -Uri $url -Method POST -ContentType "application/json" -Body "{}" -ErrorAction Stop
    Write-Host "Response: $($response.StatusCode) - $($response.Content)" -ForegroundColor Green
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Response: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
}

Write-Host ""
Write-Host "3. Checking OrderItem state after update..." -ForegroundColor Green
sqlcmd -S localhost -d restaurant -Q $query

Write-Host ""
Write-Host "Test completed!" -ForegroundColor Cyan
