# Chat với nhân viên hỗ trợ — 06/11/2026

Chủ dự án giao 06/11 tối: làm tự động, **dừng trước khi chạy SQL và deploy**. Mọi dữ liệu lưu Supabase, không viết cứng, không dữ liệu giả.

Trạng thái: **code xong, test đạt. CHƯA chạy SQL, CHƯA deploy, CHƯA cài APK, CHƯA push.**

## Commit

| Commit | Nội dung |
|---|---|
| `7291ee5` | Backend: bảng hội thoại/tin nhắn/câu trả lời mẫu/giờ hỗ trợ (Customers), kiểm tra cọc/chuyển khoản, tin tự động trong/ngoài giờ, chống spam, phân quyền, realtime `supportMessage` |
| `c3f5cde` | File SQL `SelfRestaurant-master/docs/SUPPORT_CHAT_MIGRATION_20261106.sql` |
| `c1a4c8a` | Web thu ngân tab "Hỗ trợ", Admin "Hỗ trợ khách" |
| (commit app, sau file này) | App: màn bắt đầu chat, khung chat, "Hỗ trợ của tôi", 5 nút "Cần hỗ trợ?", thông báo mở "Hỗ trợ của tôi" |

## Kết quả test (06/11, trên máy)

| Hạng mục | Kết quả |
|---|---|
| CloudHost tests | 457/457 đạt (gồm `SupportChatTests`) |
| Backend contract tests | 186 đạt, 1 bỏ qua (như cũ) |
| App unit test | 323/323 đạt (gồm `SupportTextTest`) |
| App build debug / cloudPreview / cloudProd | đạt |
| 4 web build | đạt |
| Image Production | `viettable-prod:prepared` `4b7a4a3e5d65` (06/11 23:48), test web dùng chung 48/48 (gồm `supportChat.test.mjs`, `cashierTabs.test.mjs` 6 tab) + bundle 3/3 |
| SQL chạy thử | 2 lần trên PostgreSQL 17 tạm: lần 2 không lỗi, câu kiểm tra toàn `true` |

Ảnh máy ảo (`bs_check`, dữ liệu mẫu, không có server): `screenshots\support_chat\` — 4 màn (`start`, `confirmed`, `chat`, `list`) × Sáng/Tối × chữ 100%/130% = 16 ảnh. Test chụp: `SupportChatDesignPreviewTest` (chỉ chạy tay).

## Chỗ tự quyết (chờ chủ dự án duyệt)

1. **Không làm đính kèm ảnh.** Lý do: app chưa thật sự tải ảnh ở phần đánh giá (gửi `imageUrls` rỗng), và bucket Supabase duy nhất đang dùng là bucket **công khai** cho ảnh món. Ảnh khách gửi (thường là ảnh chụp chuyển khoản, có số tài khoản/tên ngân hàng) mà để công khai là không an toàn. Muốn làm cần bucket riêng tư + link ký có hạn: việc riêng.
2. Giờ hỗ trợ: lấy bảng "Giờ hỗ trợ" Admin nhập; trống thì đọc giờ mở cửa của chi nhánh (`Branches.OpeningHours`, dạng chữ như "10:00 - 22:00"); không đọc được thì tin tự động **không hứa giờ** (chỉ báo nhân viên sẽ trả lời).
3. Tin tự động chỉ gửi khi bắt đầu một lượt chờ mới (tin đầu tiên, hoặc khách nhắn lại sau khi nhân viên đã trả lời/đã giải quyết), không gửi sau mỗi tin.
4. Chống spam: tối đa 5 tin / 30 giây mỗi cuộc, 3 cuộc mới / giờ mỗi khách; tin tối đa 1.000 ký tự; bỏ thẻ HTML, từ chối `<script`, `javascript:`, `on…=`.
5. Cuộc đã giải quyết: khách nhắn tiếp thì mở lại (về "Đang chờ").
6. Thông báo khi nhân viên trả lời: dùng hộp thông báo sẵn có (`support.reply.v1`, tiêu đề "Nhân viên hỗ trợ đã trả lời"), xếp vào nhóm "Đơn hàng" của app (app chưa có nhóm "Hỗ trợ"); bấm vào mở "Hỗ trợ của tôi". App đóng hẳn thì chưa có thông báo đẩy (chưa có FCM, như danh sách chờ).
7. Khi đang mở chat: nhận realtime `supportMessage`; nếu realtime mất kết nối thì tự tải lại mỗi 20 giây.
8. Thu ngân: danh sách sắp theo chờ lâu nhất; ô "Hiện cả đã giải quyết" xem cuộc đã giải quyết trong 7 ngày.
9. Câu trả lời mẫu: theo chi nhánh của admin (admin Quận 1 chỉ sửa của Quận 1).
10. Nút "Cần hỗ trợ?" ở màn hoá đơn trước khi trả (chưa có mã hoá đơn): mở chủ đề "Hoá đơn sai món / sai tiền" không kèm mục; ở chi tiết hoá đơn: kèm đúng hoá đơn.
11. Chủ đề có dữ liệu mà khách không có mục nào gần đây: vẫn cho nhắn, không kèm mục. Giao dịch chuyển khoản lấy 14 ngày gần nhất.
12. Tin nhắn đầu được điền sẵn từ dữ liệu thật (ví dụ "Tôi đã chuyển 300.000đ với nội dung "VT DC37" nhưng chưa được xác nhận."), khách sửa được.

## Kế hoạch deploy (chờ duyệt từng bước)

1. Chủ dự án chạy `SelfRestaurant-master/docs/SUPPORT_CHAT_MIGRATION_20261106.sql` trong Supabase SQL Editor (chạy cả file; chạy lại không lỗi). Phần cuối là câu kiểm tra: mọi cột phải `true`.
2. Build image Production từ thư mục `scripts\prepare-staff-preview.ps1` → xoá `.env.local` → `npx vercel@62.4.0 deploy --prod` (team `viet-table`, project `viettable`) → gọi 1 lần `/healthz`, `/readyz`, `/`, 3 web nhân viên, `/branches`, `/api/mobile/v1/support/chat/topics` (401 khi ẩn danh là đúng).
   - **Phải chạy SQL trước**: code mới thấy migration `20261106161251_SupportChat` chưa chạy thì **từ chối khởi động** (`PostgresBootstrap`), cả API sẽ lỗi.
   - Bản quay lui: `dpl_8rkgZQBvzq275Hn12M2C8H4NqxtN` (`https://viettable-be8r9hyr4-viet-table.vercel.app`). SQL chỉ thêm bảng nên bản cũ vẫn chạy với DB mới.
