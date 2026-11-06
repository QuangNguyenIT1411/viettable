# Test tay chat hỗ trợ, thông báo, bàn phím, G15 — 07/11/2026 (01:30–02:05)

Chủ dự án (07/11): Samsung đăng nhập Test A (Quang1), trình duyệt trong app đăng nhập thu ngân Quận 1 (`cashier_lan`).
Duyệt: không gửi ảnh, nhận các chỗ tự quyết còn lại của chat hỗ trợ, push (không force). G15 chỉ sửa phía app.

## G15 (`d8f9e60`, chỉ app)

- Nguyên nhân phía app: danh sách chi nhánh được ghi vào bộ nhớ đệm trên máy nhưng không bao giờ đọc lại.
- Sửa: có danh sách đã lưu thì hiện ngay, đồng thời tải lại ngầm. Không đổi máy chủ.
- Samsung, mở lại app: thực đơn + chi nhánh có ngay từ bộ nhớ (`CatalogPerf restore branch=1 … mapMs=26`), sau đó
  `/branches` 637 ms, `/menu` 742 ms, `/notifications` 269 ms cập nhật ngầm.

## Kết quả (Samsung SM-A528B, Production `dpl_CWXyn4NE…`)

| # | Mục | Kết quả |
|---|---|---|
| 1 | Bấm chuông → danh sách thông báo | Đạt: 0,3 s sau khi bấm đã có danh sách (`screenshots\samsung_20261107\notif_03s.png`) |
| 2a | Bắt đầu chat: chi nhánh mặc định | Đạt: Quận 1 được chọn sẵn |
| 2b | Tin tự động ngoài giờ | Đạt: "Hiện ngoài giờ làm việc (07:00–22:00). … mở cửa lúc 07:00 hôm nay." (lúc 01:47) |
| 2c | Thu ngân thấy cuộc mới, huy hiệu chưa đọc | Đạt: tab Hỗ trợ có "1", cuộc "Đang chờ" |
| 2d | Thu ngân trả lời → app | Đạt: tin đến app ~3 s, trạng thái "Đang xử lý", tin khách "Đã xem"; phía thu ngân "Khách đã xem" |
| 2e | Đã giải quyết | Đạt: máy chủ ghi "Giải quyết bởi cashier_lan"; app cập nhật sau ~24 s (tin trả lời thì 3 s) |
| 2f | Khách nhắn lại sau khi giải quyết | Đạt: cuộc mở lại "Đang chờ" (cả app và thu ngân) |
| 2g | Tin tự động trong giờ | **Chưa thử**: trình duyệt không có quyền Admin để đổi tạm giờ hỗ trợ, và lúc thử là 01:47 (ngoài giờ). Chỉ có unit test |
| 3a | "Cần hỗ trợ?" ở chi tiết đặt bàn | Đạt: chủ đề "Đổi / huỷ đặt bàn", chọn sẵn #40, tin điền sẵn |
| 3b | Chủ đề "Đã chuyển khoản đặt cọc" tự kiểm tra | Đạt: chọn cọc #37 → máy chủ trả "chưa nhận (Đã huỷ)", nhắc "VT DC37" + 500.000đ, tin điền sẵn đúng |
| 3c | "Cần hỗ trợ?" ở chi tiết hoá đơn | **Chưa thử**: Quang1 chưa có hoá đơn |
| 3d | Ở đặt cọc VietQR, hoá đơn tại bàn, chờ thanh toán | **Chưa thử**: cần đặt bàn chờ cọc / bàn đang ăn; không tự tạo dữ liệu |
| 4a | Bàn phím: bắt đầu chat, khung chat | Đạt |
| 4b | Bàn phím: Sửa hồ sơ | Lỗi lúc đầu (ô Email bị thanh "Lưu thay đổi" che nửa) → sửa `7b43abf` → Đạt (`kb_edit_profile_after.png`) |
| 4c | Bàn phím: ghi chú đặt bàn (bước 5) | Đạt (`kb_res_note.png`); thoát ra, không xác nhận đặt bàn |
| 4d | Bàn phím: đánh giá | **Chưa thử**: không có hoá đơn để đánh giá |

## Lỗi tìm thấy khi test, đã sửa (`7b43abf`)

1. Thanh nút dưới cùng nằm đè vùng cuộn (Sửa hồ sơ) che ô đang nhập → cuộn thêm đúng phần bị che (sửa chung `BaseFragment`).
2. Đổi chủ đề ở màn bắt đầu chat vẫn giữ câu điền sẵn của chủ đề cũ → câu điền sẵn đi theo chủ đề tới khi khách tự gõ. Đã thử lại trên Samsung: đạt (`prefill_switch_after.png`).
3. Trình đọc màn hình đọc "Tin nhắn tự động" 2 lần → đọc 1 lần.
4. Web thu ngân: thẻ cuộc khi rê chuột dùng màu nền xanh đậm của nút chung, chữ khó đọc → nền nhạt. **Chưa deploy** (web), có hiệu lực ở lần deploy sau.

Unit test 329/329, 3 bản build, web thu ngân build đạt.

## Ghi lại để chủ dự án quyết

- Khách nhắn lại ngay sau khi thu ngân trả lời (ngoài giờ) vẫn nhận thêm tin tự động "ngoài giờ" — đúng luật "lượt chờ mới
  thì gửi", nhưng giữa cuộc trò chuyện thì thừa. Sửa cần đổi máy chủ + deploy.
- Tên nhân viên trong chat là tên đăng nhập (`cashier_lan`).

## Dọn dữ liệu, thiết bị

- Cuộc chat thử (Quang1, "Khác") đã đánh dấu "Đã giải quyết" bởi `cashier_lan`; danh sách thu ngân trống.
- Không tạo đặt bàn, không sửa hồ sơ (thoát không lưu).
- APK `cloudProd` cuối cùng trên Samsung: SHA-256 bắt đầu `73B9FA568ACF422E` (lần cài trong phiên: `F0751550…`, `73B9FA56…`).
- Samsung đã đăng xuất Test A (đăng nhập vân tay được giữ, đúng thiết kế), giao diện Tối, đang ở màn Chào mừng.
- `GUI_BAN`: APK `73B9FA56…`, mã nguồn từ `7b43abf`, hướng dẫn mục 9 sửa dòng mở app chậm; `GUI_BAN.zip` 46,3 MB.
