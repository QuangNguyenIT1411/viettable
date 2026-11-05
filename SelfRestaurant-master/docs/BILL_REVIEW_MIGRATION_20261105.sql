-- Phí phục vụ + VAT theo chi nhánh, hoá đơn lưu SNAPSHOT, nâng cấp đánh giá (05/11/2026).
-- Migrations: 20261105140439_BillSnapshots (billing), 20261105140447_ReviewUpgrade (customers).
--
-- CHẠY 1 LẦN trong Supabase SQL Editor (tài khoản quản trị) TRƯỚC khi deploy code mới: code mới từ chối khởi động khi
-- còn migration chưa chạy ("PostgreSQL schema requires deployment migrations").
--
-- CHỈ THÊM, KHÔNG XOÁ/SỬA DỮ LIỆU CŨ:
--   billing."BranchChargeSettings" (bảng mới, rỗng = mọi chi nhánh 0% phí, 0% VAT = như hiện tại);
--   cột mới trên billing."CheckoutQuotes", billing."Bills", billing."Invoices" (số mặc định 0, chữ mặc định NULL);
--   customers."CustomerReviewDishRatings" (bảng mới) + cột mới trên customers."CustomerReviews"
--   (TagsJson mặc định '[]', IsAnonymous/IsHidden mặc định false); 2 index mới.
-- Hoá đơn cũ: SnapshotVersion = 0 → app/thu ngân hiện như trước (không có dòng Phí phục vụ/VAT vì trước 05/11 chưa thu),
--   tên/địa chỉ chi nhánh lấy từ chi nhánh hiện tại nếu hoá đơn không có, danh sách món vẫn từ ItemsJson đã lưu.
-- Chạy lại nhiều lần an toàn: mỗi bước bỏ qua khi migration đã có trong "__EFMigrationsHistory".
-- Phần 3: GRANT cho tài khoản runtime (bảng mới + cột mới). Phần 4: câu kiểm tra CHỈ ĐỌC (phải ra toàn true / số 0).

