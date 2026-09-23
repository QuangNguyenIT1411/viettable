# ONSITE payment acceptance — 2026-09-23

**F01 ONSITE blocker: PASS. Overall release remains NOT_READY_FOR_RELEASE_CANDIDATE; deployment BLOCKED.** The original audit remains historical evidence. Database ownership F02 and external deployment/provider prerequisites are not resolved here.

## Root cause and architecture

Legacy Cashier checkout completed `Bill`, whereas Android read `MobilePayment`. Invoice/split/session finalization was private to the successful VNPAY IPN path; there was no authorized ONSITE equivalent. Legacy Bill and mobile Payment were not the same transaction resource.

Cashier now lists and confirms the actual mobile ONSITE Payment. Both successful VNPAY IPN and authorized Cashier confirmation call `PaymentFinalizationService`. A Billing transaction and quote-scoped SQL application lock serialize payment success, current split-part settlement, all-paid split/quote status and invoice issuance. Persisted VND amounts and entity relationships are validated; submitted amounts are not authoritative. Replays do not repeat settlement or create invoices.

After Billing commits, an idempotent Orders call closes the session, then a keyed Identity loyalty settlement completes. Invoice `SettlementCompletedAtUtc` is the durable retry marker; a background worker retries incomplete settlement every five seconds. An Orders outage never downgrades a successful Payment. Identity persists a settlement receipt atomically with points. Orders replay can retry Catalog table release without freeing a newer active session.

Existing invoices are marked reconciled during additive bootstrap to avoid repeating historical non-idempotent loyalty effects. This is not a historical repair/backfill guarantee. No distributed transaction, new cross-service database access, provider simulator endpoint or Android success fallback was introduced.

Legacy Bill APIs/reports remain compatible for legacy sessions. Mobile sessions must use the new mobile payment panel; legacy checkout rejects those sessions rather than recording a disconnected Bill payment. Mobile invoices are not mirrored into legacy Bill reporting; unified reporting and Bill ownership migration remain separate work.

## Changed files and purpose

Paths below are relative to the workspace; backend paths start at `SelfRestaurant-master/`.

| Files | Purpose |
|---|---|
| `src/Services/SelfRestaurant.Billing.Api/Infrastructure/Payments/PaymentFinalizationService.cs` | Shared transactional settlement, validation, durable reconciliation worker |
| Billing `Controllers/CashierMobilePaymentsController.cs`, `Controllers/MobileCheckoutController.cs` | Authorized ONSITE list/confirm; common IPN finalizer, serialized payment/retry/split creation |
| Billing `Controllers/CashierController.cs` | Prevent legacy checkout bypass for mobile sessions |
| Billing `Persistence/Entities/MobileCheckoutEntities.cs`, `Persistence/Bootstrap/BillingDbBootstrapper.cs` | Cashier actor and settlement completion marker, additive schema |
| Billing `Infrastructure/CustomersApiClient.cs` | Forward deterministic loyalty settlement key |
| Billing `Program.cs`, `SelfRestaurant.Billing.Api.csproj`, `docker-compose.yml` | Purpose-scoped service JWT authentication and reconciliation registration |
| Gateway `Controllers/StaffCashierGatewayController.cs`, `Services/BillingClient.cs` | Role/branch-bound staff relay; missing invoice/payment/split returns 404 rather than undefined JSON/502 |
| Identity `Controllers/IdentityController.cs`, `Persistence/Context/IdentityDbContext.cs`, `Persistence/Bootstrap/IdentityDbBootstrapper.cs`, `Persistence/Entities/LoyaltySettlement.cs` | Atomic idempotent loyalty receipt and conflict detection |
| Orders `Controllers/DiningSessionsController.cs` | Safe closure/table-release retry |
| Cashier web `src/components/OnsitePaymentsPanel.tsx`, `src/pages/DashboardPage.tsx`, `src/lib/api.ts` | Real pending/paid payment rows, explicit amount confirmation, guarded submits and lifecycle-safe polling |
| `tests/SelfRestaurant.Backend.ContractTests/PaymentFinalizationTests.cs` | Transition, integrity and stale-attempt unit coverage |
| `scripts/test-onsite-payment.ps1`, `scripts/test-onsite-resilience.ps1` | Real Gateway/Cashier non-split, split, concurrent and outage acceptance |
| `scripts/test-b1.ps1`, `test-b4.ps1`, `test-b5.ps1`, `test-b6.ps1`, `test-split-bill.ps1`, `test-payment-provider.ps1` | Safe retained fixtures, occupied-table selection, conflicting payment regression and mixed-method regression |
| `app/src/androidTest/java/com/restaurant/viettable/network/OnsiteCashierAcceptanceTest.java` | Coordinated real Android/Cashier acceptance and Review UI CRUD |

Android production code was not changed. Existing polling receives backend success. React lifecycle/dedup review informed the panel cleanup; browser verification used the real Cashier application, not only endpoint calls.

## Runtime evidence

