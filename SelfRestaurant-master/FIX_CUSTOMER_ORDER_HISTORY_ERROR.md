# Fix: Customer Order History Error

## Lỗi hiện tại
```
Unexpected token 'S', 'SelfRestat... is not valid JSON'
```

## Nguyên nhân
Services chưa được khởi động lại với code mới. API endpoint `/api/gateway/admin/customers/{customerId}/order-history` chưa tồn tại trong services đang chạy.

## Giải pháp

### Cách 1: Sử dụng script tự động (Khuyến nghị)

1. Chạy script khởi động lại services:
```powershell
.\restart_services_for_customer_order_history.ps1
```

2. Đợi khoảng 30 giây để tất cả services khởi động

3. Mở trình duyệt và truy cập: http://localhost:7100/Admin

4. Đăng nhập và vào "Khách hàng" → Click "Nhật ký" trên bất kỳ khách hàng nào

### Cách 2: Khởi động thủ công

1. **Dừng tất cả services đang chạy:**
```powershell
Get-Process -Name "SelfRestaurant.*" -ErrorAction SilentlyContinue | Stop-Process -Force
```

2. **Khởi động từng service (mở terminal riêng cho mỗi service):**

Terminal 1 - Identity:
```powershell
dotnet run --project src/Services/SelfRestaurant.Identity.Api --urls=http://localhost:5100
```

Terminal 2 - Orders:
```powershell
dotnet run --project src/Services/SelfRestaurant.Orders.Api --urls=http://localhost:5200
```

Terminal 3 - Catalog:
```powershell
dotnet run --project src/Services/SelfRestaurant.Catalog.Api --urls=http://localhost:5300
```

Terminal 4 - Billing:
```powershell
dotnet run --project src/Services/SelfRestaurant.Billing.Api --urls=http://localhost:5400
```

Terminal 5 - Customers:
```powershell
dotnet run --project src/Services/SelfRestaurant.Customers.Api --urls=http://localhost:5500
```

Terminal 6 - Gateway:
```powershell
dotnet run --project src/Gateway/SelfRestaurant.Gateway.Api --urls=http://localhost:7100
```

3. **Khởi động Frontend (terminal riêng):**
```powershell
cd src/Frontend/selfrestaurant-admin-web
npm run dev
```

### Cách 3: Kiểm tra nhanh

Sau khi khởi động services, kiểm tra endpoint:

```powershell
# Test Identity service
Invoke-WebRequest -Uri "http://localhost:5100/health" -UseBasicParsing

# Test Gateway
Invoke-WebRequest -Uri "http://localhost:7100/health" -UseBasicParsing
```

## Kiểm tra tính năng mới

1. Đăng nhập admin panel: http://localhost:7100/Admin
   - Username: `admin`
   - Password: (mật khẩu admin của bạn)

2. Vào menu "Khách hàng"

3. Click nút "Nhật ký" trên bất kỳ khách hàng nào

4. Bạn sẽ thấy 2 phần:
   - **Nhật ký hoạt động**: Login, register, đổi mật khẩu
   - **Nhật ký đặt món**: Lịch sử đơn hàng với món ăn

## Các endpoint mới

### Backend
- `GET /api/internal/customers/{customerId}/order-history` (Identity service)
- `GET /api/gateway/admin/customers/{customerId}/order-history` (Gateway)

### Frontend
- Updated: `/Admin/Customers/{customerId}/Activity-Logs`
- Hiển thị cả activity logs và order history

## Nếu vẫn lỗi

1. **Kiểm tra services đang chạy:**
```powershell
netstat -ano | findstr "5100 5200 5300 5400 5500 7100"
```

2. **Xem logs của Identity service:**
   - Tìm terminal đang chạy Identity service
   - Xem có lỗi gì không

3. **Build lại nếu cần:**
```powershell
dotnet build --no-incremental
```

4. **Xóa cache và build lại:**
```powershell
dotnet clean
dotnet build
```

## Thông tin thêm

- Code đã được build thành công (backend + frontend)
- Tất cả thay đổi đã được commit
- Chỉ cần khởi động lại services để áp dụng code mới
