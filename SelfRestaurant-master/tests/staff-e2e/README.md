# Staff web e2e (Playwright)

Browser regression tests for Chef, Cashier and Admin web. **LOCAL docker stack only**: the tests register
customers, open tables, place orders and take payments. `playwright.config.ts` refuses any non-localhost URL.

## Chuẩn bị (một lần)

```powershell
# 1. DB + RabbitMQ đã có sẵn (không chạy db-init):
docker start selfrestaurant-db selfrestaurant-rabbitmq
# 2. Build image từ code hiện tại và bật service, KHÔNG kéo theo db-init:
cd SelfRestaurant-master
docker compose build catalog orders customers identity billing gateway
docker compose up -d --no-deps catalog orders customers identity billing gateway
# 3. Kiểm tra: http://localhost:5100/readyz trả 200
# 4. Cài Playwright (dùng Google Chrome có sẵn, không tải trình duyệt):
cd tests/staff-e2e
npm ci
```

Sau mỗi lần sửa giao diện: build lại `gateway` (web staff được gateway phục vụ) rồi chạy test:

```powershell
docker compose build gateway; docker compose up -d --no-deps gateway
cd tests/staff-e2e; npx playwright test
```

## Tài khoản

Seed local (`sql/sqlchuan.cleaned.sql`, chi nhánh 1): `chef_hung`, `cashier_lan`, `admin`, mật khẩu seed `123456`.
Đổi bằng biến môi trường `E2E_CHEF_USER`, `E2E_CASHIER_USER`, `E2E_ADMIN_USER`, `E2E_STAFF_PASSWORD`.
Cả 3 vai trò đăng nhập ở trang chung `/app/chef/Staff/Account/Login`.

## Phạm vi

| File | Nội dung |
|---|---|
| `specs/auth.spec.ts` | Đăng nhập/đăng xuất từng vai trò, sai mật khẩu |
| `specs/chef.spec.ts` | Đơn mới hiện ở "Chờ chế biến" → Bắt đầu nấu → Hoàn thành → "Sẵn sàng"; trang Lịch sử & Tài khoản |
| `specs/cashier.spec.ts` | Sơ đồ bàn, xác nhận thanh toán tại chỗ của app, nút Thanh toán dashboard, Lịch sử, Báo cáo ngày |
| `specs/admin.spec.ts` | Tổng quan, Bàn & QR (QR trỏ `/Menu/FromQr`), Món ăn, Nhân viên + nhật ký, Báo cáo (chỉ đọc) |

Dữ liệu khách được tạo qua API mobile local (`support/customer.ts`) và dùng bàn `QR-Q1-B11`…`B20`.
Ảnh QR từ `api.qrserver.com` được giả lập để test không cần Internet.

Test `test.fixme` trong `cashier.spec.ts` là **lỗi chức năng đã biết** (hóa đơn phiên app không hiện trong Lịch sử của thu ngân),
không phải lỗi giao diện.
