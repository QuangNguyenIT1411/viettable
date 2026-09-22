# Cập nhật: Nhật ký riêng cho từng nhân viên và phân trang

## Tóm tắt thay đổi

### 1. **Mỗi chef/cashier có nhật ký riêng**
   - ✅ Chef logs được lọc theo `ActorId` (mỗi chef chỉ thấy log của mình)
   - ✅ Cashier logs được lọc theo `EmployeeId` (mỗi cashier chỉ thấy hóa đơn của mình)
   - ✅ Customer logs đã có phân trang từ trước

### 2. **Thêm phân trang cho tất cả nhật ký**
   - ✅ Chef activity logs: có phân trang (page, pageSize, totalItems, totalPages)
   - ✅ Cashier history: có phân trang (page, pageSize, totalItems, totalPages)
   - ✅ Customer activity logs: đã có phân trang từ trước

### 3. **Chef có 2 loại nhật ký**
   - ✅ **Nhật ký hoạt động**: Tạm ngưng/Tiếp tục món ăn (có phân trang)
   - ✅ **Nhật ký nấu ăn**: Đơn hàng đã hoàn thành (nấu món, hủy món, sửa thành phần)
   - ✅ **Ghi chú**: Hiển thị lý do hủy món, thay đổi thành phần (ví dụ: "[HỦY] Hết nguyên liệu", "[BEP] Khách yêu cầu không hành")

## Chi tiết thay đổi

### Backend Changes

#### 1. **Catalog Service** (`SelfRestaurant.Catalog.Api`)
   - **File**: `Controllers/AdminCatalogController.cs`
     - Sửa endpoint: `GET /api/admin/branches/{branchId}/chef/{chefId}/activity-logs`
     - Thêm tham số `chefId` để lọc theo chef cụ thể
     - Thêm phân trang: `page`, `pageSize` (mặc định: page=1, pageSize=50)
     - Lọc logs theo `ActorId == chefId`
     - Trả về: `{ page, pageSize, totalItems, totalPages, logs }`

   - **File**: `Persistence/Bootstrap/CatalogDbBootstrapper.cs`
     - Thêm migration để tạo cột `BranchId` nếu chưa có
     - Thêm index `IX_BusinessAuditLogs_BranchId`

#### 2. **Billing Service** (`SelfRestaurant.Billing.Api`)
   - **File**: `Controllers/CashierBillsController.cs`
     - Sửa endpoint: `GET /api/internal/employees/{employeeId}/cashier/history`
     - Thêm phân trang: `page`, `pageSize` (mặc định: page=1, pageSize=50)
     - Đã lọc theo `employeeId` từ trước (không thay đổi)
     - Trả về: `{ page, pageSize, totalItems, totalPages, items }`

#### 3. **Identity Service** (`SelfRestaurant.Identity.Api`)
   - **File**: `Controllers/IdentityController.cs`
     - Sửa endpoint: `GET /api/identity/admin/employees/{employeeId}/history`
     - Thêm tham số: `page`, `pageSize`, `days`
     - Gọi Catalog API để lấy chef activity logs (có phân trang)
     - Gọi Billing API để lấy cashier history (có phân trang)
     - Trả về cấu trúc mới với phân trang

   - **File**: `Infrastructure/BillingApiClient.cs`
     - Cập nhật `GetCashierHistoryAsync()` để hỗ trợ phân trang
     - Trả về `CashierHistoryPagedResponse`

   - **File**: `Infrastructure/CatalogApiClient.cs`
     - Thêm method `GetChefActivityLogsAsync()` để lấy chef logs
     - Trả về `ChefActivityLogsPagedResponse`

   - **File**: `Infrastructure/InternalHistoryContracts.cs`
     - Thêm `CashierHistoryPagedResponse`
     - Thêm `ChefActivityLogItem`
     - Thêm `ChefActivityLogsPagedResponse`

#### 4. **Gateway Service** (`SelfRestaurant.Gateway.Api`)
   - **File**: `Controllers/AdminGatewayController.cs`
     - Sửa endpoint: `GET /api/gateway/admin/employees/{employeeId}/history`
     - Thêm tham số: `page`, `pageSize`, `days`

   - **File**: `Services/IdentityClient.cs`
     - Cập nhật `GetAdminEmployeeHistoryAsync()` với tham số phân trang

   - **File**: `Models/ApiContracts.cs`
     - Thay `AdminChefHistoryItemDto` → `AdminChefActivityLogItemDto`
     - Thêm `AdminChefActivityLogsPageDto`
     - Thêm `AdminCashierHistoryPageDto`
     - Cập nhật `AdminEmployeeHistoryResponse` với cấu trúc mới

