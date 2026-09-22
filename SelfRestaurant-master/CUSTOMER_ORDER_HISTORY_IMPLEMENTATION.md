# Customer Order History Implementation

## Overview
Added customer order history (nhật ký món ăn) to the admin panel's customer activity logs page. Customers now have two sections: activity logs (login/register/password changes) and order history (past orders with dishes).

## Changes Made

### 1. Backend - Orders Service
**File**: `src/Services/SelfRestaurant.Orders.Api/Controllers/OrdersController.cs`
- ✅ Already implemented `GetCustomerOrderHistory` endpoint (lines 290-400)
- Returns paginated order history with:
  - Order code, time, completion time
  - Table name
  - Status (code and name)
  - Total amount and item count
  - Dishes summary (e.g., "2x Phở Bò, 1x Cơm Gà")
- Supports pagination: `page`, `pageSize`, `days` parameters

### 2. Backend - Identity Service
**File**: `src/Services/SelfRestaurant.Identity.Api/Infrastructure/InternalHistoryContracts.cs`
- ✅ Added `CustomerOrderHistoryItem` record type
- ✅ Added `CustomerOrderHistoryPagedResponse` record type

**File**: `src/Services/SelfRestaurant.Identity.Api/Infrastructure/OrdersApiClient.cs`
- ✅ Added `GetCustomerOrderHistoryAsync()` method
- Calls Orders service: `/api/internal/customers/{customerId}/order-history`

**File**: `src/Services/SelfRestaurant.Identity.Api/Controllers/IdentityController.cs`
- ✅ Added endpoint: `GET /api/internal/customers/{customerId}/order-history`
- Accepts: `page`, `pageSize`, `days` query parameters
- Returns paginated order history

### 3. Backend - Gateway
**File**: `src/Gateway/SelfRestaurant.Gateway.Api/Services/CustomersClient.cs`
- ✅ Added `GetCustomerOrderHistoryAsync()` method
- Calls Identity service internal endpoint

**File**: `src/Gateway/SelfRestaurant.Gateway.Api/Controllers/AdminGatewayController.cs`
- ✅ Added endpoint: `GET /api/gateway/admin/customers/{customerId}/order-history`
- Requires admin authentication
- Accepts: `page`, `pageSize`, `days` query parameters

### 4. Frontend - Types
**File**: `src/Frontend/selfrestaurant-admin-web/src/lib/types.ts`
- ✅ Added `AdminCustomerOrderHistoryItemDto` type with fields:
  - orderId, orderCode, orderTime, completedTime
  - tableName, statusCode, statusName
  - totalAmount, itemCount, dishesSummary

### 5. Frontend - API Client
**File**: `src/Frontend/selfrestaurant-admin-web/src/lib/api.ts`
- ✅ Added `getCustomerOrderHistory()` method
- Calls: `GET /api/gateway/admin/customers/{customerId}/order-history`
- Returns: `Paged<AdminCustomerOrderHistoryItemDto>`

### 6. Frontend - UI
**File**: `src/Frontend/selfrestaurant-admin-web/src/pages/customers/CustomerActivityLogsPage.tsx`
- ✅ Updated to fetch both activity logs and order history
- ✅ Added separate pagination for each section
- ✅ Display two sections:
  1. **Nhật ký hoạt động** (Activity Logs)
     - Login, register, password changes
     - Shows IP address, user agent, notes
  2. **Nhật ký đặt món** (Order History)
     - Order code, time, table, status
     - Dishes summary
     - Total amount
- ✅ Added `getOrderStatusColor()` helper for status badges
- ✅ Both sections have independent pagination

## API Endpoints

### Get Customer Order History
```
GET /api/gateway/admin/customers/{customerId}/order-history
```

**Query Parameters:**
- `page` (optional, default: 1) - Page number
- `pageSize` (optional, default: 20) - Items per page
- `days` (optional, default: 90) - Number of days to look back

**Response:**
```json
{
  "page": 1,
  "pageSize": 20,
  "totalItems": 45,
  "totalPages": 3,
  "items": [
    {
      "orderId": 123,
      "orderCode": "ORD-2026-001",
      "orderTime": "2026-05-09T10:30:00",
      "completedTime": "2026-05-09T11:00:00",
      "tableName": "Bàn 5",
      "statusCode": "COMPLETED",
      "statusName": "Hoàn thành",
      "totalAmount": 250000,
      "itemCount": 3,
      "dishesSummary": "2x Phở Bò, 1x Cơm Gà"
    }
  ]
}
```

## Status Colors
Order status badges use these colors:
- `PENDING` → warning (yellow)
- `PREPARING` → info (blue)
- `READY` → primary (purple)
- `COMPLETED` → success (green)
- `CANCELLED` → danger (red)

## Build Status
- ✅ Backend: All 6 microservices build successfully (18.3s, 3 warnings - pre-existing)
- ✅ Frontend: Builds successfully (2.15s)

## Testing Instructions

1. **Start all services:**
   ```bash
   # Start backend services
   dotnet run --project src/Gateway/SelfRestaurant.Gateway.Api
   dotnet run --project src/Services/SelfRestaurant.Identity.Api
   dotnet run --project src/Services/SelfRestaurant.Orders.Api
   
   # Start frontend
   cd src/Frontend/selfrestaurant-admin-web
   npm run dev
   ```

2. **Test the feature:**
   - Login to admin panel
   - Navigate to "Khách hàng" (Customers)
   - Click "Nhật ký" on any customer
   - Verify two sections appear:
     - "Nhật ký hoạt động" (activity logs)
     - "Nhật ký đặt món" (order history)
   - Test pagination on both sections
   - Verify order details display correctly

3. **Verify data:**
   - Order history should show customer's past orders
   - Each order shows: code, time, table, status, dishes, total
   - Pagination works independently for each section
   - Status badges have correct colors

## Files Modified
1. `src/Services/SelfRestaurant.Identity.Api/Infrastructure/InternalHistoryContracts.cs`
2. `src/Services/SelfRestaurant.Identity.Api/Infrastructure/OrdersApiClient.cs`
3. `src/Services/SelfRestaurant.Identity.Api/Controllers/IdentityController.cs`
4. `src/Gateway/SelfRestaurant.Gateway.Api/Services/CustomersClient.cs`
5. `src/Gateway/SelfRestaurant.Gateway.Api/Controllers/AdminGatewayController.cs`
6. `src/Frontend/selfrestaurant-admin-web/src/lib/types.ts`
7. `src/Frontend/selfrestaurant-admin-web/src/lib/api.ts`
8. `src/Frontend/selfrestaurant-admin-web/src/pages/customers/CustomerActivityLogsPage.tsx`

## Notes
- Order history defaults to last 90 days
- Both sections have independent pagination
- Order history shows only non-cancelled items in totals
- Dishes summary is formatted as "quantity x dish name"
- All changes follow existing architecture patterns
- No breaking changes to existing functionality
