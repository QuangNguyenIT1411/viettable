# RUN 2026-11-10: giao dịch thật đầu tiên qua webhook SePay mới

Tiếp theo `RUN_2026-11-10_SEPAY_SETUP.md`, trên `dpl_C5JAwg2LhBAwKTN9ZDAETcti82NR`.

Log Production (`vercel logs`, chỉ đọc):
- 01:32:27 `POST /api/payments/sepay/webhook` 401: lệnh thử không chữ ký của Claude sau deploy.
- 01:33:50 `GET /api/mobile/v1/payments/53/transfer-qr` 200: app tạo lại QR cho giao dịch 53 (nay trỏ VA `962477LXG8`).
- 01:34:12 `POST /api/payments/sepay/webhook` **200**: SePay gửi, chữ ký HMAC khớp.

SePay → Webhooks: "VietTable … Có tiền vào · Vừa xong".
Chủ dự án báo app hiện màn **Thanh toán thành công** (10/11 ~01:34).

Kết luận: chuyển khoản VietQR tự xác nhận qua SePay hoạt động trên Production. TT53 đã được xác nhận bằng chính giao dịch này,
không cần thu ngân xác nhận tay.
