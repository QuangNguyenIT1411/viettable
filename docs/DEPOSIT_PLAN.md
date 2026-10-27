# Kế hoạch: Đặt cọc khi đặt bàn (27/10/2026)

Chủ dự án giao 27/10. Làm tự động tới cuối; **dừng trước 2 việc chủ dự án tự làm: chạy SQL trên Supabase và duyệt deploy.**
Không cài APK.

## 1. Quy tắc (chủ dự án chốt)

| Quy tắc | Cách làm |
|---|---|
| Cờ `Reservations__Deposit__Enabled`, mặc định TẮT | Gateway đọc cờ. Tắt: đặt bàn y như hiện nay, app không hiện gì về cọc. |
| Từ 6 khách: 50.000đ × số khách | `Reservations:Deposit:MinGuests` (6), `PerGuestVnd` (50000). Backend trả cho app qua `GET /api/mobile/v1/reservations/deposit-policy`. |
| Trả bằng VietQR, tự xác nhận qua webhook SePay sẵn có | Nội dung CK riêng **`VT DC<mã đặt bàn>`**, tách hẳn với `VT B<cn> TT<giao dịch>` của thanh toán bàn. |
| Chưa trả trong 15 phút → tự huỷ, nhả bàn | `PaymentMinutes` (15). Bàn được nhả ngay khi quá hạn (truy vấn bàn trống bỏ qua đặt bàn chờ cọc đã quá hạn); trạng thái chuyển "Đã huỷ" khi có lượt đọc tiếp theo (không cần chạy nền, hợp với Vercel). |
| Đã cọc: trừ vào hoá đơn cuối | Báo giá có thêm dòng **"Đã đặt cọc −X đ"**; cọc chuyển APPLIED khi hoá đơn đó thanh toán xong. |
| Huỷ ≥ 2 giờ trước giờ đến → "cần hoàn cọc"; muộn hơn / không đến → mất cọc | `RefundCutoffHours` (2). Không tự hoàn tiền; thu ngân/admin hoàn tay rồi bấm "Đã hoàn cọc". |
| Web thu ngân/admin | Chỉ thêm hiển thị trạng thái cọc + nút "Đã hoàn cọc". |
| Gộp `quote_locked` (CLAUDE.md 6b) | Cùng đợt deploy này. |

## 2. Dữ liệu

### Billing (chủ sở hữu tiền): bảng mới `billing."ReservationDeposits"`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| ReservationDepositId | int identity PK | |
| ReservationId | int, UNIQUE | 1 cọc / 1 đặt bàn |
| CustomerId, BranchId, PartySize | int | |
| Amount | numeric(18,2) | tiền cọc phải trả |
| Status | varchar(30) | xem mục 3 |
| ReservedAtUtc | timestamptz | giờ đến (cho quy tắc 2 giờ) |
| DueAtUtc | timestamptz | hạn trả = lúc tạo + 15 phút |
| PaidAmount, PaidAtUtc, ProviderTransactionId | | từ webhook SePay |
| AppliedCheckoutQuoteId, AppliedAmount, AppliedAtUtc | | khi trừ vào hoá đơn |
| ClosedReason | varchar(300) | "Huỷ trước 2 giờ", "Khách không đến", "Trả sau khi hết hạn"… |
| RefundedAtUtc, RefundedByEmployeeId | | khi bấm "Đã hoàn cọc" |
| CreatedAtUtc, UpdatedAtUtc | | |

`billing."CheckoutQuotes"` thêm `DepositAmount` (cọc có thể trừ), `DepositApplied` (đã trừ), `ReservationDepositId`.
`billing."Invoices"` thêm `DepositApplied`.

### Customers: `customers."Reservations"` thêm `DepositAmount` (numeric, null), `DepositDueAtUtc` (timestamptz, null). Trạng thái mới `PendingDeposit`.

### Orders: không đổi bảng; `checkout-context` trả thêm `reservationId` của phiên.

## 3. Trạng thái

Đặt bàn (Customers): `PendingDeposit` → (đã nhận tiền) `Pending` → `Confirmed` → … như cũ.
`PendingDeposit` quá hạn → `Cancelled` (lý do "Hết hạn đặt cọc").

Cọc (Billing):

| Trạng thái | Nghĩa | Từ đâu |
|---|---|---|
| PENDING | chờ khách chuyển | tạo khi đặt bàn |
| EXPIRED | quá 15 phút chưa trả | đọc lười (khi đọc) |
| CANCELLED | khách huỷ trước khi trả | huỷ |
| PAID | đã nhận đủ tiền | webhook |
| APPLIED | đã trừ vào hoá đơn | thanh toán hoá đơn |
| REFUND_REQUIRED | cần hoàn tay | huỷ ≥ 2 giờ, nhà hàng từ chối, tiền tới sau khi hết hạn |
| REFUNDED | đã hoàn (thu ngân/admin bấm) | nút "Đã hoàn cọc" |
| FORFEITED | mất cọc | huỷ < 2 giờ, không đến |