| Acceptance | Evidence |
|---|---|
| Android non-split | Samsung created Payment 1038 for session 2033; real browser Cashier confirmation led to SUCCEEDED, invoice 1014 and CLOSED. Android reached Payment Success through polling. |
| Android split | Session 2051, Payments 1074/1075 of 35,000 VND each. First confirmation showed PAID part with session still PAYMENT_REQUESTED; final confirmation produced invoice 1028 for 70,000 VND and CLOSED. Actual Cashier buttons and Android screens exercised. |
| Final API non-split rerun | Payment 1079, invoice 1030, session 2054 CLOSED; duplicate/concurrent confirmation passed. |
| Final three-part rerun | Payments 1076/1077/1078, session 2053. Intermediate session stayed PAYMENT_REQUESTED with invoice 404; final invoice 1029 and CLOSED. |
| Orders outage | Payment 1080 and invoice 1031 committed while Orders was stopped; after restart worker closed session 2055. Replay preserved the same invoice. Orders always restarted in finally. |
| Mixed methods/retry | Signed internal ONLINE failure/retry plus ONSITE remaining parts passed. Old failed ONLINE attempt rejected by Cashier; stale split linkage additionally covered by unit tests. |
| Security | Customer/non-staff and direct unauthenticated Billing denied; wrong-branch Cashier returned 403; chef denied. Submitted amount 1 did not change persisted amount. |
| Integrity | No duplicate invoice-per-quote groups and no outstanding new settlement markers at final verification. |
| Review | Actual Android Payment Success → create review → history → edit/save → delete → empty state passed after split settlement. |

ONSITE acceptance ran with VNPAY disabled and never used a provider webhook to complete ONSITE. Provider regression used temporary internal LOCALTEST signed IPNs, not a real external VNPAY sandbox acceptance. Test provider configuration was restored. `.env` was not edited.

Ignored local screenshots: `artifacts/onsite-acceptance/split-intermediate-cashier.png`, `split-paid-cashier.png`, `split-pending-android.png`.

## Builds and regression

- Billing, Gateway, Orders and Identity Docker builds passed; Gateway build includes Cashier TypeScript/Vite build. Existing SignalR annotation warnings are non-blocking.
- Docker .NET SDK 8 test run: **106 passed, 0 failed, 0 skipped**. Host has no .NET SDK. Read-only repository mount and temporary artifact-root symlinks supplied source discovery. Initial temporary-output source-discovery failures were corrected, not waived.
- Android `:app:assembleDebug :app:testDebugUnitTest :app:assembleDebugAndroidTest` passed with physical-device Gateway origin. Unit suite: **31 passed**.
- `scripts/test-android-instrumentation.ps1 -Tier All`: **10 passed, 1 opt-in existing-session test skipped, 0 failed** (11 reported). Dedicated coordinated ONSITE non-split and split acceptance additionally passed; final split run includes complete Review CRUD.
- B1 retained-fixture rerun, B4, B5, B6, all-online split, mixed split and B7 passed. Earlier B4 fixture conflict was corrected/rerun without weakening business rules. B2/B3 evidence remains from the previous audit, not a new full-system rerun.
- Samsung SM-A528B remained connected using `adb reverse tcp:5100 tcp:5100`; acceptance completed without observed crash/ANR and AndroidRuntime error output was empty.

## Test data and safety exception

One execution violated the requested no-SQL-cleanup procedure: existing B1 was accidentally invoked with `-KeepTestUsers` instead of its real `-KeepTestCustomer` flag. Its old non-advanced PowerShell script ignored the unknown argument and ran legacy SQL cleanup for the disposable `b1test_*` account just created by that invocation and associated authentication tokens. No pre-existing business/user data was targeted; no database or Docker volume was reset. There is no recovery artifact for that disposable account. This was disclosed during execution.

The script now uses `CmdletBinding` to reject unknown flags and defaults to retaining the test customer. The correct retained-fixture rerun passed. Other acceptance runs retained test data or used existing business cancellation operations. Failed setup fixtures can still hold tables; they were not deleted/reset to manufacture a passing test. Test fixture selection now checks actual Orders occupancy or tries another available candidate.

## Re-run

Set `VIETTABLE_TEST_CASHIER_USERNAME` and `VIETTABLE_TEST_CASHIER_PASSWORD` only in the test process environment using an authorized existing Cashier account. From the backend root run `scripts/test-onsite-payment.ps1`, then `-Split`, then `scripts/test-onsite-resilience.ps1`. The resilience test temporarily stops Orders and restores it; run only in the local acceptance environment.

For coordinated UI acceptance use `test-onsite-payment.ps1 -PrepareAndroid`, then launch instrumentation class `com.restaurant.viettable.network.OnsiteCashierAcceptanceTest` with `onsiteAcceptance=true`, fixture username/password, and optionally `onsiteSplit=true`. Confirm only that fixture's pending rows in the real Cashier browser within the test timeout. No credentials or test IDs are production defaults. Run `scripts/test-payment-provider.ps1` for internal signed-provider regression; it retains test data and runs mixed settlement when Cashier environment credentials are supplied.

## Remaining release conditions

- F02 persists: Billing `dbo.Bills` is still a synonym to shared `RESTAURANT.dbo.Bills`; no synonym/table ownership migration was attempted.
- External VNPAY credentials/public webhook acceptance, real HTTPS deployment and release signing remain unaccepted.
- Legacy Bill aggregates do not automatically include mobile invoices; unified reporting is a separate compatibility decision.
- Existing static loyalty text on Android payment-success presentation was observed, not changed into authoritative points by this backend settlement task.
- This acceptance closes ONSITE F01; it does not certify every unrelated release checklist item.
