# Khách chưa đăng nhập bấm vào mục cần tài khoản — 07/11/2026

Chủ dự án (07/11): chưa đăng nhập mà bấm vào "Thông tin" thì không hiện gì; phải báo chưa đăng nhập và có chỗ đăng nhập.

## Tìm thấy trên Samsung (chế độ "Khám phá ngay")

- Tôi → Thông tin cá nhân (và các mục cần tài khoản khác): màn trống.
- Tab Tôi: tên trống, thẻ điểm trống, nút "Đăng xuất tài khoản".
- Đơn hàng → Lịch sử phiên / Hóa đơn: "Access token không hợp lệ hoặc đã hết hạn." + "Thử tải lại".

## Sửa (`7d6843a`, chỉ app)

- Màn mới **Bạn chưa đăng nhập** ("Đăng nhập để xem …", nút **Đăng nhập tại đây** → màn Đăng nhập, **Chưa có tài khoản? Tạo tài khoản**).
- Đánh dấu 11 màn cần tài khoản (`RequiresLogin`): thông tin cá nhân, thẻ thành viên, voucher của tôi, lịch sử đặt bàn,
  hoá đơn của tôi, đánh giá của tôi, danh sách chờ, Hỗ trợ của tôi, bắt đầu chat, khung chat, thông báo. `MainActivity.navigateTo`
  thay bằng màn trên khi không có phiên đăng nhập.
- Tab Tôi khi chưa đăng nhập: "Bạn chưa đăng nhập", ẩn thẻ điểm, nút dưới thành **Đăng nhập tại đây** (màu vàng).
- Đơn hàng → Lịch sử phiên / Hóa đơn khi chưa đăng nhập: "Bạn chưa đăng nhập. Đăng nhập để xem …" + **Đăng nhập tại đây**; không gọi máy chủ.
- Unit test 327/327 (thêm `LoginRequiredMessageTest`), 3 bản build đạt.

## Samsung SM-A528B

- `adb install -r` APK `cloudProd` SHA-256 bắt đầu `1BC8108788A416E0` (bản trước trong ngày: `25B46548…`, 01:16:39).
- Chế độ khách, chi nhánh Bình Thạnh: tab Tôi đúng (`screenshots\keyboard_fix\guest_toi_after.png`); Thông tin cá nhân →
  "Bạn chưa đăng nhập / Đăng nhập để xem thông tin cá nhân." (`guest_thongtin_after.png`), bấm **Đăng nhập tại đây** mở màn
  Đăng nhập; Đơn hàng → Lịch sử phiên đúng (`guest_lichsu_after.png`); chuông → "Đăng nhập để xem thông báo của bạn.".
- Máy để ở chế độ khách (không đăng nhập), giao diện Tối.

## GUI_BAN

- `VietTable.apk` = `1BC8108788A416E0`; `VietTable_source.zip` tạo lại từ `7d6843a` (1956 mục); hướng dẫn mục 3.14 thêm đoạn
  "Chưa đăng nhập"; `GUI_BAN.zip` nén lại (46,3 MB).
