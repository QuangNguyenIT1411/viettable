# Ảnh món, thông báo, cài Samsung, gói GUI_BAN — 07/11/2026

Chủ dự án (07/11): cài Samsung; kiểm tra ảnh món ở "Xem tất cả" và thông báo tải lâu ("bấm vào nó mới gọi request"); muốn
bấm vào là hiện ảnh liền; xong thì cập nhật `GUI_BAN`.

## Đo trước khi sửa (Production, máy tính, `curl`)

- Thực đơn Quận 1: 51 ảnh món, tổng 8,5 MB, trung bình 167 KB/ảnh, lớn nhất 580 KB (`bo-ne.png`).
- Mỗi ảnh trung bình 0,81 s, trong đó 0,35 s là bước chuyển hướng 302 từ Vercel sang Supabase Storage.
- App: bộ nhớ đệm trên máy chỉ 64 ảnh (thực đơn 54 món + ảnh chi nhánh nên ảnh cũ liên tục bị xoá), giải mã nguyên kích
  thước, chỉ tải khi ô ảnh hiện ra. Đúng như chủ dự án nghĩ: bấm vào mới tải.
- Thông báo: app có giữ danh sách trong bộ nhớ khi Trang chủ đã tải, nhưng mở app lại thì trống tới khi máy chủ trả về.
  Máy chủ làm thêm bước bù "Ưu đãi mới" (`PromotionAnnouncer.EnsureForCustomerAsync`) trước mỗi lần đọc trang đầu.

## Sửa (`72a4b69`, chỉ app)

- Ảnh: thực đơn vừa tải xong là tải trước toàn bộ ảnh món ở nền (3 luồng riêng, không chen ảnh đang hiện), thu nhỏ còn
  tối đa 720 px, lưu trên máy 7 ngày, tối đa 400 ảnh.
- Thông báo: lưu danh sách lần trước trên máy theo đúng tài khoản (đăng xuất thì xoá), mở app là hiện ngay; danh sách mới
  tải xong thì thay. Ghi log thời gian request thông báo (`CatalogPerf`, chỉ đường dẫn và số ms).
- Không đổi backend (bước bù "Ưu đãi mới" ở máy chủ giữ nguyên, có thể làm nhẹ sau nếu đo thấy chậm).
- Unit test 326/326 (thêm `ImageLoaderSizeTest`, `NotificationInboxStoreTest`), 3 bản build đạt.

## Samsung SM-A528B

- `adb -s R5CRC14LFYD install -r` APK `cloudProd` SHA-256 bắt đầu `2430EFC55F7A3924`, lastUpdateTime 2026-11-07 01:06:02.
  Gồm: chat hỗ trợ, sửa bàn phím (`3428208`), ảnh/thông báo (`72a4b69`), các sửa của bản `8C959DFA…`.
- Máy đang ở màn Chào mừng (chưa đăng nhập) nên thử ở chế độ khách:
  - `/branches` 7,4 s (máy chủ khởi động lạnh, G15), thực đơn Quận 1 1,1 s.
  - Khoảng 1 phút sau khi chọn chi nhánh, bấm "Xem hết": 0,7 s sau ảnh 6 món đầu đã có
    (`screenshots\keyboard_fix\samsung_xem_het_07s.png`); cuộn nhanh tới cuối: ảnh các món cuối có sẵn (`samsung_xem_het_cuoi.png`).
  - Ở mép trái ảnh chụp có cửa sổ nổi của một app khác trên máy chủ dự án, Claude không động vào.
- **Chưa thử trên máy:** thông báo (cần đăng nhập, Claude không nhập mật khẩu), chat hỗ trợ với server thật, bàn phím ở
  các màn khác ngoài Đăng ký/Đăng nhập (đã thử trên máy ảo).

## GUI_BAN (thư mục bị git bỏ qua, không commit)

- `VietTable.apk` thay bằng APK `2430EFC55F7A3924`.
- `VietTable_source.zip` tạo lại bằng `git archive` từ HEAD (`app`, `SelfRestaurant-master`, file Gradle gốc như bản cũ),
  1952 mục, không có `b64.txt`, `.env`, `local.properties`.
- `HUONG_DAN_SU_DUNG.md`: thêm mục 3.15 Chat với nhân viên hỗ trợ (3 ảnh mới `40`–`42` từ máy ảo, dữ liệu mẫu), dòng
  "Hỗ trợ của tôi", tab **Hỗ trợ** của thu ngân (6 tab), mục **Hỗ trợ khách** của Admin, ghi chú thông báo mở ngay và ảnh món.
- `GUI_BAN.zip` nén lại (46,3 MB, 42 mục).
