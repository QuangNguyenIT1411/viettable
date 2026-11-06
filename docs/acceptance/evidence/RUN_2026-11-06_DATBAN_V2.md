# 06/11/2026 — Đặt bàn đợt 2 (7 chức năng) + gợi ý món: kế hoạch deploy và test tay

Trạng thái: **code xong, CHƯA chạy SQL, CHƯA deploy, CHƯA cài APK, CHƯA push.** Nhánh `qa/vt-ho-01`.

## 1. Commit (mỗi chức năng 1 commit)

| # | Commit | Chức năng |
|---|---|---|
| 1 | `9c38ccf` | Nhắc lịch kèm nút "Sẽ đến" / "Huỷ lịch"; câu trả lời lưu DB (`ArrivalConfirmation`), thu ngân thấy nhãn ngay |
| 2 | `5c24179` | Chỉ đường: admin nhập địa chỉ + toạ độ ("Vị trí chi nhánh"); app nút "Chỉ đường" (bản đồ / Google Maps), ẩn khi chưa có toạ độ |
| 3 | `60274d2` | Hết bàn: server gợi ý giờ gần nhất cùng ngày + chi nhánh khác còn chỗ; bấm thẻ là điền sẵn |
| 4 | `b7a2b47` | Danh sách chờ: giữ bàn trống cho người đầu tiên vừa chỗ 10 phút, hết hạn chuyển người sau; app "Danh sách chờ của tôi"; thu ngân xem danh sách |
| 5 | `2371cf2` | Ghép bàn: admin chia khu vực + bàn ghép được ("Khu vực & ghép bàn"); chọn ít bàn nhất rồi ít ghế thừa nhất, cùng khu; thu ngân thấy "Bàn 5 + 6" |
| 6 | `0096b4a` | Trợ lý đặt bàn (gõ tiếng Việt có/không dấu, hỏi phần thiếu, kiểm bàn thật, chỉ đặt khi khách bấm đồng ý) |
| 7 | `9d97c08` | Gợi ý món theo số người + ngân sách; admin gắn "Thuộc tính món"; tổng tiền như hoá đơn (phí + VAT, chưa trừ khuyến mãi); đổi món, "Thêm tất cả vào giỏ" |
| — | commit sau | Sửa nhỏ màn Hết bàn (ẩn câu lỗi cũ khi gợi ý đã tải), ảnh, tài liệu, file SQL |

## 2. File SQL — chạy TRƯỚC deploy

`SelfRestaurant-master/docs/BOOKING_V2_MIGRATION_20261106.sql` (5 migration: 2 customers, 3 catalog; chỉ THÊM cột/bảng,
không sửa/xoá dữ liệu cũ; có GRANT cho `viettable_customers`, `viettable_catalog`; phần 4 là câu kiểm tra chỉ đọc).

Chạy thử 06/11 trên PostgreSQL 17 tạm (dựng từ toàn bộ migration trước đó): lần 1 không lỗi, 12 cột kiểm tra đều `t`;
lần 2 (chạy lại) không lỗi. **Chưa chạy trên Supabase.** Code mới từ chối khởi động nếu SQL chưa chạy.

## 3. Kế hoạch deploy (mỗi bước cần chủ dự án duyệt)

1. Chủ dự án chạy file SQL trong Supabase SQL Editor, gửi kết quả phần 4 (phải toàn `true`).
2. Claude: `scripts\prepare-staff-preview.ps1` → build image Production (đã build thử 06/11, `87c69ece896a`) →
   deploy từ thư mục đó lên `viet-table/viettable`, xoá `.env.local`.
3. Gọi 1 lần: `/healthz`, `/readyz`, `/`, `/app/cashier/`, `/app/chef/`, `/app/admin/`, `/branches`,
   `POST /api/mobile/v1/menu/suggestions` (chi nhánh 1, 4 người, 800.000đ).
4. Bản quay lui: `dpl_7Nt2UL9UKZMTDocuHRmKWGKTV3Wt` (code `c3c2ead`). SQL chỉ thêm cột/bảng nên bản cũ vẫn chạy với DB mới.
5. Build + cài APK `cloudProd` lên Samsung (hỏi trước).
6. Tuỳ chọn: biến `Waitlist__OfferMinutes` (mặc định 10) nếu muốn đổi thời gian giữ bàn.

## 4. Test trước deploy (06/11)

| Lệnh | Kết quả |
|---|---|
| CloudHost tests | 403/403 |
| Backend contract tests | 186 đạt, 1 bỏ qua |
| App `:app:testDebugUnitTest` | 293/293 (gồm 45 câu trợ lý + 3, 6 câu trợ lý hội thoại, ghép bàn, chọn người chờ, hết hạn giữ, 2 người cùng nhận, gợi ý giờ, gợi ý món) |
| App build debug / cloudPreview / cloudProd | OK |
| 4 web `npm run build` | OK |
| Test web dùng chung (`Dockerfile.staff-preview`) | 43/43 |
| Image Production `viettable-prod:prepared` | OK, `87c69ece896a` |

## 5. Chỗ Claude tự quyết (chủ dự án xem lại)

1. Nhắc lịch: đặt bàn **đã trả cọc** thì nút "Huỷ lịch" mở app (để thấy luật hoàn cọc) thay vì huỷ ngay trên thông báo.
2. Trợ lý: giờ 1–9 không ghi "sáng" hiểu là chiều/tối (nhà hàng mở từ 10:00), ví dụ "7 giờ" = 19:00.
3. App **không xin quyền vị trí**: gợi ý chi nhánh khác sắp theo khoảng cách chỉ khi có toạ độ khách; không có thì theo tên.
4. Danh sách chờ dùng **WorkManager 15 phút/lần** thay FCM (chưa có Firebase). Hướng dẫn bật FCM: `docs/FCM_SETUP.md`.
5. Bàn giữ cho người chờ là một đặt bàn trạng thái `WaitlistHold` có hạn (`HoldExpiresAtUtc`); khách nhận thì thành
   đặt bàn thật (áp luật đặt cọc như thường), hết hạn thì nhả bàn. Hết hạn được xử lý khi có lượt đọc (Vercel không chạy nền).
