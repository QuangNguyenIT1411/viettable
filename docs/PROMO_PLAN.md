# Kế hoạch: Khuyến mãi (27/10/2026)

Chủ dự án giao 27/10. Làm tự động tới cuối; **dừng trước khi chạy SQL và deploy.**

## 1. Quy tắc (chủ dự án chốt)

| Quy tắc | Cách làm |
|---|---|
| Admin tạo chương trình: tên, mô tả, ảnh (tuỳ chọn), loại giảm (% có trần / số tiền cố định), đơn tối thiểu, chi nhánh áp dụng, bắt đầu–kết thúc, bật/tắt | Web admin: màn "Khuyến mãi" (danh sách + form). Billing giữ bảng `billing."Promotions"`. |
| App: banner Trang chủ + màn "Ưu đãi" hiện khuyến mãi đang chạy của chi nhánh đang chọn; bấm xem chi tiết | `GET /api/mobile/v1/promotions?branchId=` (công khai) và `GET /api/mobile/v1/promotions/{id}`. |
| Tự áp vào hoá đơn khi đủ điều kiện, tối đa 1 khuyến mãi tốt nhất | Báo giá chọn khuyến mãi giảm nhiều nhất cho chi nhánh + tạm tính lúc đó. Dòng "Khuyến mãi −X đ". |
| Không cộng dồn với voucher: chọn cái lợi hơn cho khách và ghi rõ | Báo giá giữ cả hai "đề nghị", chỉ áp cái lớn hơn; dòng giải thích `discountNote`. |
| Thứ tự: tạm tính → khuyến mãi/voucher → điểm (10% tính trên số sau khuyến mãi) → cọc | Một hàm thuần duy nhất `CheckoutPricing.Compute` ở Billing, dùng cho báo giá app, thanh toán phiên app ở quầy và thanh toán đơn tại quầy. |
| Thông báo loại "Ưu đãi" khi có khuyến mãi mới | Khi khuyến mãi bắt đầu chạy (bật + trong thời gian), mỗi khách nhận 1 thông báo "Ưu đãi mới" (sự kiện `promotion.created.v1`, nhóm "offer" = tab "Ưu đãi" đã có). |

## 2. Dữ liệu

`billing."Promotions"`: PromotionId (identity), Name (200), Description (1000, null), ImageUrl (1000, null),
DiscountType (`PERCENT`/`FIXED`), DiscountValue, MaxDiscount (null = không trần; chỉ cho %), MinSubtotal,
BranchIds (danh sách "1,2"; null = mọi chi nhánh), StartsAtUtc, EndsAtUtc, IsActive, NotifiedAtUtc (đã báo khách),
CreatedAtUtc, UpdatedAtUtc.

`billing."CheckoutQuotes"` thêm: PromotionId, PromotionName, PromotionOfferDiscount (khuyến mãi tốt nhất lúc tạo báo giá),
PromotionDiscount (đã áp), VoucherOfferDiscount (voucher khách chọn), DiscountNote. `VoucherDiscount` giờ là phần voucher **đã áp**.
`billing."Invoices"` thêm PromotionDiscount. `billing."Bills"` (thanh toán đơn tại quầy) thêm PromotionId, PromotionDiscount.

## 3. Công thức (CheckoutPricing.Compute)

1. Khuyến mãi hợp lệ: đang bật, trong thời gian, đúng chi nhánh (hoặc mọi chi nhánh), tạm tính ≥ đơn tối thiểu.
   Giảm: % → floor(tạm tính × %), không quá trần; cố định → số tiền; không quá tạm tính. Lấy cái giảm nhiều nhất.
2. So với voucher (app) hoặc giảm tay của thu ngân (quầy): lấy cái lớn hơn (bằng nhau: khuyến mãi), ghi rõ cái nào được áp.
3. Điểm: theo LoyaltyRules, giới hạn 10% tính trên **số sau khuyến mãi/voucher**.
4. Cọc: trừ tới số còn lại (không âm).

## 4. API

- Admin (Gateway `api/gateway/admin/promotions`): GET danh sách, POST tạo, PUT sửa, POST `{id}/toggle`.
- App: `GET /api/mobile/v1/promotions?branchId=`, `GET /api/mobile/v1/promotions/{id}`. Báo giá trả thêm
  `promotionName`, `promotionDiscount`, `discountNote`; hoá đơn trả `promotionDiscount`.
- Thu ngân: thẻ đơn có `promotionId/promotionName/promotionDiscount` để màn quầy xem trước; số tiền thật do backend tính.
- Nội bộ: Billing `api/internal/promotions` (CRUD + `active` + `notify-due`); Customers `api/internal/inbox/broadcast`.

## 5. Màn app (Phố đêm)

- Trang chủ: banner plum khuyến mãi tốt nhất đang chạy (tên, mức giảm, hạn) → màn "Ưu đãi".
- Màn "Ưu đãi": danh sách thẻ khuyến mãi; bấm → chi tiết (ảnh nếu có, mô tả, mức giảm, đơn tối thiểu, chi nhánh, thời gian,
  "Tự áp vào hoá đơn khi đủ điều kiện").
- Hoá đơn: dòng "Khuyến mãi −X đ" + dòng giải thích khi khuyến mãi và voucher cùng có.

## 6. Rủi ro

| Rủi ro | Cách giảm |
|---|---|
| Khuyến mãi hết hạn giữa lúc báo giá mở | Báo giá chốt mức giảm lúc tạo (báo giá chỉ sống 15 phút). |
| Báo giá cũ (trước bản này) mở dở | Hết hạn sau 15 phút; báo giá đã trả tiền không bị tính lại. |
| Màn quầy xem trước khác số backend | Số thật lấy theo backend khi thanh toán; màn quầy dùng đúng quy tắc chọn cái lớn hơn. |
| Bảng mới thiếu quyền runtime | File SQL kèm GRANT + kiểm tra. |
