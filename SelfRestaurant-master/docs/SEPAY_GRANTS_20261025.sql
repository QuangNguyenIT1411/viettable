-- Run in Supabase SQL Editor AFTER SEPAY_MIGRATION_20261019.sql (25/10/2026).
-- Why: the Billing runtime role (viettable_billing) only received privileges on tables that existed when it was
-- created; billing."BankTransferEvents" was created later by the admin, so the webhook failed with
-- "42501: permission denied for table BankTransferEvents". Same privileges as the original role setup, for the
-- new table and its identity sequence only. Safe to run more than once.
BEGIN;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing."BankTransferEvents" TO viettable_billing;
GRANT USAGE, SELECT ON SEQUENCE billing."BankTransferEvents_BankTransferEventId_seq" TO viettable_billing;
COMMIT;

-- Check (read-only): all four columns must be true.
SELECT has_table_privilege('viettable_billing', 'billing."BankTransferEvents"', 'SELECT') AS can_select,
       has_table_privilege('viettable_billing', 'billing."BankTransferEvents"', 'INSERT') AS can_insert,
       has_table_privilege('viettable_billing', 'billing."BankTransferEvents"', 'UPDATE') AS can_update,
       has_sequence_privilege('viettable_billing', 'billing."BankTransferEvents_BankTransferEventId_seq"', 'USAGE') AS can_use_seq;
