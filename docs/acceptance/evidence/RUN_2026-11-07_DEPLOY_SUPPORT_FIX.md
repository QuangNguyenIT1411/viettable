# Deploy sửa chat hỗ trợ sau test tay — 07/11/2026 (~02:15)

Chủ dự án (07/11): "ok hãy làm như bạn nói" sau báo cáo còn 2 điểm cần sửa máy chủ + deploy và 1 sửa web chưa deploy.

## Thay đổi

- `ab62047` (Customers): tin tự động chỉ gửi khi bắt đầu lượt chờ mới **và** 30 phút gần nhất không có tin nhân viên hay
  tin tự động (`SupportRules.ShouldAutoReply`). Lý do: trên Samsung khách nhắn lại ngay sau khi thu ngân trả lời vẫn nhận
  "ngoài giờ" lần nữa. Trạng thái giữ "Đang xử lý" khi khách trả lời giữa cuộc (như trước).
- `7b43abf` (web thu ngân, commit từ trước, nay mới deploy): thẻ cuộc chat khi rê chuột/focus dùng nền nhạt, chữ đọc được.
- Tên nhân viên trong chat: **không đổi code**. Máy chủ lấy đúng `Employee.Name` (`IdentityController` → `StaffLoginResponse.Name`
  → phiên `EmployeeName`); tài khoản thử `cashier_lan` có họ tên chính là "cashier_lan". Muốn hiện tên thật: sửa họ tên
  nhân viên trong Admin → Nhân viên (cần quyền Admin; chưa làm).

## Test trước deploy

- CloudHost 459/459 (thêm `Automatic_reply_only_when_nobody_answered_for_30_minutes`,
  `Customer_reply_right_after_staff_gets_no_second_automatic_message`; sửa kỳ vọng test mở lại cuộc: 1 tin tự động).
- Contract 186 đạt, 1 bỏ qua (như cũ). 4 web build đạt.
- Image Production `viettable-prod:prepared` `28b4d3be2db5` (07/11 02:09), test web dùng chung 48/48 + bundle 3/3.
- Không migration.

## Deploy

- Thư mục `scripts\prepare-staff-preview.ps1`, link `viet-table/viettable`, xoá `.env.local`, deploy 1 lần.
- **`dpl_AuWeJyy8i9Yvhz4AUf9ZSNHELXAq`** (`https://viettable-8tccpdvdw-viet-table.vercel.app`), READY, production,
  alias `https://viettable-beta.vercel.app`. Code `ab62047`.
- Gọi 1 lần: `/healthz` 200 3,1 s, `/readyz` 200 1,0 s, `/` 200, `/app/cashier/` 200, `/app/chef/` 200, `/app/admin/` 200,
  `/api/mobile/v1/branches` 200, `/api/mobile/v1/support/chat/topics` 401 (ẩn danh, đúng).
- Trang thu ngân trên Production tải `index-B7ghg6tO.css`, có `.support-item:hover,.support-item:focus-visible{background:var(--color-surface-2)…}`.
- Bản quay lui: `dpl_CWXyn4NE1u9hA47pGwn9zEvz6ibK` (`https://viettable-4h088vte6-viet-table.vercel.app`).

## Chưa thử trên máy

- Luật 30 phút với khách thật (Samsung đã đăng xuất Test A); có unit test.
- Tin tự động trong giờ, nút "Cần hỗ trợ?" ở hoá đơn / chờ thanh toán / đặt cọc VietQR, bàn phím màn đánh giá
  (cần quyền Admin hoặc tài khoản có hoá đơn / bàn đang ăn).

App không đổi: APK trên Samsung vẫn `73B9FA568ACF422E`, `GUI_BAN` không cần cập nhật APK.
