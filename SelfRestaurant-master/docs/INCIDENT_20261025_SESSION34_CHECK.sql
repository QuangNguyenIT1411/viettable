-- INCIDENT 25/10/2026 (phiên #34, Bàn 6, TT28). CHỈ ĐỌC: chạy trong Supabase SQL Editor, gửi lại kết quả.
-- Không có câu nào sửa dữ liệu.

-- 1. Phiên phục vụ #34 (Orders)
SELECT "DiningSessionId", "SessionCode", "Status", "CheckoutQuoteId", "PaymentId", "TableId", "CustomerId",
       "PaymentRequestedAtUtc", "EndedAtUtc", "UpdatedAtUtc"
FROM orders."DiningSessions" WHERE "DiningSessionId" = 34;

-- 2. Các đơn gọi món của phiên #34
SELECT o."OrderID", o."OrderCode", o."IsActive", s."StatusCode", o."OrderTime", o."CompletedTime"
FROM orders."Orders" o JOIN orders."OrderStatus" s ON s."StatusID" = o."StatusID"
WHERE o."DiningSessionCode" = (SELECT "SessionCode" FROM orders."DiningSessions" WHERE "DiningSessionId" = 34)
ORDER BY o."OrderID";

-- 3. Báo giá (Billing) của phiên #34
SELECT "CheckoutQuoteId", "QuoteCode", "Status", "TotalAmount", "ItemsJson", "CreatedAtUtc", "UpdatedAtUtc"
FROM billing."CheckoutQuotes" WHERE "DiningSessionId" = 34 ORDER BY "CheckoutQuoteId";

-- 4. Giao dịch (Billing) của phiên #34 — mong đợi: 28 SUCCEEDED (SEPAY), 29 PENDING (CASH)
SELECT "PaymentId", "PaymentCode", "CheckoutQuoteId", "Amount", "Channel", "Method", "Location", "Status",
       "ProviderName", "ProviderTransactionId", "ConfirmedByEmployeeId", "CreatedAtUtc", "CompletedAtUtc"
FROM billing."MobilePayments" WHERE "DiningSessionId" = 34 ORDER BY "PaymentId";

-- 5. Hóa đơn (Billing) của phiên #34 — mong đợi: 1 hóa đơn, SettlementCompletedAtUtc rỗng (chưa đóng phiên được)
SELECT "InvoiceId", "InvoiceCode", "CheckoutQuoteId", "PaymentId", "TotalAmount", "IssuedAtUtc",
       "SettlementCompletedAtUtc", "RecoveryAttemptCount"
FROM billing."Invoices" WHERE "DiningSessionId" = 34;

-- 6. Giao dịch ngân hàng SePay đã nhận
SELECT "BankTransferEventId", "ProviderTransactionId", "Amount", "Outcome", "PaymentId", "BranchId",
       "ReceivedAtUtc", "ProcessedAtUtc", "ResolvedAtUtc"
FROM billing."BankTransferEvents" ORDER BY "BankTransferEventId";

-- 7. Hóa đơn legacy (thu ngân) có dính đơn của phiên #34 không — mong đợi: 0 dòng
SELECT "BillID", "BillCode", "OrderID", "TotalAmount", "PaymentMethod", "IsActive", "BillTime"
FROM billing."Bills"
WHERE "OrderID" IN (SELECT o."OrderID" FROM orders."Orders" o
                    WHERE o."DiningSessionCode" = (SELECT "SessionCode" FROM orders."DiningSessions" WHERE "DiningSessionId" = 34));