-- ===== Phần 1: billing =====
START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "BankTransactionRef" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "BranchAddressSnapshot" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "BranchId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "BranchNameSnapshot" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "CashierEmployeeId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "CashierNameSnapshot" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "PaidAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "PaymentMethodSnapshot" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "PointsDiscount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "PointsEarned" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "PointsUsed" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "ServiceFeeAmount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "ServiceFeePercent" numeric(5,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "SessionStartedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "SnapshotVersion" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "TableNameSnapshot" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "VatAmount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "VatPercent" numeric(5,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Invoices" ADD "VoucherDiscount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."CheckoutQuotes" ADD "BranchAddressSnapshot" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."CheckoutQuotes" ADD "BranchNameSnapshot" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."CheckoutQuotes" ADD "ServiceFeeAmount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."CheckoutQuotes" ADD "ServiceFeePercent" numeric(5,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."CheckoutQuotes" ADD "SessionStartedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."CheckoutQuotes" ADD "TableNameSnapshot" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."CheckoutQuotes" ADD "VatAmount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."CheckoutQuotes" ADD "VatPercent" numeric(5,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Bills" ADD "EmployeeNameSnapshot" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Bills" ADD "ItemsJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Bills" ADD "ServiceFeeAmount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Bills" ADD "ServiceFeePercent" numeric(5,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Bills" ADD "VatAmount" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    ALTER TABLE billing."Bills" ADD "VatPercent" numeric(5,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    CREATE TABLE billing."BranchChargeSettings" (
        "BranchId" integer NOT NULL,
        "ServiceFeePercent" numeric(5,2) NOT NULL,
        "VatPercent" numeric(5,2) NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedByEmployeeId" integer,
        "UpdatedByName" character varying(200),
        CONSTRAINT "PK_BranchChargeSettings" PRIMARY KEY ("BranchId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    CREATE INDEX "IX_Invoices_Customer_Issued" ON billing."Invoices" ("CustomerId", "IssuedAtUtc", "InvoiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots') THEN
    INSERT INTO billing."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261105140439_BillSnapshots', '8.0.11');
    END IF;
END $EF$;
COMMIT;

-- ===== Phần 2: customers =====
START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "AuthorNameSnapshot" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "BranchId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "HiddenAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "HiddenByEmployeeId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "HiddenByName" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "HiddenReason" character varying(300);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "IsAnonymous" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "IsHidden" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "RepliedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "RepliedByEmployeeId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "RepliedByName" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "ReplyText" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    ALTER TABLE customers."CustomerReviews" ADD "TagsJson" character varying(500) NOT NULL DEFAULT '[]';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    CREATE TABLE customers."CustomerReviewDishRatings" (
        "CustomerReviewDishRatingId" integer GENERATED BY DEFAULT AS IDENTITY,
        "CustomerReviewId" integer NOT NULL,
        "DishId" integer NOT NULL,
        "DishNameSnapshot" character varying(200) NOT NULL,
        "Rating" integer NOT NULL,
        CONSTRAINT "PK_CustomerReviewDishRatings" PRIMARY KEY ("CustomerReviewDishRatingId"),
        CONSTRAINT "FK_CustomerReviewDishRatings_CustomerReviews_CustomerReviewId" FOREIGN KEY ("CustomerReviewId") REFERENCES customers."CustomerReviews" ("CustomerReviewId") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    CREATE INDEX "IX_CustomerReviews_Branch_Hidden_Created" ON customers."CustomerReviews" ("BranchId", "IsHidden", "CreatedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    CREATE INDEX "IX_CustomerReviewDishRatings_DishId" ON customers."CustomerReviewDishRatings" ("DishId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    CREATE UNIQUE INDEX "UX_CustomerReviewDishRatings_Review_Dish" ON customers."CustomerReviewDishRatings" ("CustomerReviewId", "DishId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade') THEN
    INSERT INTO customers."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261105140447_ReviewUpgrade', '8.0.11');
    END IF;
END $EF$;
COMMIT;

-- ===== Phần 3: GRANT cho runtime role =====
BEGIN;
GRANT SELECT, INSERT, UPDATE ON billing."BranchChargeSettings" TO viettable_billing;
GRANT SELECT ("ServiceFeePercent", "ServiceFeeAmount", "VatPercent", "VatAmount", "BranchNameSnapshot", "BranchAddressSnapshot", "TableNameSnapshot", "SessionStartedAtUtc"), INSERT ("ServiceFeePercent", "ServiceFeeAmount", "VatPercent", "VatAmount", "BranchNameSnapshot", "BranchAddressSnapshot", "TableNameSnapshot", "SessionStartedAtUtc"), UPDATE ("ServiceFeePercent", "ServiceFeeAmount", "VatPercent", "VatAmount", "BranchNameSnapshot", "BranchAddressSnapshot", "TableNameSnapshot", "SessionStartedAtUtc") ON billing."CheckoutQuotes" TO viettable_billing;
GRANT SELECT ("ServiceFeePercent", "ServiceFeeAmount", "VatPercent", "VatAmount", "ItemsJson", "EmployeeNameSnapshot"), INSERT ("ServiceFeePercent", "ServiceFeeAmount", "VatPercent", "VatAmount", "ItemsJson", "EmployeeNameSnapshot") ON billing."Bills" TO viettable_billing;
GRANT SELECT ("SnapshotVersion", "VoucherDiscount", "PointsUsed", "PointsDiscount", "ServiceFeePercent", "ServiceFeeAmount", "VatPercent", "VatAmount", "PointsEarned", "BranchId", "BranchNameSnapshot", "BranchAddressSnapshot", "TableNameSnapshot", "SessionStartedAtUtc", "PaidAtUtc", "PaymentMethodSnapshot", "BankTransactionRef", "CashierEmployeeId", "CashierNameSnapshot"), INSERT ("SnapshotVersion", "VoucherDiscount", "PointsUsed", "PointsDiscount", "ServiceFeePercent", "ServiceFeeAmount", "VatPercent", "VatAmount", "PointsEarned", "BranchId", "BranchNameSnapshot", "BranchAddressSnapshot", "TableNameSnapshot", "SessionStartedAtUtc", "PaidAtUtc", "PaymentMethodSnapshot", "BankTransactionRef", "CashierEmployeeId", "CashierNameSnapshot"), UPDATE ("SnapshotVersion", "VoucherDiscount", "PointsUsed", "PointsDiscount", "ServiceFeePercent", "ServiceFeeAmount", "VatPercent", "VatAmount", "PointsEarned", "BranchId", "BranchNameSnapshot", "BranchAddressSnapshot", "TableNameSnapshot", "SessionStartedAtUtc", "PaidAtUtc", "PaymentMethodSnapshot", "BankTransactionRef", "CashierEmployeeId", "CashierNameSnapshot") ON billing."Invoices" TO viettable_billing;
GRANT SELECT, INSERT, UPDATE, DELETE ON customers."CustomerReviewDishRatings" TO viettable_customers;
DO $GRANT$
DECLARE seq text := pg_get_serial_sequence('customers."CustomerReviewDishRatings"', 'CustomerReviewDishRatingId');
BEGIN
    IF seq IS NOT NULL THEN
        EXECUTE format('GRANT USAGE, SELECT ON SEQUENCE %s TO viettable_customers', seq);
    END IF;
END $GRANT$;
GRANT SELECT ("TagsJson", "IsAnonymous", "AuthorNameSnapshot", "BranchId", "ReplyText", "RepliedAtUtc", "RepliedByEmployeeId", "RepliedByName", "IsHidden", "HiddenReason", "HiddenAtUtc", "HiddenByEmployeeId", "HiddenByName"), INSERT ("TagsJson", "IsAnonymous", "AuthorNameSnapshot", "BranchId", "ReplyText", "RepliedAtUtc", "RepliedByEmployeeId", "RepliedByName", "IsHidden", "HiddenReason", "HiddenAtUtc", "HiddenByEmployeeId", "HiddenByName"), UPDATE ("TagsJson", "IsAnonymous", "AuthorNameSnapshot", "BranchId", "ReplyText", "RepliedAtUtc", "RepliedByEmployeeId", "RepliedByName", "IsHidden", "HiddenReason", "HiddenAtUtc", "HiddenByEmployeeId", "HiddenByName") ON customers."CustomerReviews" TO viettable_customers;
COMMIT;

-- ===== Phần 4: kiểm tra (chỉ đọc) =====
-- 4a. Hai migration đã ghi nhận (phải ra 2 dòng).
SELECT 'billing' AS schema, "MigrationId" FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140439_BillSnapshots'
UNION ALL
SELECT 'customers', "MigrationId" FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261105140447_ReviewUpgrade';
-- 4b. Quyền runtime (phải toàn true).
SELECT has_table_privilege('viettable_billing', 'billing."BranchChargeSettings"', 'INSERT') AS charges_insert,
       has_table_privilege('viettable_billing', 'billing."BranchChargeSettings"', 'UPDATE') AS charges_update,
       has_column_privilege('viettable_billing', 'billing."CheckoutQuotes"', 'ServiceFeeAmount', 'UPDATE') AS quote_fee,
       has_column_privilege('viettable_billing', 'billing."Bills"', 'ItemsJson', 'INSERT') AS bill_items,
       has_column_privilege('viettable_billing', 'billing."Invoices"', 'SnapshotVersion', 'INSERT') AS invoice_snapshot,
       has_column_privilege('viettable_billing', 'billing."Invoices"', 'VatAmount', 'SELECT') AS invoice_vat_read,
       has_table_privilege('viettable_customers', 'customers."CustomerReviewDishRatings"', 'INSERT') AS dish_ratings_insert,
       has_column_privilege('viettable_customers', 'customers."CustomerReviews"', 'ReplyText', 'UPDATE') AS review_reply,
       has_column_privilege('viettable_customers', 'customers."CustomerReviews"', 'IsHidden', 'UPDATE') AS review_hide;
-- 4c. Không đổi tổng tiền đang chạy: chưa chi nhánh nào bật phí/VAT (phải ra 0 cho tới khi bạn tự bật trong Admin).
SELECT count(*) AS branches_with_fee_or_vat FROM billing."BranchChargeSettings" WHERE "ServiceFeePercent" > 0 OR "VatPercent" > 0;
-- 4d. Hoá đơn cũ giữ nguyên: mọi hoá đơn trước khi deploy có SnapshotVersion = 0, phí/VAT = 0.
SELECT count(*) AS invoices, count(*) FILTER (WHERE "ServiceFeeAmount" <> 0 OR "VatAmount" <> 0) AS with_charges
FROM billing."Invoices";