3. Cài APK `cloudProd` mới lên Samsung (1 lần, gồm luôn các sửa của bản `8C959DFA…`).
4. Push (không force), cập nhật CLAUDE.md.

## Danh sách test tay sau deploy

Khách = Test A hoặc Test B trên Samsung/máy ảo; thu ngân = tài khoản Quận 1 trên trình duyệt.

1. Tôi → "Hỗ trợ của tôi": trống, có nút "Cuộc trò chuyện mới". Hỗ trợ → thẻ "Chat CSKH" mở màn "Chat với nhân viên".
2. Chi nhánh mặc định là chi nhánh đang chọn; 7 thẻ chủ đề đúng chữ.
3. Chủ đề 1 với cọc **đã** nhận: hiện "Khoản này đã được xác nhận lúc …" + "Bạn còn cần hỗ trợ không?". Với cọc **chưa** nhận: nhắc "VT DC<mã>" + số tiền, tin đầu điền sẵn.
4. Chủ đề 2 (chuyển khoản thanh toán): tương tự với "VT B…".
5. Gửi trong giờ: tin "Tin nhắn tự động: Nhân viên sẽ trả lời bạn sau ít phút." (bong bóng vàng ở giữa). Đặt giờ hỗ trợ trong Admin sang khung đã qua rồi gửi: tin báo ngoài giờ + giờ mở cửa lại. Xong đổi giờ về như cũ.
6. Thu ngân Quận 1 → tab "Hỗ trợ": cuộc mới hiện ngay (realtime), có huy hiệu số chưa đọc, ngữ cảnh có trạng thái thật + link sang tab đúng. Chi nhánh khác không thấy.
7. Thu ngân trả lời (gõ hoặc chọn câu mẫu): app đang mở chat nhận ngay; tin của khách chuyển "Đã xem". App ở màn khác: chuông có thông báo "Nhân viên hỗ trợ đã trả lời", bấm mở "Hỗ trợ của tôi".
8. Thu ngân "Đã giải quyết": app hiện "Đã giải quyết"; khách nhắn tiếp → mở lại "Đang chờ".
9. Gửi 6 tin liên tục trong 30 giây: tin thứ 6 bị từ chối với câu giải thích. Gõ `<script>`: bị từ chối.
10. Cuộn lên đầu cuộc có hơn 40 tin: tải tin cũ, không nhảy vị trí.
11. Bàn phím không che ô nhập (màn chat và màn bắt đầu); chữ 130% và giao diện Sáng đọc rõ.
12. 5 nút "Cần hỗ trợ?": đặt cọc VietQR, chi tiết đặt bàn, hoá đơn, chi tiết hoá đơn, chờ thanh toán: mở đúng chủ đề và chọn sẵn đúng mục.
13. Dọn dữ liệu test sau khi xong (chủ dự án duyệt cách dọn).
