# Evidence — mốc test trình duyệt web staff (trước redesign), 13/10/2026

Phạm vi: LOCAL docker, không đụng Vercel/Supabase. Branch `design/staff-redesign` (tạo từ `qa/vt-ho-01` @ `8490469`).

## Môi trường
- `docker start selfrestaurant-db selfrestaurant-rabbitmq` (container có sẵn, dữ liệu SQL Server local cũ).
- `docker compose build catalog orders customers identity billing gateway` → lần 1 lỗi TS2307 (gateway `Dockerfile` không chép
  `src/Frontend/shared/`); sửa Dockerfile local → exit 0.
- `docker compose up -d --no-deps …` (không chạy `db-init`). `/readyz` gateway + 5 service = 200.
- Playwright `@playwright/test` 1.49.1, Google Chrome có sẵn (`channel: "chrome"`), 1366×768, 1 worker.

## Lệnh và kết quả
`cd SelfRestaurant-master/tests/staff-e2e; npx playwright test`

| Lần | Kết quả |
|---|---|
| 1 | 16 passed, 1 skipped (34–45 s) |
| 2 | 16 passed, 1 skipped, exit 0 |

| File | Test | Kết quả |
|---|---|---|
| auth | đăng nhập + đăng xuất chef / cashier / admin; sai mật khẩu | 4 PASS |
| chef | đơn mới → Bắt đầu nấu → Hoàn thành → cột Sẵn sàng; trang Lịch sử & Tài khoản | 2 PASS |
| cashier | sơ đồ bàn có khách; xác nhận thu tiền tại chỗ (phiên CLOSED); nút Thanh toán dashboard (phiên CLOSED); trang Lịch sử; Báo cáo ngày | 5 PASS |
| cashier | hóa đơn phiên app hiện trong Lịch sử thu ngân | SKIP (`test.fixme`, lỗi chức năng F1 đã biết) |
| admin | Tổng quan; Bàn & QR (QR → `/Menu/FromQr?code=QR-Q1-B01`); Món ăn; Nhân viên + Nhật ký; Báo cáo + Top món | 5 PASS |

Dữ liệu do test tạo trên DB local: khách `e2e.<id>@viettable.local`, phiên/đơn/thanh toán ở bàn `QR-Q1-B11..B20`.
Ảnh chụp giao diện mốc: `docs/design/baseline-2026-10-13/`. Rà soát: `docs/design/STAFF_UI_AUDIT_2026-10-13.md`.
"16 PASS" chỉ chứng minh các luồng trên chạy được trên local; không phải nghiệm thu E2E Production.
