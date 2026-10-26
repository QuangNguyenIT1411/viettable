-- Reservation item 2 (26/10/2026): cashier confirms / rejects bookings. Migration 20261026041640_ReservationDecisions.
-- Run ONCE in the Supabase SQL Editor (admin account) BEFORE deploying the code that contains it: the new code refuses
-- to start while a migration is pending ("PostgreSQL schema requires deployment migrations").
-- Additive only: three NULL columns on customers."Reservations"; no data is changed. Safe to run more than once
-- (each step is skipped once the migration is recorded in customers."__EFMigrationsHistory").
-- No GRANT needed: column privileges follow the table, which viettable_customers can already read and update.
-- After running it, run the read-only checks at the end of this file.

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261026041640_ReservationDecisions') THEN
    ALTER TABLE customers."Reservations" ADD "StatusChangedAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261026041640_ReservationDecisions') THEN
    ALTER TABLE customers."Reservations" ADD "StatusChangedByEmployeeId" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261026041640_ReservationDecisions') THEN
    ALTER TABLE customers."Reservations" ADD "StatusReason" character varying(300);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM customers."__EFMigrationsHistory" WHERE "MigrationId" = '20261026041640_ReservationDecisions') THEN
    INSERT INTO customers."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261026041640_ReservationDecisions', '8.0.11');
    END IF;
END $EF$;
COMMIT;

-- ---- Checks (read-only) ----
-- 1) The three columns exist (expect 3 rows).
SELECT column_name, data_type, character_maximum_length, is_nullable
FROM information_schema.columns
WHERE table_schema = 'customers' AND table_name = 'Reservations'
  AND column_name IN ('StatusChangedAtUtc', 'StatusChangedByEmployeeId', 'StatusReason')
ORDER BY column_name;

-- 2) The migration is recorded (expect 1 row).
SELECT "MigrationId", "ProductVersion" FROM customers."__EFMigrationsHistory"
WHERE "MigrationId" = '20261026041640_ReservationDecisions';

-- 3) The runtime role can still read and write the table (expect all three true).
SELECT has_table_privilege('viettable_customers', 'customers."Reservations"', 'SELECT') AS can_select,
       has_table_privilege('viettable_customers', 'customers."Reservations"', 'INSERT') AS can_insert,
       has_table_privilege('viettable_customers', 'customers."Reservations"', 'UPDATE') AS can_update;

-- 4) ...and the new columns themselves (expect all three true; false would mean column-level grants: tell Claude).
SELECT has_column_privilege('viettable_customers', 'customers."Reservations"', 'StatusChangedAtUtc', 'UPDATE') AS changed_at,
       has_column_privilege('viettable_customers', 'customers."Reservations"', 'StatusChangedByEmployeeId', 'UPDATE') AS changed_by,
       has_column_privilege('viettable_customers', 'customers."Reservations"', 'StatusReason', 'UPDATE') AS reason;
