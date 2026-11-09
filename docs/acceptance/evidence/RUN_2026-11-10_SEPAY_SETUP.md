# RUN 2026-11-10: chuyển khoản không tự xác nhận (TT53) và thiết lập SePay

## Sự cố
Chủ dự án quét VietQR của giao dịch `VT B1 TT53` bằng MoMo; app không tự chuyển "Thanh toán thành công".

## Điều tra (chỉ đọc)
- Log Production `dpl_9B2tf7N8…` 01:05–01:09 (10/11): app tạo QR (`GET /payments/53/transfer-qr` 01:05:27) và hỏi trạng thái liên tục; **không có request nào tới `/api/payments/sepay/webhook`**.
- POST không chữ ký tới webhook: 401 → địa chỉ không bị chặn, SePay bật, có secret.
- Tài khoản SePay (công ty "Nguyễn Vinh Quang", tạo 2026-10-25 08:12:58): **0 tài khoản ngân hàng, 0 webhook, 0 giao dịch**.

**Nguyên nhân:** SePay chưa nối ngân hàng và chưa có webhook, nên không ai báo tiền về cho VietTable. Ngoài ra BIDV cá nhân chỉ báo tiền vào VA, trong khi QR trỏ số chính `8830150124`.

## Thiết lập (10/11)
- Chủ dự án nối BIDV cá nhân qua API (tự nhập CCCD/OTP), SePay tạo VA **`962477LXG8`**. Thử 2.000đ từ MoMo vào VA lúc 01:22:09: SePay nhận.
- Webhook "VietTable" (Claude điền mục 1–2 theo yêu cầu chủ dự án, chủ dự án tự tạo Secret Key và bấm Thêm):
  URL `https://viettable-beta.vercel.app/api/payments/sepay/webhook`, Tiền vào, JSON, tự gửi lại, tài khoản BIDV ****0124 tất cả VA,
  "Dùng để xác thực thanh toán" bật, "Chỉ gửi khi có mã thanh toán" tắt, HMAC-SHA256.
- Chủ dự án sửa trên Vercel (Production): `Payments__SePay__WebhookSecret`, `Payments__SePay__AccountNumbers` (= `962477LXG8,8830150124`),
  `Payments__BankTransfer__AccountNumber` (= `962477LXG8`), BankBin `970418`, BankName `BIDV`, AccountName `NGUYEN VINH QUANG`.
  Claude không đọc giá trị biến (chỉ thấy tên).

## Deploy lại (chủ dự án duyệt "Đã xong, deploy đi")
- `dpl_C5JAwg2LhBAwKTN9ZDAETcti82NR` (`https://viettable-hwvin4ifs-viet-table.vercel.app`), Ready, production, 01:30:50 10/11,
  cùng thư mục/mã với `dpl_9B2tf7N8…` (backend `6dde112`), chỉ để nạp biến mới. Bản quay lui: `dpl_9B2tf7N8NCJSapHRjdw3EVxYebcs`.
- Gọi 1 lần: `/healthz` 200 1,2 s, `/readyz` 200 1,0 s, `/` 200, `/app/cashier/` 200, `/app/chef/` 200, `/app/admin/` 200,
  `/api/mobile/v1/branches` 200; webhook không chữ ký 401.

## Chưa xác nhận
- Chưa có giao dịch thật qua webhook mới: cần chủ dự án thanh toán thử một hoá đơn nhỏ mới (QR phải hiện số `962477LXG8`).
- TT53 (tiền vào số chính) không tự xác nhận được: thu ngân xác nhận tay ở "Chờ thu tiền".
