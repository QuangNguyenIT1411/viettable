# Deploy + test Samsung: phí/VAT, hoá đơn, đánh giá, 05/11/2026

Chủ dự án chạy `BILL_REVIEW_MIGRATION_20261105.sql` trên Supabase rồi duyệt cả chuỗi: deploy, cài APK, test ở 0%, push.
Kế hoạch và test trước đó: `RUN_2026-11-05_HOADON_DANHGIA.md`.

## Deploy

| Mục | Kết quả |
|---|---|
| Deploy đang chạy | `dpl_7Nt2UL9UKZMTDocuHRmKWGKTV3Wt` (`https://viettable-a2zuah5eo-viet-table.vercel.app`), 05/11 23:12, Production, Ready, `[syd1]`, alias `viettable-beta.vercel.app` (`vercel inspect`), code `c3c2ead` |
| Lệnh deploy | CLI báo `fetch failed` lúc theo dõi; Vercel vẫn build xong, xác nhận bằng `inspect` |
| Gọi 1 lần | `/healthz` 200 8,1 s, `/readyz` 200 1,1 s, `/` 200, `/app/cashier/` 200, `/app/chef/` 200, `/app/admin/` 200, `/branches` 200, `/dishes/29/reviews/summary` 200, `/invoices` 401 khi ẩn danh (đúng) |
| Bản trung gian | `dpl_A5uRNd7T…` (lỗi tóm tắt đánh giá món 401), `dpl_CgUZYKc69ij3KwbT3sSgFLiTHGKM` (code `e86428c`, lỗi tìm kiếm) |
| Quay lui | Trước đợt này: `dpl_BdmHSffuXEKQLvpKCCA4G9bMDHNA`. Migration chỉ thêm cột/bảng có mặc định |
| `.env.local` | CLI tự tạo khi link, đã xoá trước deploy |

## Test trước deploy cuối

| Lệnh | Kết quả |
|---|---|
| CloudHost tests | 336/336 |
| Contract tests | 186 đạt, 1 bỏ qua |
| `gradlew :app:assembleDebug :app:assembleCloudPreview :app:assembleCloudProd :app:testDebugUnitTest` | 3 bản build OK, 210/210 |
| Image Production | `viettable-prod:prepared` `12a051d83e56`, shared 34/34, bundle 3/3 |

## APK

`cloudProd`, code `c71e71c`, SHA-256 bắt đầu `0D7BE9ADD181579A`, `adb install -r` lên Samsung SM-A528B, lastUpdateTime 23:21:29 05/11/2026.

## Test tay trên Samsung (mọi chi nhánh 0%, tài khoản đang đăng nhập, chế độ Sáng)

Ảnh: `screenshots\samsung_hoadon\`.

| Việc | Kết quả | Ảnh |
|---|---|---|
| Hoá đơn của tôi: danh sách thật, nhóm theo tháng | ĐẠT | `h01`–`h08`, `s05_list` |
| Lọc Chuyển khoản: #40, #39; lọc tháng không có hoá đơn: "Không có hoá đơn khớp bộ lọc." | ĐẠT | `h01`–`h08` |
| Tìm "44" | Lần đầu SAI (ra cả #35, do mã có GUID chứa 44). Sửa `2072938`, test lại ĐẠT: chỉ #44 | `s06_search44` |
| Hoá đơn cũ hiện bàn | Lần đầu thiếu. Sửa `c3c2ead`, test lại ĐẠT: #44 "Bàn 2", khớp `RUN_2026-10-30_SAMSUNG.md` | `s05_list`, `s07_detail` |
| Chi tiết #44: dòng 0đ bị ẩn, không có phí/VAT | ĐẠT | `s07_detail` |
| Xuất hoá đơn: file trong Download/VietTable, mở menu chia sẻ | ĐẠT | `s08_share` |
| Nội dung PDF: 1 trang rộng 227 pt, tiếng Việt đủ dấu, "PHIẾU THANH TOÁN", "Không phải hoá đơn GTGT" | ĐẠT | đọc file đã kéo về |
| Nhãn giờ trên PDF | Lần đầu ghi "Giờ vào – thanh toán" dù chỉ có giờ thanh toán. Sửa `c3c2ead`, test lại ĐẠT: "Thanh toán lúc" | |
| Gửi đánh giá #44: 4 sao tổng, 3 tiêu chí, thẻ, chấm món, ẩn tên | ĐẠT, ghi được trên Supabase (quyền INSERT đúng); nút đổi thành "Xem đánh giá" | `r04`–`r08` |
| Mở lại đánh giá: "Sửa đánh giá", hạn "tới 23:05, 06/11/2026" | ĐẠT | `r08_view_review` |
| Chi tiết món Bánh mì ốp la: "5,0 (1)", "★ 5,0 / 5 · 1 lượt đánh giá", "Khách ẩn danh" | ĐẠT | `s10_dish_reviews` |
| API công khai `/dishes/29/reviews/summary`: tên "Khách ẩn danh", không lộ tên thật | ĐẠT | |
| Đánh giá của tôi: đánh giá quá 24 giờ | Lần đầu vẫn hiện "Sửa"/"Xoá" dù server từ chối. Sửa `c71e71c`, test lại ĐẠT: #27, #25 chỉ còn "Xem"; #44 còn "Sửa"/"Xóa" | `s12`, `s13_my_reviews_fixed` |
| Sửa hồ sơ: có ô Ngày sinh | Chỉ xem, không lưu | `s11_profile` |

## Chưa test

- Thanh toán thật có phí/VAT (cần quét QR bàn, bật % trong Admin); các bước 1–6 của danh sách test tay trong `RUN_2026-11-05_HOADON_DANHGIA.md`.
- Màn Admin (Phí & VAT, Đánh giá) và thu ngân (In / Xuất) chưa mở trên trình duyệt; chưa in thật; chưa gửi thật qua Zalo.
- Thông báo "Đánh giá bữa ăn"; lưu ngày sinh; máy bay → "Thử lại"; chế độ Tối với dữ liệu thật (chỉ có ảnh dữ liệu mẫu).
- Gửi đánh giá lần 2 cùng hoá đơn: app chuyển sang màn sửa; chặn ở server chỉ có unit test.

## Dữ liệu thử để lại

- Đánh giá hoá đơn #44 trên tài khoản đang đăng nhập, nhận xét "Danh gia thu, se xoa" (`adb` không gõ được dấu), hiện công khai ở món Bánh mì ốp la dưới tên "Khách ẩn danh". Xoá được trong "Đánh giá của tôi" tới 23:05 06/11/2026; sau đó phải xử lý qua Admin → Đánh giá (ẩn).
- 2 file PDF trong Download/VietTable của Samsung.
- Máy đang ở chế độ Sáng (đã thấy `light` khi bắt đầu test, không đổi).