6. Ghép bàn tối đa **4 bàn**, chỉ bàn cùng khu vực và admin đã đánh dấu "ghép được với nhau".
7. Gợi ý món: món **chưa gắn loại** không bao giờ được gợi ý; "cay" chưa gắn coi như không biết (bật "Không ăn cay" thì bỏ);
   tổng tiền là **trước khuyến mãi/voucher/điểm** (giống dòng tạm tính + phí + VAT trên hoá đơn).
8. Gợi ý món dành ~60% ngân sách cho món chính, phần còn lại thêm khai vị / canh / đồ uống / tráng miệng nếu còn tiền.

## 6. Việc chủ dự án cần làm

1. **FCM** (nếu muốn báo tức thì): làm theo `docs/FCM_SETUP.md`, gửi Claude khi xong bước 1–3.
2. **Toạ độ chi nhánh**: Admin → "Vị trí chi nhánh": nhập địa chỉ, vĩ độ, kinh độ (lấy trên Google Maps: nhấn giữ vị trí
   → số đầu là vĩ độ ~10.7, số sau là kinh độ ~106.7). Chưa nhập thì app ẩn "Chỉ đường".
3. **Thuộc tính món**: Admin → "Thuộc tính món": gắn loại (món chính, khai vị…), chay, cay, khẩu phần (lẩu 4 người ghi 4).
   Món chưa gắn loại không được gợi ý.
4. **Khu vực / ghép bàn**: Admin → "Khu vực & ghép bàn": đặt tên khu cho từng bàn, chọn các bàn kê sát được. Chưa cấu hình
   thì không ghép, bàn lớn vẫn đặt như cũ.

## 7. Danh sách test tay (sau deploy, trên Samsung + web)

1. Đặt bàn giờ gần (nhắc lịch theo `REMINDER_MINUTES`): thông báo có 2 nút; bấm "Sẽ đến" → thu ngân thấy nhãn "Khách xác nhận sẽ đến";
   đặt bàn khác bấm "Huỷ lịch" → đặt bàn huỷ, thu ngân thấy "Khách huỷ từ lời nhắc". Đặt bàn có cọc: "Huỷ lịch" mở app.
2. Nhập toạ độ Quận 1 → app: Chi nhánh và Chi tiết đặt bàn có "Chỉ đường", mở đúng bản đồ. Chi nhánh chưa có toạ độ: không có nút.
3. Đặt kín 1 khung giờ (hoặc chọn số khách lớn) → màn "Hết bàn" hiện giờ khác + chi nhánh khác; bấm thẻ → sơ đồ bàn đúng giờ/chi nhánh mới.
4. Vào danh sách chờ (tài khoản A); tài khoản B huỷ đặt bàn giờ đó → A thấy "Có bàn cho bạn" (mở app hoặc chờ ≤15 phút), đếm ngược 10 phút;
   nhận → thành đặt bàn thật. Lần khác để hết 10 phút → lời mời chuyển sang người kế (thu ngân thấy trong bảng Danh sách chờ).
5. Cấu hình 2 bàn 4 chỗ cùng khu, ghép được → đặt 7 khách → app gợi ý "Bàn 5 + 6"; thu ngân thấy "Bàn 5 + 6"; check-in và thanh toán 1 lần.
6. Trợ lý: gõ "toi mai 7h 4 nguoi quan 1" → hỏi xác nhận → "Đặt bàn" → có đặt bàn; gõ thiếu thông tin → trợ lý hỏi lại.
7. Gắn thuộc tính ≥5 món → app Thực đơn → "Gợi ý món": 4 người, 1 ăn chay, 800k → có món chay, tổng ≤ 800k; bật phí/VAT
   ở chi nhánh thử thì tổng đổi theo; "Đổi" món → tổng tính lại; "Thêm tất cả vào giỏ" → giỏ đúng số lượng.

## 8. Ảnh máy ảo

`screenshots\datban_v2\` — máy ảo `bs_check` (Android 37.1, 1080×2400, tạo lại 06/11 tại `D:\Quang\AndroidAvd`
vì máy ảo cũ không còn), dữ liệu MẪU qua `BookingV2DesignPreviewTest` (chỉ chạy tay, không gọi server):
`suggest`, `suggest_result`, `assistant`, `notable`, `waitlist` × `dark`/`light` × chữ `10` (100%) / `13` (130%).
Ảnh tổng: `SO_SANH_datban_v2.png` (hàng: Tối 100%, Tối 130%, Sáng 100%, Sáng 130%).
Câu "Không thể kết nối đến VietTable" trên ảnh `notable`/`waitlist` là do máy ảo không có server (lỗi tải tới sau khi bài
test đã điền dữ liệu mẫu), không phải giao diện thật khi có mạng.
Sửa nhờ xem ảnh: tin nhắn của khách trong Trợ lý mất chữ ở giao diện Sáng (chữ `pd_on_color` trên nền đảo) → dùng
`pd_on_inverse`; màn Hết bàn ẩn câu lỗi cũ khi gợi ý đã tải. Sau 2 sửa: app 293/293, build debug OK.
Màn quản trị web chưa chụp (cần đăng nhập backend có dữ liệu): xem sau deploy.