### Frontend Changes

#### 1. **Admin Web** (`selfrestaurant-admin-web`)
   - **File**: `src/lib/api.ts`
     - Cập nhật `getEmployeeHistory()` với tham số: `page`, `pageSize`, `days`

   - **File**: `src/lib/types.ts`
     - Thêm `AdminChefActivityLogItemDto`
     - Cập nhật `AdminEmployeeHistoryResponse` với cấu trúc mới:
       - `chefActivityLogs`: có phân trang
       - `cashierHistory`: có phân trang

   - **File**: `src/pages/employees/EmployeesModulePage.tsx`
     - Hiển thị chef activity logs thay vì chef order history
     - Hiển thị thông tin phân trang cho cả chef và cashier
     - Parse `afterState` JSON để hiển thị tên món và trạng thái
     - Hiển thị badge cho trạng thái (Đang bán/Tạm ngưng)

## Cấu trúc dữ liệu mới

### Chef Activity Logs (Tạm ngưng/Tiếp tục món)
```json
{
  "page": 1,
  "pageSize": 50,
  "totalItems": 25,
  "totalPages": 1,
  "logs": [
    {
      "auditId": 123,
      "timestampUtc": "2026-05-09T10:30:00Z",
      "actionType": "DISH_SELLING_PAUSED_BY_CHEF",
      "dishId": 45,
      "actorName": "Chef Hùng",
      "afterState": "{\"branchId\":1,\"menuId\":10,\"categoryDishId\":100,\"available\":false,\"dishName\":\"Phở bò\"}"
    }
  ]
}
```

### Chef Cooking History (Nấu món)
```json
[
  {
    "orderId": 456,
    "orderCode": "ORD-2026-001",
    "orderTime": "2026-05-09T11:00:00",
    "completedTime": "2026-05-09T11:30:00",
    "tableName": "Bàn 5",
    "branchName": "Chi nhánh 1",
    "statusCode": "COMPLETED",
    "statusName": "Hoàn thành",
    "dishesSummary": "Phở bò x2, Cơm rang x1",
    "notes": "[HỦY] Hết nguyên liệu | [BEP] Khách yêu cầu không hành"
  }
]
```

**Ghi chú (Notes):**
- `[HỦY]` - Lý do hủy món (khi chef hủy món trong đơn)
- `[BEP]` - Ghi chú của bếp (khi chef thêm ghi chú hoặc sửa thành phần)
- Nhiều ghi chú được nối bằng ` | `

### Cashier History
```json
{
  "page": 1,
  "pageSize": 50,
  "totalItems": 25,
  "totalPages": 1,
  "logs": [
    {
      "auditId": 123,
      "timestampUtc": "2026-05-09T10:30:00Z",
      "actionType": "DISH_SELLING_PAUSED_BY_CHEF",
      "dishId": 45,
      "actorName": "Chef Hùng",
      "afterState": "{\"branchId\":1,\"menuId\":10,\"categoryDishId\":100,\"available\":false,\"dishName\":\"Phở bò\"}"
    }
  ]
}
```

### Cashier History
```json
{
  "page": 1,
  "pageSize": 50,
  "totalItems": 150,
  "totalPages": 3,
  "items": [
    {
      "billId": 789,
      "billCode": "BILL-2026-001",
      "billTime": "2026-05-09T11:00:00",
      "orderCode": "ORD-001",
      "tableName": "Bàn 5",
      "customerName": "Nguyễn Văn A",
      "subtotal": 200000,
      "discount": 0,
      "pointsDiscount": 0,
      "pointsUsed": null,
      "totalAmount": 200000,
      "paymentMethod": "CASH",
      "paymentAmount": 200000,
      "changeAmount": 0
    }
  ]
}
```

## API Endpoints

### Chef Activity Logs
```
GET /api/admin/branches/{branchId}/chef/{chefId}/activity-logs?page=1&pageSize=50&days=90
```

### Cashier History
```
GET /api/internal/employees/{employeeId}/cashier/history?page=1&pageSize=50&days=90
```

### Employee History (Gateway)
```
GET /api/gateway/admin/employees/{employeeId}/history?page=1&pageSize=50&days=90
```

## Hiển thị frontend

### Chef có 2 bảng nhật ký:

#### 1. Nhật ký hoạt động (Tạm ngưng/Tiếp tục món)
- Hiển thị: Thời gian, Hành động, Món ăn, Trạng thái
- Badge màu: 🟢 Đang bán / ⚫ Tạm ngưng
- Có phân trang nếu > 50 log

