# Phí phục vụ + VAT, snapshot hoá đơn, lịch sử/chi tiết hoá đơn, PDF, nâng cấp đánh giá, 05/11/2026

Trạng thái: **code và test xong trên máy. CHƯA chạy SQL trên Supabase, CHƯA deploy, CHƯA cài APK, CHƯA push.**
Chưa có màn nào chạy với dữ liệu thật: API mới chưa có trên Production nên ảnh dùng dữ liệu mẫu.

## Commit

`33975de` backend · `f71aa8b` app · `a66a765` web · `51d6075` bước 0 + test xem trước · `b2cdce7` sửa thanh nút + ảnh.
Tài liệu: `SelfRestaurant-master/docs/CHECKOUT_FORMULA.md`, `docs/DATA_AUDIT_20261105.md`.

## Test đã chạy

| Lệnh / bước | Kết quả |
|---|---|
| CloudHost tests | 336/336 đạt (gồm `BillChargesSnapshotTests`, `ReviewUpgradeTests`) |
| Contract tests | 185 đạt, 1 bỏ qua |
| `gradlew :app:assembleDebug :app:assembleCloudPreview :app:assembleCloudProd :app:testDebugUnitTest` | 3 bản build OK, 210/210 |
| androidTest `ReceiptPdfTest` trên máy ảo `bs_check` | OK (1 test): PDF 1 trang, rộng 80 mm |
| Web admin, thu ngân `npm run build` | OK |
| Image Production (`prepare-staff-preview.ps1` + `docker build`) | `viettable-prod:prepared` `b6c2520e506b`, shared 34/34 (gồm `receipt.test.mjs`), bundle 3/3; build cả 3 web nhân viên |
| Chạy thử `BILL_REVIEW_MIGRATION_20261105.sql` trên PostgreSQL 17 tạm trong Docker (dựng từ toàn bộ migration trước đó, DB rỗng, xoá sau khi xong; không đụng Supabase) | Chạy 2 lần không lỗi; 2 migration được ghi nhận; câu kiểm tra quyền toàn `t` |

Chưa kiểm: SQL trên dữ liệu thật của Supabase; toàn bộ API mới gọi qua app/web thật; in phiếu từ trình duyệt; lưu vào Tải xuống và menu chia sẻ trên máy thật; `SelfRestaurant.Postgres.Tests` và `SqlServer.CompatibilityTests` (cần DB, không chạy).

## Ảnh

`screenshots\hoadon_danhgia\` (Sáng/Tối × chữ 100%/130%, dữ liệu mẫu, `InvoiceReviewDesignPreviewTest`), ảnh ghép `SO_SANH_hoadon_danhgia.png`.
Chưa chụp: màn thật của admin và thu ngân.

## Kế hoạch deploy (mỗi bước cần chủ dự án duyệt)

1. Chạy `SelfRestaurant-master/docs/BILL_REVIEW_MIGRATION_20261105.sql` trong Supabase SQL Editor. **Phải trước deploy**: code mới từ chối khởi động khi còn migration chưa chạy. Bản đang chạy vẫn hoạt động với DB mới (chỉ thêm cột/bảng có mặc định).
2. Xem phần 4 của SQL: 2 dòng migration, toàn `true`, `branches_with_fee_or_vat = 0`, `with_charges = 0`.
3. Deploy Production theo quy trình chuẩn (build image, xoá `.env.local`, kiểm `[syd1]` Ready, gọi `/healthz`, `/readyz`, `/`, `/app/cashier/`, `/app/admin/`, `/branches`). Quay lui: `dpl_BdmHSffuXEKQLvpKCCA4G9bMDHNA`.
4. Cài APK `cloudProd` mới lên Samsung. Cài sau deploy: app mới gọi `/dishes/{id}/reviews/summary` và đọc các trường snapshot.
5. Test tay (dưới). Chỉ bật phí/VAT trong Admin sau khi test ở 0% đạt.

## Test tay sau deploy

1. Mọi chi nhánh 0%/0%: thanh toán 1 bữa ở app, tổng không đổi, không có dòng phí/VAT.
2. Admin → Phí & VAT: đặt 1 chi nhánh 5% / 8%, thử nhập 35% phải bị từ chối.
3. App: gọi món, xem báo giá có "Phí phục vụ (5%)", "VAT (8%)"; dùng điểm + voucher; số khớp `CHECKOUT_FORMULA.md`.
4. Thu ngân: cùng phiên, `checkout-preview` cùng số; thu tiền tại quầy một đơn không qua app.
5. Đơn có đặt cọc: cọc trừ sau VAT.
6. Sau thanh toán: đổi % về 0 và sửa giá món → hoá đơn cũ không đổi.
7. Hoá đơn của tôi: nhóm tháng, lọc tháng/chi nhánh/phương thức, tìm "44", cuộn tải thêm, máy bay → "Thử lại"; hoá đơn cũ không có dòng phí/VAT.
8. Chi tiết hoá đơn: giờ vào–thanh toán, thu ngân, mã giao dịch khi chuyển khoản, điểm được cộng khớp thông báo điểm.
9. Xuất hoá đơn: file trong Tải xuống/VietTable, chia sẻ qua Zalo, tiếng Việt đủ dấu, có "Không phải hoá đơn GTGT".
10. Thu ngân → Lịch sử → In / Xuất: cả đơn quầy và hoá đơn app; đơn quầy cũ ghi "không lưu danh sách món".
11. Đánh giá: nhận thông báo "Đánh giá bữa ăn"; gửi đủ tiêu chí, chấm món, thẻ, ẩn tên; gửi lần 2 cùng hoá đơn bị chặn; sửa được, quá 24 giờ thì khoá.
12. Chi tiết món: "Chưa có đánh giá" → sau khi đánh giá hiện trung bình + nhận xét; ẩn tên hiện "Khách ẩn danh".
13. Admin → Đánh giá: lọc, trả lời (khách thấy trong app), ẩn phải có lý do (biến khỏi điểm trung bình, ghi người ẩn), thống kê.
14. Sửa hồ sơ: chọn ngày sinh, Lưu, mở lại vẫn còn; ngày sinh/giới tính/địa chỉ không bị xoá khi chỉ đổi tên.
15. Lặp lại màn hoá đơn và đánh giá ở chế độ Sáng và Tối.
