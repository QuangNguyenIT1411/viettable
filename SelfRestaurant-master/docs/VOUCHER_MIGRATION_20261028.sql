-- Voucher dùng 1 lần / nhiều lần + "Không dùng khuyến mãi" (28/10/2026). Migration 20261028015135_VoucherLimits (billing).
-- Run ONCE in the Supabase SQL Editor (admin account) BEFORE deploying the code that contains it: the new code refuses
-- to start while a migration is pending ("PostgreSQL schema requires deployment migrations").
-- Additive only: 2 nullable columns on billing."Vouchers" (MaxUsesPerCustomer, MaxTotalUses; NULL = no limit, so every
-- existing voucher keeps working as before), 1 column on billing."CheckoutQuotes" (PromotionDeclined, default false)
-- and 1 index. No existing data is changed, no table is created.
-- Safe to run more than once (each step is skipped once the migration is recorded in billing."__EFMigrationsHistory").
-- Part 2: grants so the Billing runtime role can CREATE/EDIT vouchers from the admin screen (until now the app only read
-- billing."Vouchers"). Part 3: read-only checks.

-- ===== Part 1: billing =====
START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261028015135_VoucherLimits') THEN
    ALTER TABLE billing."Vouchers" ADD "MaxTotalUses" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261028015135_VoucherLimits') THEN
    ALTER TABLE billing."Vouchers" ADD "MaxUsesPerCustomer" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261028015135_VoucherLimits') THEN
    ALTER TABLE billing."CheckoutQuotes" ADD "PromotionDeclined" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261028015135_VoucherLimits') THEN
    CREATE INDEX "IX_CheckoutQuotes_Voucher_Status" ON billing."CheckoutQuotes" ("VoucherId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261028015135_VoucherLimits') THEN
    INSERT INTO billing."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261028015135_VoucherLimits', '8.0.11');
    END IF;
END $EF$;
COMMIT;

-- ===== Part 2: GRANT for the runtime role (admin creates/edits vouchers; identity sequence for new rows) =====
BEGIN;
GRANT SELECT, INSERT, UPDATE ON billing."Vouchers" TO viettable_billing;
DO $GRANT$
DECLARE seq text := pg_get_serial_sequence('billing."Vouchers"', 'VoucherId');
BEGIN
    IF seq IS NOT NULL THEN
        EXECUTE format('GRANT USAGE, SELECT ON SEQUENCE %s TO viettable_billing', seq);
    END IF;
END $GRANT$;
COMMIT;

-- ===== Part 3: checks (read-only) =====
-- 1) The migration is recorded (expect 1 row).
SELECT "MigrationId" FROM billing."__EFMigrationsHistory" WHERE "MigrationId" = '20261028015135_VoucherLimits';

-- 2) New columns exist (expect 3 rows).
SELECT table_name, column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'billing' AND (
      (table_name = 'Vouchers' AND column_name IN ('MaxUsesPerCustomer', 'MaxTotalUses'))
   OR (table_name = 'CheckoutQuotes' AND column_name = 'PromotionDeclined'))
ORDER BY table_name, column_name;

-- 3) The Billing runtime role can read/create/edit vouchers and use the id sequence (expect all true).
SELECT has_table_privilege('viettable_billing', 'billing."Vouchers"', 'SELECT') AS can_select,
       has_table_privilege('viettable_billing', 'billing."Vouchers"', 'INSERT') AS can_insert,
       has_table_privilege('viettable_billing', 'billing."Vouchers"', 'UPDATE') AS can_update,
       has_sequence_privilege('viettable_billing', pg_get_serial_sequence('billing."Vouchers"', 'VoucherId'), 'USAGE') AS can_use_seq;

-- 4) ...and the new column of the existing quote table (expect true; false would mean column-level grants: tell Claude).
SELECT has_column_privilege('viettable_billing', 'billing."CheckoutQuotes"', 'PromotionDeclined', 'UPDATE') AS quote_declined;

-- 5) Vouchers now on Supabase (information only; an empty result explains the empty "Chọn Voucher" list on 28/10).
SELECT "VoucherId", "Code", "Name", "IsActive", "StartsAtUtc", "EndsAtUtc", "MaxUsesPerCustomer", "MaxTotalUses"
FROM billing."Vouchers" ORDER BY "VoucherId";