#### 2. Nhật ký nấu ăn (Đơn hàng đã hoàn thành)
- Hiển thị: Mã đơn, Thời gian tạo, Hoàn tất, Bàn, Trạng thái, Món, **Ghi chú**
- Badge màu theo trạng thái: 
  - 🟢 READY (Sẵn sàng)
  - 🟡 PREPARING (Đang chế biến)
  - 🔵 COMPLETED (Hoàn thành)
  - ⚫ PENDING (Chờ xử lý)
- **Ghi chú** hiển thị:
  - `[HỦY]` Lý do hủy món (ví dụ: "[HỦY] Hết nguyên liệu")
  - `[BEP]` Ghi chú của bếp (ví dụ: "[BEP] Khách yêu cầu không hành")
  - Nhiều ghi chú được nối bằng ` | `
- Không có phân trang (hiển thị tối đa 50 đơn gần nhất)

### Cashier có 1 bảng nhật ký:
- Hiển thị: Mã hóa đơn, Thời gian, Mã đơn, Bàn, Khách hàng, Tổng tiền
- Có phân trang nếu > 50 hóa đơn

## Lưu ý quan trọng

1. **Database Migration**: Khi khởi động Catalog service, migration sẽ tự động chạy để thêm cột `BranchId` vào bảng `BusinessAuditLogs`

2. **Phân trang mặc định**:
   - Page: 1
   - PageSize: 50 (tối đa 100)
   - Days: 90 (tối đa 365)

3. **Lọc theo nhân viên**:
   - Chef activity logs: lọc theo `ActorId` (chỉ hiển thị log tạm ngưng/tiếp tục của chef đó)
   - Chef cooking history: lọc theo `BranchId` (hiển thị tất cả đơn của chi nhánh - **CHƯA lọc theo chef cụ thể**)
   - Cashier logs: lọc theo `EmployeeId` (chỉ hiển thị hóa đơn của cashier đó)

4. **Hiển thị frontend**:
   - Chef: 2 bảng (hoạt động + nấu ăn)
   - Cashier: 1 bảng (hóa đơn)
   - Cả hai đều có thông tin phân trang khi cần

## Testing

1. **Database Migration**: Khi khởi động Catalog service, migration sẽ tự động chạy để thêm cột `BranchId` vào bảng `BusinessAuditLogs`

2. **Phân trang mặc định**:
   - Page: 1
   - PageSize: 50 (tối đa 100)
   - Days: 90 (tối đa 365)

3. **Lọc theo nhân viên**:
   - Chef logs: lọc theo `ActorId` (chỉ hiển thị log của chef đó)
   - Cashier logs: lọc theo `EmployeeId` (chỉ hiển thị hóa đơn của cashier đó)

4. **Hiển thị frontend**:
   - Chef: hiển thị nhật ký tạm ngưng/tiếp tục món ăn
   - Cashier: hiển thị danh sách hóa đơn đã thanh toán
   - Cả hai đều có thông tin phân trang ở cuối bảng

## Testing

### Build Status
- ✅ Backend: Build thành công (3 warnings không liên quan)
- ✅ Frontend: Build thành công

### Cách test
1. Khởi động tất cả services
2. Đăng nhập admin
3. Vào trang Nhân viên → chọn một chef → click "Nhật ký"
4. Kiểm tra:
   - **Bảng 1 - Nhật ký hoạt động**:
     - Chỉ hiển thị log tạm ngưng/tiếp tục của chef đó (không có log của chef khác)
     - Có thông tin phân trang nếu có nhiều hơn 50 log
     - Hiển thị đúng tên món và trạng thái (Đang bán/Tạm ngưng)
   - **Bảng 2 - Nhật ký nấu ăn**:
     - Hiển thị các đơn hàng đã hoàn thành trong chi nhánh
     - Hiển thị trạng thái với badge màu sắc
     - Hiển thị tóm tắt món ăn

5. Làm tương tự với cashier
   - Chỉ hiển thị hóa đơn của cashier đó
   - Có thông tin phân trang nếu có nhiều hơn 50 hóa đơn

## Lưu ý về Chef Cooking History

⚠️ **Hiện tại**: Chef cooking history hiển thị **TẤT CẢ đơn hàng của chi nhánh**, không lọc theo chef cụ thể.

Nếu muốn lọc theo chef cụ thể, cần:
1. Orders service phải lưu thông tin chef nào đã xử lý đơn
2. Thêm filter `chefId` vào API `GetChefHistoryAsync()`
3. Cập nhật database schema để lưu `ChefId` trong bảng Orders

Hiện tại hệ thống chưa có thông tin này, nên hiển thị tất cả đơn của chi nhánh là hợp lý.
