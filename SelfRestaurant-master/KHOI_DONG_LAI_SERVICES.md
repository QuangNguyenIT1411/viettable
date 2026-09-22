# HƯỚNG DẪN KHỞI ĐỘNG LẠI SERVICES

## ⚠️ LỖI ĐÃ SỬA
Đã sửa lỗi endpoint path trong Gateway. Code đã được build lại thành công.

## 🚀 CÁCH KHỞI ĐỘNG (Chọn 1 trong 2 cách)

### CÁCH 1: Dùng file .cmd (ĐƠN GIẢN NHẤT)

1. **Mở Command Prompt** (cmd) trong thư mục dự án
2. **Chạy lệnh:**
   ```cmd
   start_all_services.cmd
   ```
3. **Đợi 30 giây** để tất cả services khởi động
4. **Mở trình duyệt:** http://localhost:7100/Admin

### CÁCH 2: Khởi động thủ công

**Bước 1: Dừng tất cả process cũ**
```powershell
taskkill /F /IM dotnet.exe
```

**Bước 2: Mở 6 cửa sổ Command Prompt và chạy từng lệnh:**

**Cửa sổ 1:**
```cmd
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src\Services\SelfRestaurant.Identity.Api --urls=http://localhost:5100
```

**Cửa sổ 2:**
```cmd
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src\Services\SelfRestaurant.Orders.Api --urls=http://localhost:5200
```

**Cửa sổ 3:**
```cmd
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src\Services\SelfRestaurant.Catalog.Api --urls=http://localhost:5300
```

**Cửa sổ 4:**
```cmd
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src\Services\SelfRestaurant.Billing.Api --urls=http://localhost:5400
```

**Cửa sổ 5:**
```cmd
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src\Services\SelfRestaurant.Customers.Api --urls=http://localhost:5500
```

**Cửa sổ 6:**
```cmd
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
dotnet run --project src\Gateway\SelfRestaurant.Gateway.Api --urls=http://localhost:7100
```

## ✅ KIỂM TRA

1. Đợi đến khi thấy dòng "Now listening on: http://localhost:7100" trong cửa sổ Gateway
2. Mở trình duyệt: http://localhost:7100/Admin
3. Đăng nhập admin
4. Vào **Khách hàng** → Click **Nhật ký** trên bất kỳ khách hàng nào
5. Bạn sẽ thấy:
   - ✅ **Nhật ký hoạt động** (đăng nhập, đăng ký)
   - ✅ **Nhật ký đặt món** (lịch sử đơn hàng với món ăn)

## 🔍 KIỂM TRA SERVICES ĐANG CHẠY

```cmd
netstat -ano | findstr ":5100 :5200 :5300 :5400 :5500 :7100"
```

Nếu thấy các port này → Services đang chạy ✅

## ❌ NẾU VẪN LỖI

### Lỗi: "Cannot connect to database"
```cmd
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT @@VERSION"
```
Nếu lỗi → Khởi động SQL Server LocalDB

### Lỗi: "Port already in use"
```cmd
taskkill /F /IM dotnet.exe
```
Sau đó khởi động lại

### Lỗi: "File not found"
Đảm bảo bạn đang ở đúng thư mục:
```cmd
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main
```

## 📝 THAY ĐỔI ĐÃ SỬA

1. ✅ Sửa endpoint path trong `CustomersClient.cs`
   - Cũ: `/api/internal/customers/...`
   - Mới: `/api/identity/internal/customers/...`

2. ✅ Build lại Gateway và Identity service

3. ✅ Tạo script khởi động tự động

## 🎯 KẾT QUẢ MONG ĐỢI

Sau khi khởi động lại, trang "Nhật ký hoạt động" sẽ hiển thị:

1. **Thông tin khách hàng** (tên, tài khoản, email, SĐT)
2. **Nhật ký hoạt động** với bảng:
   - Thời gian
   - Hoạt động (Đăng nhập, Đăng ký, Đổi mật khẩu)
   - IP Address
   - Ghi chú
3. **Nhật ký đặt món** với bảng:
   - Mã đơn
   - Thời gian
   - Bàn
   - Trạng thái
   - Món ăn (ví dụ: "2x Phở Bò, 1x Cơm Gà")
   - Tổng tiền

## 📞 HỖ TRỢ

Nếu vẫn gặp lỗi, hãy:
1. Chụp màn hình lỗi
2. Copy log từ cửa sổ Gateway (cửa sổ 6)
3. Copy log từ cửa sổ Identity (cửa sổ 1)
