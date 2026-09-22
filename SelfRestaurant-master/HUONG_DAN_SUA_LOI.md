# Hướng Dẫn Sửa Lỗi - Nhật Ký Đặt Món Khách Hàng

## ❌ Lỗi
Khi vào trang "Nhật ký hoạt động" của khách hàng, xuất hiện lỗi:
```
Unexpected token 'S', 'SelfRestat... is not valid JSON'
```

## ✅ Nguyên nhân
Services backend chưa được khởi động lại với code mới. Tính năng "Nhật ký đặt món" đã được code xong nhưng services đang chạy vẫn là code cũ.

## 🔧 Cách sửa

### Bước 1: Dừng tất cả services
Mở PowerShell và chạy:
```powershell
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
```

### Bước 2: Khởi động lại services

**Cách A: Dùng script tự động (Dễ nhất)**
```powershell
.\restart_services_for_customer_order_history.ps1
```

**Cách B: Khởi động thủ công**

Mở 6 cửa sổ PowerShell riêng biệt và chạy từng lệnh sau:

**Cửa sổ 1 - Identity:**
```powershell
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src/Services/SelfRestaurant.Identity.Api --urls=http://localhost:5100
```

**Cửa sổ 2 - Orders:**
```powershell
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src/Services/SelfRestaurant.Orders.Api --urls=http://localhost:5200
```

**Cửa sổ 3 - Catalog:**
```powershell
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src/Services/SelfRestaurant.Catalog.Api --urls=http://localhost:5300
```

**Cửa sổ 4 - Billing:**
```powershell
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src/Services/SelfRestaurant.Billing.Api --urls=http://localhost:5400
```

**Cửa sổ 5 - Customers:**
```powershell
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src/Services/SelfRestaurant.Customers.Api --urls=http://localhost:5500
```

**Cửa sổ 6 - Gateway:**
```powershell
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src/Gateway/SelfRestaurant.Gateway.Api --urls=http://localhost:7100
```

### Bước 3: Đợi services khởi động
Đợi khoảng 30 giây để tất cả services khởi động hoàn tất.

### Bước 4: Kiểm tra
1. Mở trình duyệt: http://localhost:7100/Admin
2. Đăng nhập admin
3. Vào "Khách hàng"
4. Click "Nhật ký" trên bất kỳ khách hàng nào
5. Bạn sẽ thấy 2 phần:
   - ✅ **Nhật ký hoạt động** (đăng nhập, đăng ký, đổi mật khẩu)
   - ✅ **Nhật ký đặt món** (lịch sử đơn hàng)

## 📋 Tính năng mới

### Nhật ký đặt món hiển thị:
- Mã đơn hàng
- Thời gian đặt
- Bàn
- Trạng thái (Pending, Preparing, Ready, Completed, Cancelled)
- Danh sách món ăn (ví dụ: "2x Phở Bò, 1x Cơm Gà")
- Tổng tiền

### Phân trang độc lập:
- Nhật ký hoạt động: 50 items/trang
- Nhật ký đặt món: 20 items/trang
- Mỗi phần có nút phân trang riêng

## 🔍 Kiểm tra services đang chạy

```powershell
netstat -ano | findstr "5100 5200 5300 5400 5500 7100"
```

Nếu thấy các port này có kết quả → Services đang chạy ✅
Nếu không thấy gì → Services chưa chạy ❌

## ⚠️ Lưu ý

- **Không cần build lại** - Code đã build thành công rồi
- **Chỉ cần khởi động lại services** để load code mới
- Mỗi service cần chạy trong terminal riêng
- Đợi mỗi service khởi động xong (thấy "Now listening on...") trước khi khởi động service tiếp theo

## 📞 Nếu vẫn lỗi

1. **Xem log của Identity service** (cửa sổ 1) - có thể có lỗi kết nối database
2. **Kiểm tra database đang chạy:**
   ```powershell
   sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT @@VERSION"
   ```
3. **Build lại nếu cần:**
   ```powershell
   dotnet clean
   dotnet build
   ```
