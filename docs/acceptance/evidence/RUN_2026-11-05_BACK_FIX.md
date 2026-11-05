# Back ở màn xác nhận đặt bàn, câu chữ trạng thái, màn Thay đổi đặt bàn, 05/11/2026 (tối)

Chủ dự án duyệt trước: sửa câu chữ + Back, việc giao diện ưu tiên thấp, cài APK Samsung, test Back, để máy chế độ Tối, push.

## Code

| Commit | Nội dung |
|---|---|
| `ce218dd` | Back ở "Đặt bàn thành công" (mọi trạng thái) = "Về trang chủ" (`switchTab` xoá back stack, không còn về bước 5). "Bị từ chối" không có lý do: chỉ "Bạn có thể đặt giờ khác." (không còn "Lý do từ chối: …"), áp cho cả màn Chi tiết đặt bàn. "Quá giờ": "Bạn chưa đến trong thời gian giữ bàn. Bạn có thể đặt bàn mới khi cần." Test cập nhật trong `ReservationScheduleTest`, `ReservationConfirmationViewTest` |
| `40b937d` | Màn "Thay đổi đặt bàn" làm lại theo Phố đêm (nút tròn quay lại, nhãn mã đặt bàn, tiêu đề lớn, chip ngày/giờ, bộ đếm khách như Chi tiết món, nút vàng mũi tên cố định dưới), giữ nguyên ID. Màn Chào mừng: bóng đĩa không còn bị cắt thành khung vuông ở chế độ Sáng |

## Test

| Bước | Kết quả |
|---|---|
| `gradlew :app:assembleDebug :app:assembleCloudPreview :app:assembleCloudProd :app:testDebugUnitTest` | 3 bản build OK, 201/201 unit test |
| Máy ảo `bs_check`, test xem trước (dữ liệu mẫu) ở chế độ Sáng: Tiến độ món, Đặt cọc, VietQR, Hoá đơn, Xác nhận đặt bàn | Đọc rõ, QR nền trắng. Ảnh `screenshots\light\p_light_*.png`. Check-in và Chia hoá đơn không có trong bộ xem trước, chưa chụp |
| Cài APK `cloudProd` lên Samsung SM-A528B | SHA-256 bắt đầu `BDFB6921791BC686`, `dumpsys` lastUpdateTime 20:39:09 05/11/2026 |
| Đặt bàn #34 → màn xác nhận → Back | Về Trang chủ; Back lần nữa thoát app (không còn bước form). Ảnh `screenshots\samsung_back\f01`, `f02` |
| Màn Thay đổi đặt bàn (#34) ở Tối và Sáng | Đạt. Ảnh `f03`–`f06` |
| Dọn | #34 đã huỷ; máy về chế độ Tối qua Cài đặt (`appearance.xml` = dark), máy bay tắt. Ảnh `f07_home_dark_final.png` |
