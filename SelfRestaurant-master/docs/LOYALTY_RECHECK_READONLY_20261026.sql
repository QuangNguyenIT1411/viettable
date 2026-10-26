-- VietTable: điểm thưởng đã tích / đã đổi theo quy tắc CŨ, so với quy tắc MỚI (chủ dự án chốt 26/10/2026).
-- CHỈ ĐỌC: toàn bộ là SELECT, không sửa dữ liệu. Chạy từng khối trong Supabase SQL Editor (schema identity, billing).
--   Quy tắc cũ:  tích = floor(số tiền trả × 1%)      | đổi trên app = 1.000đ/điểm | đổi ở thu ngân = 1đ/điểm
--   Quy tắc mới: tích = floor(số tiền trả / 1.000)  | đổi (app + thu ngân) = 10đ/điểm, bước 1.000 điểm

-- 1) Số dư hiện tại của các tài khoản có điểm.
SELECT c."CustomerID", c."Username", c."Name", c."LoyaltyPoints"
FROM identity."Customers" c
WHERE COALESCE(c."LoyaltyPoints", 0) <> 0
ORDER BY c."LoyaltyPoints" DESC;

-- 2) Điểm TÍCH qua app (quyết toán có key "mobile-invoice:..."): đã cộng theo 1% so với đúng ra theo 1 điểm/1.000đ.
SELECT s."CustomerId", c."Username", s."SettlementKey", s."AmountPaid", s."PointsUsed",
       (s."ResponseJson"::jsonb ->> 'pointsEarned')::int                                   AS earned_old,
       floor(s."AmountPaid" / 1000)::int                                                  AS earned_new,
       (s."ResponseJson"::jsonb ->> 'pointsEarned')::int - floor(s."AmountPaid" / 1000)::int AS earned_excess
FROM identity."LoyaltySettlements" s
LEFT JOIN identity."Customers" c ON c."CustomerID" = s."CustomerId"
ORDER BY s."CustomerId", s."SettlementKey";

-- 3) Điểm TÍCH ở thu ngân (lệnh checkout đã xong).
SELECT cc."BillCode", cc."CustomerName", cc."TotalAmount", cc."PointsUsed", cc."PointsEarned" AS earned_old,
       floor(cc."TotalAmount" / 1000)::int                           AS earned_new,
       cc."PointsEarned" - floor(cc."TotalAmount" / 1000)::int       AS earned_excess
FROM billing."CheckoutCommands" cc
WHERE cc."Status" = 'COMPLETED' AND COALESCE(cc."PointsEarned", 0) > 0
ORDER BY cc."BillCode";

-- 4) Điểm ĐỔI trên app: số tiền đã giảm theo 1.000đ/điểm so với đúng ra 10đ/điểm.
SELECT q."CheckoutQuoteId", q."CustomerId", q."Status", q."PointsApplied",
       q."PointsDiscount"                   AS discount_old,
       q."PointsApplied" * 10               AS discount_new,
       q."PointsDiscount" - q."PointsApplied" * 10 AS discount_excess
FROM billing."CheckoutQuotes" q
WHERE q."PointsApplied" > 0
ORDER BY q."CheckoutQuoteId";

-- 5) Điểm ĐỔI ở thu ngân: số tiền đã giảm theo 1đ/điểm so với đúng ra 10đ/điểm.
SELECT b."BillCode", b."CustomerID", b."BillTime", b."PointsUsed",
       b."PointsDiscount"             AS discount_old,
       b."PointsUsed" * 10            AS discount_new
FROM billing."Bills" b
WHERE COALESCE(b."PointsUsed", 0) > 0
ORDER BY b."BillTime";
