# Rà UI/UX app khách, 05/11/2026

Chủ dự án báo màn quét QR bị che và yêu cầu rà toàn bộ. **Chỉ sửa app. Chưa cài lên Samsung, chưa push. Không cần deploy.**

## Cách rà

| Cách | Phạm vi |
|---|---|
| `LayoutAuditTest` (mới, chỉ chạy tay, `-e layoutAudit true`) trên máy ảo `bs_check` | Nạp mọi layout của app ở Sáng/Tối × chữ 100%/130% trên màn 360 dp, xuất ảnh + `report.csv`: chữ bị cắt, chữ đè nhau, nút nhỏ, tràn màn hình, tương phản. Nội dung là chữ mặc định trong XML, không có dữ liệu từ server |
| Xem ảnh 76 màn | Sáng 130% toàn bộ; Tối 100% cho 14 màn đã sửa |
| App thật trên máy ảo, chế độ khách, gọi Production (chỉ đọc) | Trang chủ, Thực đơn, Chi tiết món, Tìm kiếm, Đổi chi nhánh, Đặt bàn bước 1, Quét QR, Nhập mã bàn. Ảnh `screenshots\uiux_guest\` |
| Rà chữ viết sẵn trong layout và code | Tìm dữ liệu giả |
| Samsung | Chỉ dùng để xác nhận lỗi Ngày sinh, ảnh `screenshots\samsung_hoadon\s11_profile.png` chụp trước đợt rà |

Lần đi màn tự động trên Samsung dừng giữa chừng: chủ dự án đang dùng máy, bước tự động không đọc được giao diện khi camera chạy nên chỉ gửi lệnh vuốt/quay lại, không gõ chữ, và chụp nhầm 1 ảnh Zalo. Ảnh đó đã xoá ngay. Từ đó không dùng Samsung nữa.

## Lỗi đã sửa

| Nhóm | Màn | Lỗi | Sửa |
|---|---|---|---|
| Bị che | Quét QR | Tiêu đề 2 dòng + nút đè lên nửa trên khung quét | Tiêu đề 1 dòng, khung quét nằm trong phần dưới tiêu đề, tự thu nhỏ khi thiếu chỗ; bỏ dòng trạng thái trùng với gợi ý |
| Bị che | Quét QR | Ở Sáng, giờ/pin chữ trắng trên nền kem | Biểu tượng theo màu trang |
| Chữ đè | Sửa hồ sơ | Hai gợi ý đè nhau ở ô Ngày sinh | Bỏ gợi ý thừa |
| Chữ đè | Đã gửi đơn | "Món ăn của bạn:" bị ép thành cột 1 chữ | Bỏ nhãn thừa |
| Dữ liệu giả | Hết bàn | "20:00 còn 2 bàn", "VietTable Ba Đình cách 2.5 km", nút báo đã chọn giờ nhưng không làm gì | Viết lại: "Chọn giờ khác" (về bước chọn giờ), "Chọn chi nhánh khác" (danh sách thật) |
| Dữ liệu giả | Tìm kiếm | "Tìm kiếm gần đây" và "Từ khoá phổ biến" viết sẵn, khách mới cũng thấy | Lịch sử thật lưu trên máy, trống thì ẩn; bỏ từ khoá phổ biến vì không có dữ liệu |
| Dữ liệu giả | Chi tiết món đã gọi | "Phân bổ: Bếp Nóng tầng 1 • Bếp phó phụ trách" | Bỏ |
| Dữ liệu giả | Cài đặt | "v5.0.1 (Build 2026)" | Đọc phiên bản thật của app |
| Hứa không có thật | Tiến độ món, Đã gửi đơn, Địa điểm thanh toán | "ước tính thời gian", "Bếp trưởng đã tiếp nhận", "quầy tại tầng 1" | Đổi câu đúng sự thật |
| Chức năng | Kết quả tìm kiếm | Gõ "bun" không ra "Bún" (0 món) | Tìm không dấu: "bun" ra 5 món |
| Bị cắt | Mở bàn, Chia hoá đơn | Dấu − và + bị cắt | Bỏ phần đệm thừa của nút |
| Bị cắt ở chữ 130% | Hoá đơn, Chi tiết đặt bàn, Mở bàn, thẻ chi nhánh | Nút cao cố định làm cắt chữ | Nút cao theo chữ |
| Bị cắt ở chữ 130% | 4 màn | Tiêu đề bị "…" | Rút gọn tiêu đề |
| Dễ bấm | Thẻ món, voucher, tìm kiếm, bản đồ chi nhánh | Nút 36–42 dp | Tối thiểu 44–48 dp |
| Nhất quán | 10 màn cũ | Thanh trên trắng, tiêu đề vàng | Màu trang, tiêu đề tối |
| Nhất quán | Đặt bàn, Kết quả tìm kiếm | "Chi nhánh: Chi nhánh Bình Thạnh" | Bỏ chữ lặp |
| Trình bày | Hoá đơn của tôi | "Tổng thanh toán 35.000đ" xuống 2 dòng | "Tổng cộng 35.000đ" |

## Dòng báo còn lại trong `report.csv`, đã xem, không phải lỗi

- `CLIP_WIDTH`: khoảng trắng cuối dòng khi chữ xuống hàng.
- `CONTRAST` ở `tvDiningBadge`, `tvScanHint`: công cụ đọc màu nền chưa đúng; ảnh hiển thị rõ. `btnSaveTransferQr`: trạng thái đang tắt.
- `SMALL_TARGET` ở nút tròn chọn phương thức: cả thẻ bấm được.
- `CLIP_HEIGHT` ở `fragment_home`: chữ tự thu nhỏ, ảnh Tối 100% hiện đủ.
- `OFFSCREEN` `fragment_qr_scan`: quầng sáng trang trí.
- Biểu tượng đè chữ ở ô đăng nhập chỉ có trong ảnh dựng tĩnh; ô tìm hoá đơn cùng kiểu trên máy thật không bị (`s05_list`). Màn đăng nhập thật chưa xem lại.

## Test

| Lệnh | Kết quả |
|---|---|
| `gradlew :app:assembleDebug :app:assembleCloudPreview :app:assembleCloudProd :app:assembleDebugAndroidTest :app:testDebugUnitTest` | Build OK, 215/215 |
| `LayoutAuditTest` trên máy ảo | OK |
| Quét QR Tối 100%, Sáng 130% trên máy ảo | Khung quét không bị che, `g23_qr`, `g30_qr_light_130` |
| Tìm "bun" trên Production | 5 món, `g31_bun_light_130` |

APK `cloudProd` SHA-256 bắt đầu `D7C18124D1DBCBA1`. **Chưa cài Samsung.**

## Chưa rà

- Web admin, thu ngân, bếp phía sau đăng nhập: mới xem trang đăng nhập rộng 375 px (không cuộn ngang). Mình không có mật khẩu nhân viên.
- Các màn cần tài khoản khách với dữ liệu thật ở chữ 130%: mới rà bằng chữ mặc định và dữ liệu mẫu.
- Quét mã QR thật bằng camera thật trên bản mới; màn "Hết bàn" bấm thử 2 nút trên máy.
- Màn VietQR bản chuyển khoản đang chờ: chưa có phiên thanh toán để xem bằng dữ liệu thật.
