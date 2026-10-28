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
