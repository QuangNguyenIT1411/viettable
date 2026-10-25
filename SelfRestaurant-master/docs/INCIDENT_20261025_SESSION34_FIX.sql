-- INCIDENT 25/10/2026 — sửa dữ liệu phiên #34 (Bàn 6). Chủ dự án xem trước rồi TỰ chạy trong Supabase SQL Editor.
-- Bản 2 (theo kết quả CHECK của chủ dự án): phiên chỉ có 1 đơn #49; app đã tạo lại báo giá 36–40 và yêu cầu
-- thanh toán lại trên báo giá 36 nên phiên đang khoá theo 36, còn tiền đã trả cho báo giá 35.
-- Một transaction duy nhất. Mọi điều kiện được kiểm tra trước; sai một điều kiện → RAISE EXCEPTION → không đổi gì.
--
-- Kết quả mong muốn (chủ dự án duyệt 25/10):
--   * Giao dịch 28 (chuyển khoản SePay, 35.000đ, báo giá 35) là khoản DUY NHẤT được tính; hóa đơn của nó giữ nguyên.
--   * Giao dịch 29 (tiền mặt, báo giá 36, chưa thu) → CANCELLED. Không thu tiền 2 lần.
--   * Báo giá 36 (PAYMENT_REQUESTED) và 37–40 (OPEN) → CANCELLED.
--   * Đơn #49 nằm trong báo giá 35 đã trả → COMPLETED.
--   * Phiên #34 → CLOSED, gắn giao dịch 28 / báo giá 35; yêu cầu phục vụ còn mở → CANCELLED; Bàn 6 → trống.
--   * Hóa đơn đánh dấu đã hoàn tất đóng phiên (bỏ qua cộng điểm thành viên lần này).
--   * Dòng BankTransferEvents của giao dịch SePay 41419: RECEIVED → AUTO_CONFIRMED (kẹt do lỗi code, đã giải thích).

BEGIN;

DO $$
DECLARE
    v_session      orders."DiningSessions"%ROWTYPE;
    v_branch       integer;
    v_complete_id  integer;
    v_available_id integer;
    v_n            integer;
