-- INCIDENT 25/10/2026 — sửa dữ liệu phiên #34 (Bàn 6). Chủ dự án xem trước rồi TỰ chạy trong Supabase SQL Editor.
-- Một transaction duy nhất. Mọi điều kiện được kiểm tra trước; sai một điều kiện → RAISE EXCEPTION → không đổi gì.
--
-- Kết quả mong muốn (chủ dự án duyệt 25/10):
--   * Giao dịch 28 (chuyển khoản SePay, 35.000đ) là khoản DUY NHẤT được tính; hóa đơn của nó giữ nguyên.
--   * Giao dịch 29 (tiền mặt, PAY-…022415, chưa thu) → CANCELLED. Không thu tiền 2 lần.
--   * Các báo giá mở sau báo giá đã trả → CANCELLED.
--   * Đơn đã được giao dịch 28 trả → COMPLETED; đơn #49 (gọi thêm, chưa trả, dữ liệu thử) → CANCELLED.
--   * Phiên #34 → CLOSED; yêu cầu phục vụ còn mở → CANCELLED; Bàn 6 → trống.
--   * Hóa đơn đánh dấu đã hoàn tất đóng phiên (bỏ qua cộng điểm thành viên lần này).
--   * Dòng BankTransferEvents của giao dịch SePay 41419: RECEIVED → AUTO_CONFIRMED (bị kẹt do lỗi code, xem báo cáo).

BEGIN;

DO $$
DECLARE
    v_session      orders."DiningSessions"%ROWTYPE;
    v_paid_quote   integer;
    v_branch       integer;
    v_cancel_id    integer;
    v_complete_id  integer;
    v_available_id integer;
    v_n            integer;
BEGIN
    -- ---------- Kiểm tra điều kiện (không ghi gì) ----------
    SELECT * INTO v_session FROM orders."DiningSessions" WHERE "DiningSessionId" = 34 FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Không có phiên #34'; END IF;
    IF v_session."EndedAtUtc" IS NOT NULL THEN RAISE EXCEPTION 'Phiên #34 đã kết thúc rồi — dừng.'; END IF;

    SELECT "CheckoutQuoteId" INTO v_paid_quote FROM billing."MobilePayments"
     WHERE "PaymentId" = 28 AND "DiningSessionId" = 34 AND "Status" = 'SUCCEEDED' AND "ProviderName" = 'SEPAY' FOR UPDATE;
    IF v_paid_quote IS NULL THEN RAISE EXCEPTION 'Giao dịch 28 không phải SUCCEEDED/SEPAY của phiên #34 — dừng.'; END IF;

    SELECT count(*) INTO v_n FROM billing."MobilePayments" WHERE "DiningSessionId" = 34 AND "Status" = 'SUCCEEDED';
    IF v_n <> 1 THEN RAISE EXCEPTION 'Phiên #34 có % giao dịch thành công (mong đợi 1) — dừng.', v_n; END IF;

    PERFORM 1 FROM billing."MobilePayments"
     WHERE "PaymentId" = 29 AND "DiningSessionId" = 34 AND "Status" = 'PENDING' AND "Method" = 'CASH' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Giao dịch 29 không phải CASH/PENDING của phiên #34 — dừng.'; END IF;

    SELECT count(*) INTO v_n FROM billing."Invoices" WHERE "DiningSessionId" = 34 AND "PaymentId" = 28 AND "CheckoutQuoteId" = v_paid_quote;
    IF v_n <> 1 THEN RAISE EXCEPTION 'Không đúng 1 hóa đơn của giao dịch 28 (thấy %) — dừng.', v_n; END IF;

    SELECT "BranchId" INTO v_branch FROM billing."CheckoutQuotes" WHERE "CheckoutQuoteId" = v_paid_quote;

    PERFORM 1 FROM orders."Orders" WHERE "OrderID" = 49 AND "DiningSessionCode" = v_session."SessionCode";
    IF NOT FOUND THEN RAISE EXCEPTION 'Đơn #49 không thuộc phiên #34 — dừng.'; END IF;
    IF EXISTS (SELECT 1 FROM billing."CheckoutQuotes" q, jsonb_array_elements(q."ItemsJson"::jsonb) e
               WHERE q."CheckoutQuoteId" = v_paid_quote AND (e->>'OrderId')::int = 49) THEN
        RAISE EXCEPTION 'Đơn #49 nằm trong báo giá đã trả — không được hủy. Dừng.';
    END IF;

    SELECT "StatusID" INTO v_cancel_id   FROM orders."OrderStatus" WHERE "StatusCode" = 'CANCELLED';
    SELECT "StatusID" INTO v_complete_id FROM orders."OrderStatus" WHERE "StatusCode" = 'COMPLETED';
    SELECT "StatusID" INTO v_available_id FROM catalog."TableStatus" WHERE "StatusCode" = 'AVAILABLE';
    IF v_cancel_id IS NULL OR v_complete_id IS NULL OR v_available_id IS NULL THEN
        RAISE EXCEPTION 'Thiếu mã trạng thái CANCELLED/COMPLETED/AVAILABLE — dừng.';
    END IF;

    -- ---------- Billing ----------
    UPDATE billing."MobilePayments"
       SET "Status" = 'CANCELLED', "FailureCode" = 'incident_20261025_duplicate', "UpdatedAtUtc" = now(), "CompletedAtUtc" = now()
     WHERE "PaymentId" = 29;

    UPDATE billing."CheckoutQuotes"
       SET "Status" = 'CANCELLED', "UpdatedAtUtc" = now()
     WHERE "DiningSessionId" = 34 AND "CheckoutQuoteId" <> v_paid_quote AND "Status" IN ('OPEN', 'PAYMENT_REQUESTED');

    UPDATE billing."Invoices"
       SET "SettlementCompletedAtUtc" = now(), "RecoveryLastAttemptAtUtc" = now()
     WHERE "DiningSessionId" = 34 AND "PaymentId" = 28 AND "SettlementCompletedAtUtc" IS NULL;

    UPDATE billing."BankTransferEvents"
       SET "Outcome" = 'AUTO_CONFIRMED', "PaymentId" = 28, "BranchId" = v_branch, "Overpaid" = false, "ProcessedAtUtc" = now()
     WHERE "Provider" = 'SEPAY' AND "ProviderTransactionId" = '41419' AND "Outcome" = 'RECEIVED';

    -- ---------- Orders ----------
    UPDATE orders."OrderItems" SET "StatusCode" = 'CANCELLED'
     WHERE "OrderID" = 49 AND "StatusCode" <> 'CANCELLED';

    UPDATE orders."Orders"
       SET "StatusID" = v_cancel_id, "IsActive" = false, "CompletedTime" = now()
     WHERE "OrderID" = 49;

    UPDATE orders."Orders"
       SET "StatusID" = v_complete_id, "IsActive" = false, "CompletedTime" = now()
     WHERE "DiningSessionCode" = v_session."SessionCode" AND COALESCE("IsActive", true)
       AND "OrderID" IN (SELECT (e->>'OrderId')::int FROM billing."CheckoutQuotes" q, jsonb_array_elements(q."ItemsJson"::jsonb) e
                         WHERE q."CheckoutQuoteId" = v_paid_quote);

    SELECT count(*) INTO v_n FROM orders."Orders"
     WHERE "DiningSessionCode" = v_session."SessionCode" AND COALESCE("IsActive", true);
    IF v_n <> 0 THEN RAISE EXCEPTION 'Còn % đơn đang hoạt động không thuộc báo giá đã trả và không phải #49 — dừng.', v_n; END IF;

    UPDATE orders."ServiceRequests"
       SET "Status" = 'CANCELLED', "CancelledAtUtc" = now(), "UpdatedAtUtc" = now()
     WHERE "DiningSessionId" = 34 AND "Status" IN ('OPEN', 'ACKNOWLEDGED');

    UPDATE orders."DiningSessions"
       SET "Status" = 'CLOSED', "PaymentId" = 28, "CheckoutQuoteId" = v_paid_quote, "EndedAtUtc" = now(), "UpdatedAtUtc" = now()
     WHERE "DiningSessionId" = 34;

    -- ---------- Catalog: trả bàn (chỉ khi bàn không còn phiên nào khác đang mở) ----------
    IF NOT EXISTS (SELECT 1 FROM orders."DiningSessions" WHERE "TableId" = v_session."TableId" AND "EndedAtUtc" IS NULL) THEN
        UPDATE catalog."DiningTables"
           SET "StatusID" = v_available_id, "CurrentOrderID" = NULL, "UpdatedAt" = now()
         WHERE "TableID" = v_session."TableId";
    END IF;

    RAISE NOTICE 'Đã sửa phiên #34 (bàn %, báo giá đã trả %).', v_session."TableId", v_paid_quote;
