# Giao diện Sáng ("Phố ngày") + vá ghi chú đặt bàn, 05/11/2026

Chủ dự án duyệt trước cả chuỗi (05/11): làm A + B, test máy ảo, deploy backend, cài APK Samsung, test Samsung, push.

## Code

| Commit | Nội dung |
|---|---|
| `62bf532` | Backend: đổi lịch thiếu trường `note` thì giữ ghi chú cũ (`CustomersController.UpdateReservation`), test `ReservationNoteTests` (3 ca) |
| `246d0c8` … `5908e97` | App: token màu theo vai trò ở `values/` (Sáng) và `values-night/` (Tối, giữ nguyên giá trị cũ); Cài đặt → Giao diện (Theo hệ thống / Sáng / Tối, mặc định Tối, lưu cục bộ, áp ngay); icon thanh hệ thống theo chế độ; sửa chữ trên nền tối cố định, chữ vàng ở các màn cũ |

## Test trên máy

| Lệnh / bước | Kết quả |
|---|---|
| CloudHost tests (container `viettable-cloudhost-sdk`) | 304/304 đạt |
| Contract tests | 185 đạt, 1 bỏ qua |
| Image Production (`prepare-staff-preview.ps1` + `docker build`) | `viettable-prod:prepared` `7e35b14f83f6`, shared 30/30, bundle 3/3 |
| `gradlew :app:assembleDebug :app:assembleCloudPreview :app:assembleCloudProd :app:testDebugUnitTest` | 3 bản build OK, 201/201 unit test (gồm `ThemeStoreTest` 4 ca) |
| Máy ảo `bs_check` (chưa đăng nhập) | Sáng/Tối × chữ 100%/130%: Chào mừng, Đăng nhập, Đăng ký, Trang chủ, Thực đơn, Chi tiết món, Giỏ. Ảnh `screenshots\light\`, ảnh ghép `screenshots\light\SO_SANH_light.png` |

## Deploy

- `dpl_BdmHSffuXEKQLvpKCCA4G9bMDHNA` (`https://viettable-fogkeh6rp-viet-table.vercel.app`), production, `[syd1]`, Ready, alias `viettable-beta.vercel.app`, tạo 16:53 05/11. Đã xoá `.env.local` sau `vercel link`.
- Gọi 1 lần: `/healthz` 200 2,3 s, `/readyz` 200 1,0 s, `/` 200, `/app/cashier/` 200, `/app/chef/` 200, `/app/admin/` 200, `/api/mobile/v1/branches` 200.
- Bản quay lui: `dpl_4cyEEiaozzC1ymgfyvf6ixmqspCj`.

## Samsung SM-A528B (`R5CRC14LFYD`), tài khoản đang đăng nhập

APK `cloudProd` cuối: SHA-256 bắt đầu `2DA34C5526C5FB29`, `adb install -r`, `dumpsys` lastUpdateTime 20:14:40 05/11/2026.

| Bước | Kết quả | Ảnh (`screenshots\samsung_light\`) |
|---|---|---|
| a. Giao diện Sáng → Tối → Theo hệ thống, đổi ngay; tắt mở app vẫn nhớ | Đạt (sửa thêm: nhãn "Theo hệ thống" không cập nhật khi màn không tạo lại, `5908e97`) | `s04`–`s08` |
| b. Chế độ Sáng: Trang chủ, Thực đơn, Chi tiết món, Giỏ, Đặt bàn 5 bước, Thông báo, Tôi, Thẻ thành viên, Hoá đơn | Đạt sau khi sửa chữ vàng ở Chi tiết hoá đơn và các màn cũ | `b01`–`b14`, `c02`–`c09` |
| c. Đặt bàn #32 có ghi chú → chỉ đổi giờ 19:00 → 20:30 → ghi chú còn; Back về xác nhận → 20:30; huỷ → "Đã huỷ đặt bàn" | Đạt | `c09`–`c21` |
| d. Chế độ máy bay: Lịch sử đặt bàn, Thông báo, màn xác nhận đặt bàn (#33) báo lỗi mạng + "Thử lại"; tắt máy bay, "Thử lại" tải được | Đạt cả 3 màn | `d01`–`d09` |
| e. Về chế độ Tối | Đạt, giống bản demo | `e01_home_dark_final.png` |

Dọn dữ liệu: đặt bàn thử #32 và #33 đã huỷ; giỏ thử đã trống; chế độ máy bay tắt (`airplane_mode_on` = 0); cỡ chữ máy 1.0.

## Ghi nhận

- Lần chọn 20:00 đầu tiên không ăn vì bong bóng chat Zalo trên máy che nút; chọn 20:30.
- Màn xác nhận của đặt bàn đã huỷ không còn nút "Xem chi tiết" (đúng thiết kế), Back từ đó về bước 5 của form.
