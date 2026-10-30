# Test trên Samsung SM-A528B — 30/10/2026 16:03–16:11

APK `cloudProd` code `32d61c4` (SHA-256 bắt đầu `3E2474966397E0B1`, cài 16:01:23), trỏ Production
`https://viettable-beta.vercel.app` (`dpl_Au5pSzmrbi3dskk59tvHRTRZQX3y`). Tài khoản khách của chủ dự án (đăng nhập sẵn, "Chào Vinh,").
Dùng phiên có sẵn Bàn 2 Quận 1 (phiên #46, mở 28/10 08:44, đơn #60) theo lựa chọn của chủ dự án. Bếp/thu ngân: trình duyệt trong
app Claude, chủ dự án tự đăng nhập chef_hung rồi cashier_lan. Điều khiển máy qua `adb` (chụp, bấm, đọc giao diện).
Ảnh: `screenshots/e2e_samsung_20261030/`.

| # | Bước | Kết quả | Ảnh |
|---|---|---|---|
| 1 | Mở app | ĐẠT: Trang chủ, banner "blackday — Giảm 50% (tối đa 50.000đ) cho hóa đơn từ 300.000đ", thẻ Bàn 2 "Đang dùng bữa" | `01_launch.png` |
| 2 | Tiến độ món trước khi nấu | "BÀN 2 · ĐƠN #60 · NHÀ HÀNG XÁC NHẬN", 0/1 | `03_progress_before_cook.png` |
| 3 | Bếp "Bắt đầu nấu" đơn #60 | ĐẠT: Samsung tự đổi "ĐANG XỬ LÝ", món "Đang nấu" (realtime) | `04_progress_cooking.png` |
| 4 | Bếp "Hoàn thành" đơn #60 | ĐẠT (bản sửa `32d61c4`): "BÀN 2 · ĐƠN #60 · HOÀN THÀNH", "Món đã lên đủ" 1/1, thanh 4 bước sáng đủ | `05_progress_done.png` |
| 5 | Hoá đơn | ĐẠT: 35.000đ, không có khuyến mãi (dưới 300.000đ) | `06_invoice.png` |
| 6 | Yêu cầu thanh toán tiền mặt tại quầy | ĐẠT: "Đang chờ thanh toán 35.000đ", Tại quầy thu ngân (Bàn 2), Tiền mặt | `07_waiting_cash.png` |
| 7 | Thu ngân "Xác nhận đã thu tiền" | ĐẠT: giao dịch Bàn #2 35.000đ → "Đã thu tiền" | trang Thu ngân |
| 8 | Màn thanh toán thành công | ĐẠT (bản sửa `eb0c5b3`): "Đã thanh toán 35.000đ · Tiền mặt", Hoá đơn #44, Phương thức "Tiền mặt", "Bàn 2 • Chi nhánh Quận 1", "+35 điểm tích lũy mới!" | `08_success.png`, `09_success_scrolled.png` |
| 9 | Về Trang chủ | ĐẠT: thẻ Bàn 2 biến mất (phiên đóng) | `10_home_after.png` |

## Ghi chú

- Lỗi trình bày nhỏ (chưa sửa): ở màn thành công trên Samsung, "Bàn 2 • Chi nhánh Quận 1" xuống 3 dòng, chữ "1" đứng riêng.
- `uiautomator dump` báo "could not get idle state" trên màn thành công (vòng xoay chạy liên tục); đọc bằng ảnh chụp.
- Dữ liệu thay đổi: phiên #46 (Bàn 2) của chủ dự án đã đóng, hoá đơn #44 35.000đ, tài khoản chủ dự án +35 điểm.
