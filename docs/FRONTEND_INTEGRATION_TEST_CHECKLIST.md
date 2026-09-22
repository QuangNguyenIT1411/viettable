# Checklist test tích hợp Android ↔ backend

## 1. Kiểm tra tự động

Điều kiện: Docker Desktop đang chạy và các container backend đã được khởi động.

```powershell
cd D:\Quang\VietTable
powershell -ExecutionPolicy Bypass -File .\scripts\test-integration.ps1
```

Kết quả cần thấy: `Integration checks passed.`

Nếu emulator hoặc điện thoại USB đã kết nối, build và cài APK trong một lệnh. Với điện thoại thật, script tự tạo `adb reverse` và build app dùng `127.0.0.1:5100`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-integration.ps1 -InstallApk
```

APK debug nằm tại `D:\Quang\VietTable\app\build\outputs\apk\debug\app-debug.apk`.

## 2. Chuẩn bị test UI

- [ ] Docker Desktop báo Engine running.
- [ ] `http://localhost:5100/health` trả HTTP 200 trên Windows.
- [ ] Android Emulator dùng `http://10.0.2.2:5100` để gọi Gateway trên máy Windows.
- [ ] Điện thoại USB dùng `adb reverse tcp:5100 tcp:5100` và app build với `http://127.0.0.1:5100`; giữ cáp USB trong lúc test.

Nếu rút/cắm lại cáp, khởi động lại điện thoại hoặc ADB, chỉ cần phục hồi kết nối (không cần build lại):

```powershell
cd D:\Quang\VietTable
powershell -ExecutionPolicy Bypass -File .\scripts\connect-phone.ps1 -OpenApp
```

## 3. Tài khoản và hồ sơ

- [ ] Đăng ký tài khoản mới bằng email duy nhất và số điện thoại Việt Nam 10 chữ số.
- [ ] Đăng nhập thành công, đóng/mở app vẫn giữ phiên.
- [ ] Nút Google mở account picker; cancel không tạo session và vẫn đăng nhập mật khẩu được.
- [ ] Google account mới nhận session VietTable; email đã đăng ký bằng mật khẩu phải hiện lỗi conflict thật từ backend.
- [ ] Sửa họ tên/email/số điện thoại, mở lại trang cá nhân thấy dữ liệu đã lưu.
- [ ] Đăng xuất rồi xác nhận màn cần đăng nhập không còn truy cập được.
- [ ] Quên mật khẩu → đặt mật khẩu mới → đăng nhập bằng mật khẩu mới.

## 4. Chi nhánh, menu và tương tác khách hàng

- [ ] Danh sách 3 chi nhánh tải từ backend.
- [ ] Chọn chi nhánh và thấy menu, giá, hình ảnh từ backend.
- [ ] Thêm/bỏ món yêu thích; đóng mở màn hình vẫn giữ đúng trạng thái.
- [ ] Mở thông báo và đánh dấu đã đọc.
- [ ] Voucher hiển thị từ tài khoản; mã nhập tay không tồn tại phải bị từ chối.
- [ ] Thay đổi cài đặt thông báo/ngôn ngữ rồi mở lại màn hình để kiểm tra đã lưu.
- [ ] Mở Hỗ trợ, xác nhận FAQ tải từ backend; khi API lỗi phải có thông báo và nút thử lại, tải lại không nhân đôi câu hỏi.
- [ ] Mở Đánh giá của tôi, sửa/xóa một review và tải lại để xác nhận dữ liệu canonical từ backend.

## 5. Đặt bàn và pre-order

- [ ] Chọn ngày động trong 7 ngày tới, giờ và số khách.
- [ ] Màn chọn bàn chỉ hiển thị bàn phù hợp còn trống do backend trả về.
- [ ] Xác nhận đặt bàn và thấy reservation mới trong lịch sử.
- [ ] Đổi ngày/giờ/số khách; tải lại chi tiết và thấy giá trị mới.
- [ ] Thêm món pre-order và gửi; mở lại reservation thấy đã có pre-order.
- [ ] Hủy reservation và xác nhận trạng thái `Đã hủy` từ backend.

## 6. Check-in, gọi món và phục vụ