END $$;

COMMIT;

-- ---------- Kiểm tra sau khi chạy (chỉ đọc) ----------
SELECT "DiningSessionId", "Status", "PaymentId", "CheckoutQuoteId", "EndedAtUtc" FROM orders."DiningSessions" WHERE "DiningSessionId" = 34;
-- mong đợi: CLOSED, 28, <báo giá đã trả>, có thời gian

SELECT o."OrderID", o."IsActive", s."StatusCode" FROM orders."Orders" o JOIN orders."OrderStatus" s ON s."StatusID" = o."StatusID"
WHERE o."DiningSessionCode" = (SELECT "SessionCode" FROM orders."DiningSessions" WHERE "DiningSessionId" = 34) ORDER BY 1;
-- mong đợi: đơn đã trả = COMPLETED, #49 = CANCELLED, tất cả IsActive = false

SELECT "PaymentId", "Status", "Method", "ProviderName", "FailureCode" FROM billing."MobilePayments" WHERE "DiningSessionId" = 34 ORDER BY 1;
-- mong đợi: 28 SUCCEEDED SEPAY; 29 CANCELLED incident_20261025_duplicate

SELECT "CheckoutQuoteId", "Status" FROM billing."CheckoutQuotes" WHERE "DiningSessionId" = 34 ORDER BY 1;
-- mong đợi: báo giá đã trả = PAID, còn lại CANCELLED (hoặc EXPIRED)

SELECT "InvoiceId", "PaymentId", "TotalAmount", "SettlementCompletedAtUtc" FROM billing."Invoices" WHERE "DiningSessionId" = 34;
-- mong đợi: 1 dòng, PaymentId 28, 35000, có thời gian

SELECT "ProviderTransactionId", "Outcome", "PaymentId", "BranchId" FROM billing."BankTransferEvents";
-- mong đợi: 41419, AUTO_CONFIRMED, 28, 1

SELECT t."TableID", s."StatusCode", t."CurrentOrderID" FROM catalog."DiningTables" t JOIN catalog."TableStatus" s ON s."StatusID" = t."StatusID"
WHERE t."TableID" = (SELECT "TableId" FROM orders."DiningSessions" WHERE "DiningSessionId" = 34);
-- mong đợi: AVAILABLE, CurrentOrderID rỗng
