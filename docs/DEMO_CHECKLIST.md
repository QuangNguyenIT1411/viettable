# VietTable — checklist demo local

Mục tiêu: Samsung SM-A528B → `adb reverse tcp:5100 tcp:5100` → Gateway local `:5100` → 5 service. Đây **không** là nghiệm thu public HTTPS. Không reset database/volume, không `pm clear` hoặc uninstall app để chuẩn bị demo.

## Trước buổi demo

1. Sạc laptop và Samsung (ít nhất 25%); chuẩn bị cáp USB, bật USB debugging, cho phép ADB trên điện thoại. Wi-Fi không bắt buộc cho backend local qua USB.
2. Mở Docker Desktop. Chạy từ `D:\Quang\VietTable`:

   ```powershell
   $env:JAVA_HOME='D:\Quang\Android1\jbr'
   .\gradlew.bat :app:assembleDebug :app:testDebugUnitTest '-PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100'
   powershell -ExecutionPolicy Bypass -File .\scripts\demo-install-app.ps1
   ```

   Installer dùng `adb install -r`, giữ app data/session và tạo reverse. Nếu không cần cài lại, bỏ qua bước install.
3. Chuẩn bị một tài khoản customer **riêng cho demo** qua màn Register hoặc API register hợp lệ. Ghi username vào ghi chú cá nhân; không commit password. Để preflight xác minh login, đặt `VIETTABLE_DEMO_USERNAME` và `VIETTABLE_DEMO_PASSWORD` trong PowerShell session hiện tại, rồi xoá biến sau demo. Không dùng tài khoản cá nhân nếu có thể tránh.
4. Chuẩn bị tài khoản Cashier đúng chi nhánh và mở `http://localhost:5100/app/cashier/Staff/Account/Login` trong browser. ONSITE chỉ thành công sau thao tác xác nhận thu tiền của Cashier; Android không tự đánh dấu thành công.
5. Chạy `powershell -ExecutionPolicy Bypass -File .\scripts\demo-find-table.ps1` ngay trước khi chọn bàn. Script chỉ đọc Catalog + Orders và loại bàn có session/order đang active. Không xem mã QR in sẵn là luôn trống; chọn lại khi fixture thay đổi. Nếu muốn đặt bàn, kiểm tra availability đúng ngày/giờ trong app.
   Chạy `powershell -ExecutionPolicy Bypass -File .\scripts\demo-fixture-audit.ps1` để biết số phiên, order, payment PENDING, split bill OPEN và reservation sắp tới. Số lớn không đồng nghĩa lỗi toàn hệ thống; tuyệt đối không xoá SQL để làm số về 0.
6. Với hàng đã chọn, dùng cột `QrCode` để nhập tay. Muốn demo camera, hiển thị QR mã đó trên **màn hình khác/giấy**, không phải ngay trên Samsung. QR là artifact kiểm thử, không phải fallback trong app.

Snapshot lúc chuẩn bị (2026-09-23): helper đề xuất **Chi nhánh Quận 1, bàn 5, `QR-Q1-B05`**; Gateway `GET /api/mobile/v1/qr/QR-Q1-B05` đã trả đúng branch/table. Đây là snapshot, **không** phải bàn được giữ chỗ; bắt buộc chạy lại helper trước buổi demo.

## Năm phút trước demo

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\demo-preflight.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\demo-find-table.ps1
```

Preflight kiểm tra Docker, 8 container, 6 `/readyz`, SQL `SELECT 1`, RabbitMQ ping, Samsung, reverse, APK, pin, launch và AndroidRuntime sau launch. Exit code khác 0 là chưa sẵn sàng. Nó không tạo account, reservation, session hoặc payment. `Demo login MANUAL` nghĩa là chưa cung cấp credential qua biến môi trường; đăng nhập thử trên app trước khi lên trình bày.

## Trong demo (8–12 phút)

1. Login → Home → Branch/Menu (dữ liệu qua Gateway).
2. Quét QR của bàn trống đã xác minh, hoặc nhập mã bàn thủ công → validate → walk-in/check-in → Dining Session. Reservation có thể trình bày riêng nếu slot/bàn thực sự phù hợp.
3. Thêm món → Cart → gửi Order → Order Tracking.
4. Gọi nhân viên → xem service request; nếu realtime không đến ngay, refresh REST.
5. Checkout quote → ONSITE PENDING → Cashier web xác nhận thu tiền → Android cập nhật SUCCEEDED → Invoice → Review.
6. Nếu còn thời gian: Split Bill, FAQ, Favorites hoặc Notifications.

## Khi có lỗi

| Tình huống | Fallback đúng nghiệp vụ |
|---|---|
| Camera/permission | Nhập đúng `QrCode` từ `demo-find-table.ps1`. |
| Bàn occupied | Chạy lại helper, chọn bàn khác; không sửa SQL. |
| Tài khoản có reservation lệch bàn hoặc active session | Dùng đúng reservation/QR của tài khoản đó, hoàn thành phiên qua luồng nghiệp vụ hoặc dùng demo customer sạch. Không fake session. |
| SignalR chậm | REST là nguồn trạng thái chuẩn; refresh Order/Service Request. |
| ONSITE chưa thành công | Kiểm tra đúng Cashier/chi nhánh và trạng thái PENDING; chỉ xác nhận khi giả lập thao tác thu tiền thật trong buổi demo. Không dùng VNPAY. |
| Docker/API lỗi | Chạy preflight, xem container/log liên quan; không `docker compose down -v`. |

Trạng thái bên ngoài phạm vi demo: Google OAuth E2E cần cấu hình Google Cloud; VNPAY external cần merchant credentials/callback public; production HTTPS cần server/domain thật. Không trình bày ba mục này là đã nghiệm thu.