- [ ] Quét camera hoặc nhập mã QR bàn hợp lệ; QR sai phải hiện lỗi từ backend.
- [ ] Check-in reservation hoặc walk-in tạo đúng một dining session.
- [ ] Gửi một vòng món; danh sách đơn hiện mã và trạng thái backend.
- [ ] Khi SignalR connected, cập nhật trạng thái bếp tới màn theo dõi mà không polling liên tục và không tự giả lập trạng thái.
- [ ] Dừng Gateway rồi mở lại: app chuyển sang polling dự phòng, tự reconnect và REST resync khi Gateway phục hồi.
- [ ] Chuyển qua lại giữa các màn dining/order không tạo nhiều SignalR connection; background/foreground chỉ kết nối lại một lần.
- [ ] Gửi yêu cầu gọi nhân viên và mở lịch sử thấy yêu cầu từ backend.
- [ ] Hủy một yêu cầu còn `REQUESTED`; tải lại thấy trạng thái `ĐÃ HỦY`.

## 7. Thanh toán

- [ ] Chỉ được tạo báo giá khi các món đã ở trạng thái cho phép thanh toán.
- [ ] Áp voucher/điểm; tổng tiền phải lấy từ báo giá server, không tự tính ở app.
- [ ] Tạo VietQR hoặc yêu cầu thanh toán tại quầy/bàn; app hiển thị `PENDING` và tự kiểm tra trạng thái.
- [ ] Sau khi backend/provider ghi nhận `SUCCEEDED`, app mới mở màn thành công và hóa đơn.
- [ ] Gửi đánh giá sau hóa đơn; mở lại Đánh giá của tôi để xem, sửa, xóa và xác nhận thay đổi vẫn đúng sau reload.
- [ ] Gửi yêu cầu hỗ trợ.

Lưu ý: backend hiện không tích hợp cổng thanh toán ngân hàng thật. Vì vậy giao dịch online không tự thành công nếu chưa có webhook/provider test cập nhật trạng thái; app cố ý không có nút “giả lập thành công”.

## 8. Token security / release policy (kiểm chứng 2026-09-22)

- [x] Upgrade Samsung bằng install -r, không clear data: legacy keys mất sau secure verify, phiên cũ còn hoạt động.
- [x] Force-stop/cold launch: secure session tồn tại, authenticated profile và SignalR hoạt động.
- [x] Controlled real Keystore: key không export, IV thay đổi, corruption/key loss fail closed, clear/logout/relogin.
- [x] Isolated Gateway account: password login, 4 REST 401 đồng thời, refresh rotation/retry; late REST/SignalR refresh caller không rotate lại.
- [x] 27 unit tests + 4 instrumented security/runtime tests PASS; không UTP/uninstall main app.
- [x] Release HTTPS build-only config PASS; thiếu URL/HTTP/local hosts bị reject. Merged manifest và packaged network config đã kiểm tra.
- [x] Static audit: không TrustAll/token logging/plaintext store ngoài migration; backup loại trừ hai auth stores.
- [ ] HTTPS/WSS E2E với production Gateway và certificate thật (external deployment chưa có).
- [ ] Cloud/device-transfer restore thực tế; hiện đã kiểm tra rules và controlled missing-key restore case.

Chi tiết cách chạy an toàn, test account side effect và giới hạn: [ANDROID_SECURITY.md](ANDROID_SECURITY.md).

## 9. Android instrumentation regression (2026-09-22)

- [x] Cold launch signed-out → Welcome/Login; authenticated → Home.
- [x] Password login hợp lệ, invalid backend error và logout qua UI bằng test account/store riêng.
- [x] Branch/menu thật non-empty; background/foreground giữ canonical list và SignalR reconnect.
- [x] Home → Profile → FAQ; question/answer backend non-empty, reload không duplicate.
- [x] Camera denied vẫn nhập QR; QR sai scan lại; QR thật qua Gateway tới walk-in confirmation; recreate giữ input và không tạo session giả.
- [x] SecureTokenStore/refresh concurrency device tests tiếp tục PASS.
- [x] 27 unit tests PASS; Samsung runner `OK (11 tests)`: 10 PASS + 1 existing-session test opt-in skipped.
- [x] Script dùng install-r, không clear/uninstall, trả exit code và thu diagnostics khi fail.
- [ ] Camera QR vật lý, Google chooser và VNPAY external acceptance.
- [ ] Reservation → dining → order → service → checkout → split/review automation, chờ fixture lifecycle/cleanup API công khai; tiếp tục kiểm tra manual, không fake state.

Kiến trúc tier, lệnh chạy, cleanup và flaky policy: [ANDROID_TESTING.md](ANDROID_TESTING.md).