BEGIN
    -- ---------- Kiểm tra điều kiện (không ghi gì) ----------
    SELECT * INTO v_session FROM orders."DiningSessions" WHERE "DiningSessionId" = 34 FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Không có phiên #34'; END IF;
    IF v_session."EndedAtUtc" IS NOT NULL OR v_session."PaymentId" IS NOT NULL THEN
        RAISE EXCEPTION 'Phiên #34 đã kết thúc/đã gắn giao dịch — dừng.'; END IF;
    IF v_session."Status" <> 'PAYMENT_REQUESTED' OR v_session."CheckoutQuoteId" IS DISTINCT FROM 36 THEN
        RAISE EXCEPTION 'Phiên #34 không ở PAYMENT_REQUESTED theo báo giá 36 (đang %, báo giá %) — dừng.',
            v_session."Status", v_session."CheckoutQuoteId"; END IF;

    SELECT "BranchId" INTO v_branch FROM billing."CheckoutQuotes"
     WHERE "CheckoutQuoteId" = 35 AND "DiningSessionId" = 34 AND "Status" = 'PAID' FOR UPDATE;
    IF v_branch IS NULL THEN RAISE EXCEPTION 'Báo giá 35 không phải PAID của phiên #34 — dừng.'; END IF;

    PERFORM 1 FROM billing."CheckoutQuotes"
     WHERE "CheckoutQuoteId" = 36 AND "DiningSessionId" = 34 AND "Status" = 'PAYMENT_REQUESTED' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Báo giá 36 không phải PAYMENT_REQUESTED của phiên #34 — dừng.'; END IF;

    PERFORM 1 FROM billing."MobilePayments"
     WHERE "PaymentId" = 28 AND "DiningSessionId" = 34 AND "CheckoutQuoteId" = 35
       AND "Status" = 'SUCCEEDED' AND "ProviderName" = 'SEPAY' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Giao dịch 28 không phải SUCCEEDED/SEPAY của báo giá 35 — dừng.'; END IF;

    SELECT count(*) INTO v_n FROM billing."MobilePayments" WHERE "DiningSessionId" = 34 AND "Status" = 'SUCCEEDED';
    IF v_n <> 1 THEN RAISE EXCEPTION 'Phiên #34 có % giao dịch thành công (mong đợi 1) — dừng.', v_n; END IF;

    PERFORM 1 FROM billing."MobilePayments"
     WHERE "PaymentId" = 29 AND "DiningSessionId" = 34 AND "CheckoutQuoteId" = 36
       AND "Status" = 'PENDING' AND "Method" = 'CASH' FOR UPDATE;
    IF NOT FOUND THEN RAISE EXCEPTION 'Giao dịch 29 không phải CASH/PENDING của báo giá 36 — dừng.'; END IF;

    SELECT count(*) INTO v_n FROM billing."Invoices" WHERE "DiningSessionId" = 34;
    IF v_n <> 1 THEN RAISE EXCEPTION 'Phiên #34 có % hóa đơn (mong đợi 1) — dừng.', v_n; END IF;
    PERFORM 1 FROM billing."Invoices" WHERE "DiningSessionId" = 34 AND "PaymentId" = 28 AND "CheckoutQuoteId" = 35;
    IF NOT FOUND THEN RAISE EXCEPTION 'Hóa đơn của phiên #34 không thuộc giao dịch 28/báo giá 35 — dừng.'; END IF;

    SELECT count(*) INTO v_n FROM orders."Orders"
     WHERE "DiningSessionCode" = v_session."SessionCode" AND COALESCE("IsActive", true);
    IF v_n <> 1 THEN RAISE EXCEPTION 'Phiên #34 có % đơn đang hoạt động (mong đợi 1: #49) — dừng.', v_n; END IF;
    PERFORM 1 FROM orders."Orders"
     WHERE "OrderID" = 49 AND "DiningSessionCode" = v_session."SessionCode" AND COALESCE("IsActive", true);
    IF NOT FOUND THEN RAISE EXCEPTION 'Đơn #49 không phải đơn đang hoạt động của phiên #34 — dừng.'; END IF;
    IF NOT EXISTS (SELECT 1 FROM billing."CheckoutQuotes" q, jsonb_array_elements(q."ItemsJson"::jsonb) e
                   WHERE q."CheckoutQuoteId" = 35 AND (e->>'OrderId')::int = 49) THEN
        RAISE EXCEPTION 'Đơn #49 không nằm trong báo giá 35 đã trả — dừng.';
    END IF;

    SELECT "StatusID" INTO v_complete_id  FROM orders."OrderStatus"  WHERE "StatusCode" = 'COMPLETED';
    SELECT "StatusID" INTO v_available_id FROM catalog."TableStatus" WHERE "StatusCode" = 'AVAILABLE';
    IF v_complete_id IS NULL OR v_available_id IS NULL THEN
        RAISE EXCEPTION 'Thiếu mã trạng thái COMPLETED/AVAILABLE — dừng.';
    END IF;

    -- ---------- Billing ----------
    UPDATE billing."MobilePayments"
       SET "Status" = 'CANCELLED', "FailureCode" = 'incident_20261025_duplicate', "UpdatedAtUtc" = now(), "CompletedAtUtc" = now()
     WHERE "PaymentId" = 29;

    -- 36 (PAYMENT_REQUESTED) và 37–40 (OPEN). Mọi báo giá khác 35 còn mở của phiên đều bị hủy.
    UPDATE billing."CheckoutQuotes"
       SET "Status" = 'CANCELLED', "UpdatedAtUtc" = now()
     WHERE "DiningSessionId" = 34 AND "CheckoutQuoteId" <> 35 AND "Status" IN ('OPEN', 'PAYMENT_REQUESTED');
    GET DIAGNOSTICS v_n = ROW_COUNT;
    RAISE NOTICE 'Đã hủy % báo giá (mong đợi 5: 36–40).', v_n;

    UPDATE billing."Invoices"
       SET "SettlementCompletedAtUtc" = now(), "RecoveryLastAttemptAtUtc" = now()
     WHERE "DiningSessionId" = 34 AND "PaymentId" = 28 AND "SettlementCompletedAtUtc" IS NULL;

    UPDATE billing."BankTransferEvents"
       SET "Outcome" = 'AUTO_CONFIRMED', "PaymentId" = 28, "BranchId" = v_branch, "Overpaid" = false, "ProcessedAtUtc" = now()
     WHERE "Provider" = 'SEPAY' AND "ProviderTransactionId" = '41419' AND "Outcome" = 'RECEIVED';

    -- ---------- Orders (giống bước đóng phiên khi thanh toán thành công) ----------
    UPDATE orders."Orders"
       SET "StatusID" = v_complete_id, "IsActive" = false, "CompletedTime" = now()
     WHERE "DiningSessionCode" = v_session."SessionCode" AND COALESCE("IsActive", true)
       AND "OrderID" IN (SELECT (e->>'OrderId')::int FROM billing."CheckoutQuotes" q, jsonb_array_elements(q."ItemsJson"::jsonb) e
                         WHERE q."CheckoutQuoteId" = 35);

    SELECT count(*) INTO v_n FROM orders."Orders"
     WHERE "DiningSessionCode" = v_session."SessionCode" AND COALESCE("IsActive", true);
    IF v_n <> 0 THEN RAISE EXCEPTION 'Còn % đơn đang hoạt động sau khi hoàn tất — dừng.', v_n; END IF;

    UPDATE orders."ServiceRequests"
       SET "Status" = 'CANCELLED', "CancelledAtUtc" = now(), "UpdatedAtUtc" = now()
     WHERE "DiningSessionId" = 34 AND "Status" IN ('OPEN', 'ACKNOWLEDGED');

    UPDATE orders."DiningSessions"
       SET "Status" = 'CLOSED', "PaymentId" = 28, "CheckoutQuoteId" = 35, "EndedAtUtc" = now(), "UpdatedAtUtc" = now()
     WHERE "DiningSessionId" = 34;

    -- ---------- Catalog: trả bàn (chỉ khi bàn không còn phiên nào khác đang mở) ----------
    IF NOT EXISTS (SELECT 1 FROM orders."DiningSessions" WHERE "TableId" = v_session."TableId" AND "EndedAtUtc" IS NULL) THEN
        UPDATE catalog."DiningTables"
           SET "StatusID" = v_available_id, "CurrentOrderID" = NULL, "UpdatedAt" = now()
         WHERE "TableID" = v_session."TableId";
    END IF;

    RAISE NOTICE 'Đã sửa phiên #34 (bàn %).', v_session."TableId";
