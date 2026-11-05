# Công thức tính tiền (một công thức cho app + thu ngân + quầy)

Cập nhật 05/11/2026 (chủ dự án): thêm **phí phục vụ** và **VAT** theo chi nhánh. Nguồn duy nhất trong code:
`src/Services/SelfRestaurant.Billing.Api/Infrastructure/Payments/CheckoutPricing.cs` (`Compute`, `Apply`). App và web thu ngân
**không tự tính**: app đọc báo giá (`/checkout/quote`), thu ngân đọc `checkout/preview`, hoá đơn đọc snapshot.

## Thứ tự

| Bước | Công thức | Ghi chú |
|---|---|---|
| 1. Tạm tính | Σ (đơn giá × số lượng) của món chưa huỷ | Từ Orders, lúc tạo báo giá |
| 2. Giảm giá | khuyến mãi **hoặc** voucher **hoặc** giảm tay của thu ngân — lấy cái lợi hơn, không cộng dồn | Như trước (27/10) |
| 3. Điểm | tối đa 10% của (tạm tính − giảm giá), bước 1.000 điểm, 1 điểm = 10đ | Như trước; giới hạn 10% **không** tính trên phí/VAT |
| 4. Phí phục vụ | `round(sau điểm × phí%)` | Làm tròn tới đồng, 0,5 lên |
| 5. VAT | `round((sau điểm + phí phục vụ) × VAT%)` | Làm tròn tới đồng, 0,5 lên |
| 6. Cọc | trừ cọc đã trả, không quá số ở bước 5 | Số phải trả = (sau điểm + phí + VAT) − cọc |

Ví dụ (test `BillChargesSnapshotTests.Every_step_together_promotion_points_fee_vat_deposit`): tạm tính 500.000đ, khuyến mãi
−50.000đ, dùng 2.000 điểm −20.000đ → 430.000đ; phí 5% = 21.500đ; VAT 8% × 451.500đ = 36.120đ; cọc −300.000đ → **phải trả
187.620đ** (tổng trước cọc 487.620đ).

## Cấu hình

- Theo **chi nhánh**: Admin → Chi nhánh → "Phí phục vụ & VAT" (`billing."BranchChargeSettings"`). 0 = tắt. Phí 0–30%, VAT 0–20%,
  tối đa 2 chữ số thập phân. Chi nhánh chưa có dòng = 0% / 0%.
- **Mặc định khi deploy: 0% / 0%** cho mọi chi nhánh → tổng tiền giữ nguyên tới đồng (test `Zero_percent_keeps_the_old_total_to_the_dong`).
- Phần trăm được **chụp lại vào báo giá** lúc tạo báo giá (và vào hoá đơn quầy lúc thanh toán). Đổi % sau đó không làm đổi
  báo giá đang mở hay hoá đơn đã xuất.

## Snapshot hoá đơn

Hoá đơn app (`billing."Invoices"`, `SnapshotVersion = 1`) lưu lúc thanh toán: danh sách món (tên, đơn giá, số lượng, thành tiền),
tạm tính, khuyến mãi, voucher, điểm dùng + tiền điểm, % phí + tiền phí, % VAT + tiền VAT, cọc, số phải trả, phương thức, mã giao dịch
ngân hàng (chuyển khoản), điểm được cộng, chi nhánh (tên, địa chỉ), bàn, giờ vào, giờ thanh toán, thu ngân. Hoá đơn quầy
(`billing."Bills"`) lưu thêm % phí/VAT, tiền phí/VAT, danh sách món, tên thu ngân.

Hoá đơn cũ (trước khi chạy migration `BillSnapshots`): `SnapshotVersion = 0`. Màn hình hiện như trước: không có dòng phí/VAT
(trước 05/11 chưa từng thu), món vẫn từ `ItemsJson` đã lưu, tên chi nhánh lấy theo chi nhánh hiện tại nếu hoá đơn không có.

## Hiển thị

"Phí phục vụ (5%)", "VAT (8%)" — phần trăm lấy từ báo giá/hoá đơn, không viết cứng. 0% → ẩn dòng.
