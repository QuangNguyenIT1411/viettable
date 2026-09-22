# Trạng thái tích hợp frontend Android

## Token security / HTTPS (2026-09-22)

Đã chuyển access/refresh token sang AndroidKeyStore AES-GCM, migrate session hiện có bằng install -r trên Samsung, không còn hai legacy plaintext keys. REST profile và SignalR đã chạy bằng secure session. 27 unit tests và 4 device tests PASS; login/refresh đồng thời/logout/relogin kiểm tra bằng tài khoản/kho token riêng.

Debug giữ local HTTP qua ADB reverse; release bắt buộc HTTPS origin cấu hình rõ, không fallback localhost. Release build + merged manifest/resource audit PASS với URL chỉ dùng build validation. Chưa có production HTTPS endpoint/certificate để test HTTPS/WSS E2E. Chi tiết, giới hạn và lệnh chạy lại: [ANDROID_SECURITY.md](ANDROID_SECURITY.md).

Android app đã chuyển từ dữ liệu mock sang Gateway mobile API v1 cho các nhóm chức năng chính:

- Auth: đăng ký, đăng nhập mật khẩu, Google Sign-In qua Credential Manager và Identity B1, refresh token, đăng xuất, quên/đặt lại mật khẩu.
- Catalog: chi nhánh, menu, ảnh món, yêu thích.
- Customer: hồ sơ, thông báo, voucher, cài đặt, hỗ trợ.
- Reservation: kiểm tra bàn trống, tạo, danh sách, đổi, hủy và pre-order.
- Dining: xác thực QR, check-in, phiên bàn, gọi món, theo dõi món, yêu cầu phục vụ.
- Realtime: Android kết nối Hub `/hubs/mobile-notifications` bằng JWT, nhận `orderUpdated` và `serviceRequestUpdated`, rồi đồng bộ lại dữ liệu chuẩn qua REST. Polling chỉ chạy dự phòng khi Hub mất kết nối.
- Billing/engagement: quote, voucher, điểm, payment request, payment polling, hóa đơn, FAQ từ B7 và Review create/read/update/delete.

Gateway mặc định khi chạy emulator là `http://10.0.2.2:5100`. Có thể ghi đè lúc build bằng Gradle property `VIETTABLE_API_BASE_URL`. Script `test-integration.ps1 -InstallApk` tự dùng `adb reverse` và `http://127.0.0.1:5100` cho điện thoại USB.

Các giới hạn môi trường còn lại:

- Camera QR dùng CameraX + ML Kit; nhập mã bàn thủ công vẫn được giữ làm phương án thay thế.
- Thanh toán ngân hàng cần provider/webhook bên ngoài để chuyển trạng thái sang `SUCCEEDED`.
- Review được tạo theo `invoiceId` do backend phát hành; danh sách, sửa và xóa luôn tải/lưu qua Gateway, không dùng state local làm nguồn dữ liệu.
- Đã cài và chạy trên Samsung SM-A528B qua ADB; màn chi nhánh và menu đã tải dữ liệu thật từ Gateway.
