# Billing ownership migration

Status: Billing-scoped cutover and runtime acceptance PASS on 2026-09-23. Baseline b64abd1; initial working tree clean. Other services' ownership is not certified.

## Mapping and decision

Strategy B: Billing owns legacy Bills in `RESTAURANT_BILLING.billing.Bills`. Cashier checkout, checkout replay, bill history and cashier/branch reports still use Bills; Admin employee cashier history consumes the Billing API through Gateway. Keeping IDs/DTOs avoids breaking those consumers. Orders audit stores a logical BillId, not a cross-database FK. Shared database entities retain historical navigation models, not Billing runtime access.

Billing also owns CheckoutQuotes, MobilePayments, SplitBills/SplitBillParts, Invoices, Vouchers, checkout commands and its local snapshots/outbox. Catalog, Orders, Customers and Identity remain owners of their respective operational data; Billing obtains cross-domain context through service APIs/events and stores necessary identifiers/snapshots without a physical cross-database FK. The SQL login is still shared/privileged, so the boundary is currently enforced by runtime dependency design and checks, not least-privilege credentials.

Mobile Payment/Quote/Split/Invoice remain their existing owned entities. Invoice has PaymentId/QuoteId/SessionId, not BillId. Legacy Bills represent legacy checkout only; they are not a mirror or competing success record for mobile transactions. Existing mobile-session legacy-checkout guard stays intact. Cross-domain metadata comes from Orders/Identity APIs and stored bill snapshots. Reporting remains legacy-only as before this migration.

Before: `BillingDbContext.Bills → dbo.Bills synonym → RESTAURANT.dbo.Bills`. Runtime Billing also had 29 other shell synonyms. Only Bills is mapped by Billing's context; bootstrap actively followed its target for ALTER/FK/index work. Source has 22 columns, decimal(18,2) amounts, datetime timestamps, nullable snapshots and IDs, identity PK and four supporting indexes. Source FK to Employees must not be recreated across ownership boundaries.

Initial runtime source: **0 Bills rows**, SUM(TotalAmount)=NULL. Thus no real historical Bill record exists to spot-check on this installation. Nonempty migration preservation must be verified in an isolated test database. Mobile history exists and must be checked separately. SQL dependency metadata showed only local MobilePayments dependencies, no object referencing shell synonyms.

## Procedure and rollback — read before execution

1. Retain source table untouched; take SQL database backups before operational cutover. Stop Billing and any external legacy/MVC Bill writers for the migration window. Do not reopen old writers afterward.
2. Run `sql/billing-owned-tables.sql` against Billing. This additive schema does not remove aliases or copy data. New setup uses it without a shared-database dependency.
3. Run `sql/migrate-billing-bills.sql`. Exclusive source/table and migration locks, explicit identity copy, all source-column EXCEPT comparison, count/sum checks and migration ledger commit together. Unknown source columns/nonempty unrecorded target fail closed. Nullable absent snapshot columns stay NULL. Re-run returns the ledger, never overwrites post-cutover writes.
4. Deploy new mapping and bootstrap; verify Cashier history/report and payment regression before retiring aliases.
5. Run `sql/retire-billing-synonyms.sql` only after runtime verification. It refuses SQL dependencies. Then rerun architecture/payment regression with aliases absent. No source table/row is removed.

Before new writes, rollback can stop Billing and redeploy b64abd1 with original dbo.Bills synonym (source is intact); leave added target tables for investigation. After local Bills writes, **do not simply roll back code**: stop writers, back up both DBs, reconcile/export new local records and identity high-water mark into a reviewed reverse migration first. Restoring stale source as authoritative would lose post-cutover changes. Recreating unused shell synonyms is unnecessary. No automatic destructive rollback is provided.

Current SA/shared SQL credential remains privilege debt; removing runtime cross-DB dependencies does not itself enforce least privilege. Other service synonyms remain out of scope, so global isolation cannot be declared PASS merely from Billing success.

## Executed cutover and evidence (2026-09-23)

- Backed up and `RESTORE VERIFYONLY`-checked both source and Billing databases inside the SQL Server container. No Docker volume, source table, source row or `.env` was deleted. The source had 0 Bills, so preservation of nonempty history was verified separately in isolated databases using two representative Bills (IDs 17 and 29, decimal/date/NULL/Unicode values), migration replay, identity continuation and fresh-schema replay. `scripts/test-billing-migration.ps1` passed.
- Migrated the live empty source with ledger entry `billing-bills-v1`, deployed Billing with `billing.Bills` mapping, then retired all 30 Billing shell synonyms. `scripts/test-billing-ownership.ps1` passed with 0 synonyms and 0 SQL cross-database expression dependencies. Billing bootstrap does not inspect or mutate the shared source. Other service aliases remain outside this scoped result.
- Authenticated legacy cashier checkout via Gateway generated `BILL-20260923-5108` for test Order 3030 and 70,000 VND. It persisted as BillID 1 in `RESTAURANT_BILLING.billing.Bills`; `RESTAURANT.dbo.Bills` remained at 0. Legacy bill history/report reads returned their expected shape; they contain only legacy Bills, not mobile Invoices.
- After synonym retirement, non-split and three-part ONSITE settlement, signed internal provider split/mixed retry, B4, B5, B6 and B7 runtime checks passed. Cashier web login/ONSITE confirmation changed the test payment from pending to paid. Android Tier All rerun passed 10 tests with 1 opt-in skip. Docker .NET suite passed 108/108. Cashier web built via Node 22 Docker image. External VNPAY acceptance was not performed.
- The explicit test-data reset script now targets only local `billing.Bills` in Billing and does not delete historical shared Bills from the Orders reset step. This script was **not run** during migration.

The first live cutover had no historical Bills to spot-check; the isolated nonempty migration test is the preservation evidence, not proof of a production history copy. SQL backups and isolated test databases are retained. The original shared table remains for recovery and is no longer a Billing runtime dependency. New local Bill writes mean rollback now requires reconciliation; do not redeploy the old synonym mapping alone.
