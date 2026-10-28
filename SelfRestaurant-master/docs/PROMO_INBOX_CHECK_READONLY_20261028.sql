-- READ-ONLY. Why no "Ưu đãi mới" reached the "Ưu đãi" tab (owner's test 28/10/2026). Run in the Supabase SQL Editor.
-- Nothing here writes. Compare the numbers with the explanation in Claude's report.

-- 1) How many active customer accounts the announcement had to reach (the old code made ONE query per customer
--    inside a single 10-second request; many test accounts = timeout = nobody got it, and every retry started over).
SELECT count(*) AS active_customers FROM identity."Customers" WHERE "IsActive" = true;

-- 2) Promotions and whether Billing recorded them as announced (NotifiedAtUtc NULL = never finished).
SELECT "PromotionId", "Name", "IsActive", "StartsAtUtc", "EndsAtUtc", "BranchIds", "NotifiedAtUtc", "CreatedAtUtc"
FROM billing."Promotions" ORDER BY "PromotionId";

-- 3) "Ưu đãi mới" rows actually in inboxes, per promotion (OrderItemId = PromotionId).
SELECT "OrderItemId" AS promotion_id, count(*) AS inbox_rows, min("CreatedAtUtc") AS first_row, max("CreatedAtUtc") AS last_row
FROM customers."ReadyDishNotifications"
WHERE "EventName" = 'promotion.created.v1'
GROUP BY "OrderItemId" ORDER BY "OrderItemId";

-- 4) (added 29/10) "blackday" took −17.500đ on a 35.000đ bill (Bàn 2, Đơn #60) although the admin meant a 300.000đ
--    minimum. Shows every money field EXACTLY as stored. If "MinSubtotal" is 300 (not 300000), the admin form saved
--    "300.000" as 300đ: a number box reads the dot as a decimal point. "suspicious_money" lists such values.
SELECT "PromotionId", "Name", "IsActive", "DiscountType", "DiscountValue", "MaxDiscount", "MinSubtotal", "BranchIds",
       "StartsAtUtc", "EndsAtUtc", "UpdatedAtUtc",
       concat_ws(', ',
         CASE WHEN "MinSubtotal" > 0 AND "MinSubtotal" < 1000 THEN 'MinSubtotal < 1.000đ' END,
         CASE WHEN "MaxDiscount" > 0 AND "MaxDiscount" < 1000 THEN 'MaxDiscount < 1.000đ' END,
         CASE WHEN "DiscountType" = 'FIXED' AND "DiscountValue" < 1000 THEN 'DiscountValue (FIXED) < 1.000đ' END) AS suspicious_money
FROM billing."Promotions"
ORDER BY "PromotionId";

-- 5) (added 29/10) The same check for vouchers.
SELECT "VoucherId", "Code", "Name", "IsActive", "DiscountType", "DiscountValue", "MaxDiscount", "MinSubtotal",
       concat_ws(', ',
         CASE WHEN "MinSubtotal" > 0 AND "MinSubtotal" < 1000 THEN 'MinSubtotal < 1.000đ' END,
         CASE WHEN "MaxDiscount" > 0 AND "MaxDiscount" < 1000 THEN 'MaxDiscount < 1.000đ' END,
         CASE WHEN "DiscountType" = 'FIXED' AND "DiscountValue" < 1000 THEN 'DiscountValue (FIXED) < 1.000đ' END) AS suspicious_money
FROM billing."Vouchers"
ORDER BY "VoucherId";

-- 6) (added 29/10) Counter bills already paid with a promotion: a bill far below the minimum the admin MEANT
--    (e.g. 35.000đ against "300.000đ") was discounted by mistake.
SELECT b."BillID", b."BillCode", b."BillTime", b."Subtotal", b."PromotionDiscount", p."Name", p."MinSubtotal"
FROM billing."Bills" b JOIN billing."Promotions" p ON p."PromotionId" = b."PromotionId"
WHERE b."PromotionDiscount" > 0
ORDER BY b."BillID" DESC;