## 4. API

Gateway (app):
- `GET /api/mobile/v1/reservations/deposit-policy` → `{enabled, minGuests, perGuestVnd, paymentMinutes, refundCutoffHours}`.
- `POST /api/mobile/v1/reservations`: khi cần cọc, đặt bàn tạo ở `PendingDeposit` và tạo cọc ở Billing.
- `GET /api/mobile/v1/reservations/{id}/deposit` → cọc thật + VietQR (khi còn chờ) + hạn trả. Đồng bộ lại nếu bước trước bị lỗi giữa chừng.
- `POST .../cancel`: đóng cọc theo quy tắc 2 giờ.
- `GET /api/mobile/v1/checkout/quotes/{id}` (việc `quote_locked`): tải lại báo giá thật.

Gateway (nhân viên): `GET /api/cashier/deposits`, `POST /api/cashier/deposits/{id}/refunded`,
`GET /api/admin/deposits`, `POST /api/admin/deposits/{id}/refunded`. Từ chối / khách không đến → đóng cọc.

Webhook `POST /api/payments/sepay/webhook` (sẵn có): Billing nhận ra `VT DC<id>` → ghi nhận cọc (cùng bảng
`BankTransferEvents`, chống trùng theo mã giao dịch SePay) → gateway chuyển đặt bàn sang `Pending` và báo khách/thu ngân.

Billing nội bộ: `POST /api/internal/deposits`, `GET /api/internal/deposits/by-reservation/{id}`,
`POST /api/internal/deposits/by-reservation/{id}/close`, `GET /api/internal/deposits?branchId=`,
`POST /api/internal/deposits/{id}/refunded`. Customers nội bộ: `POST /api/internal/reservations/{id}/deposit-paid`.

## 5. Màn app (Phố đêm)

- Bước số khách: thẻ vàng nhạt "Từ 6 khách cần đặt cọc 50.000đ/khách = X" (số từ backend; cờ tắt thì ẩn).
- Bước ghi chú: nút "Tiếp tục đặt cọc (X đ)".
- **Màn đặt cọc mới**: số tiền Display.XL, "vé" VietQR (QR, ngân hàng, STK, chủ TK, nội dung CK + nút sao chép),
  đếm ngược 15 phút, chấm bạc hà "Đang chờ…", tự chuyển sang "Đặt bàn thành công" khi nhận tiền.
- Chi tiết / thành công / huỷ đặt bàn: trạng thái cọc thật; huỷ báo trước "được hoàn" hay "mất cọc".
- Hoá đơn: dòng "Đã đặt cọc −X đ".

## 6. Rủi ro và cách giảm

| Rủi ro | Cách giảm |
|---|---|
| Tiền tới đúng lúc vừa hết hạn | Billing quyết theo `DueAtUtc`; nếu Customers đã huỷ đặt bàn thì cọc thành REFUND_REQUIRED (không mất tiền khách). |
| Webhook nhận tiền xong nhưng gọi Customers lỗi | Lượt đọc cọc kế tiếp (app đang chờ hỏi 5 giây/lần) tự đồng bộ lại. Nếu không ai đọc trước 15 phút, đặt bàn có thể bị huỷ dù đã trả → cọc REFUND_REQUIRED, hiện ở thu ngân. |
| Chuyển thiếu tiền | Giữ PENDING; giao dịch hiện ở "Chuyển khoản cần xử lý" của thu ngân (như thanh toán bàn). |
| Cọc lớn hơn tiền còn phải trả | Chỉ trừ tới số còn phải trả (tổng không âm); phần dư không tự hoàn (ghi ở mục tự quyết). |
| Bảng mới thiếu quyền runtime (bài học 25/10) | File SQL kèm GRANT + kiểm tra `has_table_privilege`. |
| Code mới chạy trước SQL | Code từ chối khởi động khi còn migration chưa chạy (như các lần trước): chạy SQL TRƯỚC khi deploy. |

## 7. Thứ tự làm và commit

1. Kế hoạch này. 2. Backend: quy tắc dùng chung + Billing + Customers + Orders + Gateway + test.
3. `quote_locked` + GET báo giá. 4. File SQL migration (không chạy). 5. Web thu ngân/admin. 6. App. 7. CLAUDE.md 6a.
