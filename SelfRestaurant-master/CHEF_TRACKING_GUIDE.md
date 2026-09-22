# Hướng dẫn Test ChefId Tracking

## Tóm tắt thay đổi

Đã thêm cột `ChefId` vào bảng `OrderItems` để tracking chef nào đã nấu món nào.

## Các bước đã thực hiện

### 1. Database Migration ✅
- Thêm cột `ChefId INT NULL` vào bảng `OrderItems`
- Tạo index `IX_OrderItems_ChefId` để tối ưu query
- Dữ liệu cũ sẽ có `ChefId = NULL`

### 2. Code Changes ✅
- **Orders Service**: Lưu `ChefId` khi chef start món
- **Gateway**: Truyền `staff.EmployeeId` xuống Orders service
- **Identity Service**: Lọc cooking history theo `employeeId`

### 3. Build Status ✅
- Backend: Build thành công
- Frontend: Build thành công

## Cách test

### Bước 1: Khởi động lại Orders Service
```powershell
# Dừng Orders service hiện tại (Ctrl+C)
# Khởi động lại:
cd src/Services/SelfRestaurant.Orders.Api
dotnet run
```

### Bước 2: Đăng nhập vào Chef Web App
1. Mở trình duyệt: `http://localhost:5200` (hoặc port chef web)
2. Đăng nhập bằng tài khoản chef (ví dụ: `chef_hung`)

### Bước 3: Bắt đầu nấu món
1. Vào trang "Đơn hàng chờ"
2. Chọn một đơn hàng
3. Bấm "Bắt đầu" cho một món ăn
4. **Quan trọng**: Khi bấm "Bắt đầu", hệ thống sẽ:
   - Gọi API: `POST /api/orders/{orderId}/items/{itemId}/chef/start?chefId={chefId}`
   - Lưu `ChefId` vào database
   - Chuyển trạng thái món sang "Đang chuẩn bị"

### Bước 4: Kiểm tra database
```sql
-- Xem OrderItems vừa được cập nhật
SELECT TOP 10 
    ItemID, 
    OrderID, 
    DishID, 
    Quantity, 
    ChefId,
    StatusCode
FROM OrderItems 
WHERE ChefId IS NOT NULL
ORDER BY ItemID DESC;
```

### Bước 5: Kiểm tra Nhật ký nấu ăn
1. Đăng nhập vào Admin Web: `http://localhost:5000`
2. Vào "Quản lý nhân viên"
3. Chọn chef vừa test
4. Bấm "Nhật ký"
5. Xem phần "Nhật ký nấu ăn (Đơn hàng đã hoàn thành)"
6. **Kết quả mong đợi**: Chỉ hiển thị các đơn hàng mà chef đó đã nấu

## Kiểm tra API trực tiếp

### Test endpoint với curl:
```powershell
# Giả sử:
# - OrderID = 19
# - ItemID = 98
# - ChefID = 3

# Start item
curl -X POST "http://localhost:5100/api/orders/19/items/98/chef/start?chefId=3" `
  -H "Content-Type: application/json" `
  -d "{}"

# Check database
sqlcmd -S localhost -d restaurant -Q "SELECT ItemID, OrderID, ChefId FROM OrderItems WHERE ItemID = 98"
```

### Test cooking history API:
```powershell
# Get cooking history for chef ID 3 in branch 1
curl "http://localhost:5001/api/identity/admin/employees/3/history?activityPage=1&cookingPage=1&pageSize=50&days=90"
```

## Troubleshooting

### Vấn đề: ChefId vẫn là NULL sau khi bấm "Bắt đầu"

**Nguyên nhân có thể:**
1. Orders service chưa được khởi động lại với code mới
2. Gateway chưa được khởi động lại
3. Chef web app đang cache code cũ

**Giải pháp:**
```powershell
# 1. Dừng tất cả services
# 2. Build lại
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
powershell -ExecutionPolicy Bypass -File scripts\win-build-sln.ps1

# 3. Khởi động lại từng service
# Orders Service
cd src/Services/SelfRestaurant.Orders.Api
dotnet run

# Gateway (terminal mới)
cd src/Gateway/SelfRestaurant.Gateway.Api
dotnet run

# Chef Web (terminal mới)
cd src/Frontend/selfrestaurant-chef-web
npm run dev
```

### Vấn đề: Nhật ký nấu ăn vẫn hiển thị tất cả đơn hàng

**Nguyên nhân:**
- Các đơn hàng cũ có `ChefId = NULL` nên không được lọc
- Chỉ các đơn hàng mới (sau khi thêm tracking) mới có `ChefId`

**Giải pháp:**
- Tạo đơn hàng mới và test với đơn hàng đó
- Hoặc cập nhật `ChefId` cho dữ liệu cũ bằng SQL:
```sql
-- Gán tất cả OrderItems cũ cho chef ID 3 (ví dụ)
UPDATE OrderItems 
SET ChefId = 3 
WHERE ChefId IS NULL 
  AND OrderID IN (SELECT OrderID FROM Orders WHERE StatusID IN (5, 6));
```

## Xác nhận thành công

Khi test thành công, bạn sẽ thấy:

1. ✅ Database: `ChefId` được lưu khi chef start món
2. ✅ API: Cooking history chỉ trả về đơn hàng của chef đó
3. ✅ Admin UI: Mỗi chef có nhật ký riêng, không lẫn lộn
4. ✅ Pagination: Mỗi loại nhật ký có phân trang độc lập

## Lưu ý quan trọng

- **Dữ liệu cũ**: Các OrderItems đã tồn tại sẽ có `ChefId = NULL`
- **Dữ liệu mới**: Chỉ các món được "start" sau khi deploy code mới mới có `ChefId`
- **Migration tự động**: Orders service sẽ tự động tạo cột `ChefId` khi khởi động (nếu chưa có)
- **Không mất dữ liệu**: Thêm cột mới không ảnh hưởng đến dữ liệu hiện có

## Câu hỏi thường gặp

**Q: Tại sao nhật ký nấu ăn vẫn trống?**
A: Vì chưa có đơn hàng nào được hoàn thành với `ChefId`. Hãy tạo đơn mới và hoàn thành nó.

**Q: Có thể gán ChefId cho dữ liệu cũ không?**
A: Có, nhưng không chính xác vì không biết chef nào đã nấu. Nên để NULL và chỉ track từ bây giờ.

**Q: Làm sao biết code mới đã chạy?**
A: Kiểm tra log khi Orders service khởi động, sẽ thấy dòng "Adding ChefId column to OrderItems table..."