END $$;

COMMIT;

-- ---------- Kiểm tra sau khi chạy (chỉ đọc) ----------
SELECT "DiningSessionId", "Status", "PaymentId", "CheckoutQuoteId", "EndedAtUtc" FROM orders."DiningSessions" WHERE "DiningSessionId" = 34;
-- mong đợi: CLOSED, 28, 35, có thời gian

SELECT o."OrderID", o."IsActive", s."StatusCode" FROM orders."Orders" o JOIN orders."OrderStatus" s ON s."StatusID" = o."StatusID"
WHERE o."DiningSessionCode" = (SELECT "SessionCode" FROM orders."DiningSessions" WHERE "DiningSessionId" = 34) ORDER BY 1;
-- mong đợi: 49, false, COMPLETED

SELECT "PaymentId", "CheckoutQuoteId", "Status", "Method", "ProviderName", "FailureCode" FROM billing."MobilePayments" WHERE "DiningSessionId" = 34 ORDER BY 1;
-- mong đợi: 28/35 SUCCEEDED SEPAY; 29/36 CANCELLED incident_20261025_duplicate

SELECT "CheckoutQuoteId", "Status" FROM billing."CheckoutQuotes" WHERE "DiningSessionId" = 34 ORDER BY 1;
-- mong đợi: 35 PAID; 36, 37, 38, 39, 40 CANCELLED

SELECT "InvoiceId", "PaymentId", "TotalAmount", "SettlementCompletedAtUtc" FROM billing."Invoices" WHERE "DiningSessionId" = 34;
-- mong đợi: 1 dòng, PaymentId 28, 35000, có thời gian

SELECT "ProviderTransactionId", "Outcome", "PaymentId", "BranchId" FROM billing."BankTransferEvents";
-- mong đợi: 41419, AUTO_CONFIRMED, 28, 1

SELECT t."TableID", s."StatusCode", t."CurrentOrderID" FROM catalog."DiningTables" t JOIN catalog."TableStatus" s ON s."StatusID" = t."StatusID"
WHERE t."TableID" = (SELECT "TableId" FROM orders."DiningSessions" WHERE "DiningSessionId" = 34);
-- mong đợi: AVAILABLE, CurrentOrderID rỗng
