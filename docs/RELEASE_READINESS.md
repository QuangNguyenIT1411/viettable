# VietTable release readiness

Evidence date: 2026-09-23. This document distinguishes proven behavior from external or manual acceptance work.

**Code baseline NOT_READY_FOR_RELEASE_CANDIDATE; Deployment BLOCKED.** [ONSITE acceptance](ONSITE_PAYMENT_ACCEPTANCE.md) closes mobile ONSITE F01. [Billing ownership migration](BILLING_OWNERSHIP_MIGRATION.md), [Orders ownership cleanup](ORDERS_OWNERSHIP_CLEANUP.md) and [Customers ownership cleanup](CUSTOMERS_OWNERSHIP_CLEANUP.md) close scoped internal database isolation. [Production HTTPS deployment](PRODUCTION_HTTPS_DEPLOYMENT.md) is prepared but lacks a real server/domain/certificate and public E2E acceptance. The [full database ownership audit](DATABASE_OWNERSHIP_AUDIT.md) retains its historical inventory and later PASS addendum. [Original audit](FINAL_RELEASE_AUDIT.md) is historical evidence.

| Area | Status | Evidence / remaining condition |
|---|---|---|
| Backend health | PASS | Affected services rebuilt and runtime regressions passed; no volume reset. |
| Android debug build | PASS | `:app:assembleDebug` completed successfully. |
| Unit tests | PASS | `:app:testDebugUnitTest` completed successfully, including display formatting regression tests. |
| Android lint | PASS | `:app:lintDebug` completed with no errors. Remaining warnings are classified in `ANDROID_UI_AUDIT.md`. |
| Instrumentation tests | PASS | Post-Customers Tier All on Samsung: 10 pass, 1 existing-session opt-in skip, 0 fail (`OK (11 tests)`). Earlier dedicated non-split and split UI acceptance also passed. |
| Token security | PASS | Device tests covered Keystore migration, rotation, corruption handling, logout, concurrent 401 refresh, and relogin. |
| HTTPS enforcement | PASS | Release Gradle validation requires a non-local HTTPS origin; merged release manifest has `usesCleartextTraffic=false`. |
| Reverse proxy configuration | PREPARED | Separate production Compose override and Caddyfile validate; only 80/443 publish in merged config. Not deployed to a production host. |
| DNS / trusted production TLS | BLOCKED_EXTERNAL | No real server, public DNS name or trusted certificate has been supplied or accepted. |
| Public Android/SignalR HTTPS E2E | BLOCKED_EXTERNAL | No public endpoint to test WSS or Samsung without `adb reverse`; local tests do not count as production acceptance. |
| Login/password | PASS | Real Gateway login success/failure/logout passed instrumentation; forms now expose field-local errors and guarded submits. |
| Google Sign-In | BLOCKED_EXTERNAL | Implementation/config guard exists, but Google Cloud consent/client configuration and a real external account E2E are not accepted here. |
| Branch/menu | PASS | Authenticated cold launch loaded canonical branches/menu in device instrumentation; fake distance/rating/open/map data is no longer presented. |
| Reservation | MANUAL_ACCEPTANCE | Backend integration and regression suite exist; a complete availability → table → create/change/cancel lifecycle still needs a stable acceptance fixture. |
| QR manual | PASS | Permission-denied + invalid/valid manual Gateway validation passed device instrumentation. |
| QR camera | MANUAL_ACCEPTANCE | CameraX/ML Kit build and lifecycle checks pass; a physical printed QR scan remains a manual acceptance item. |
| Dining session | PASS | Full B4 rerun and ONSITE closure/outage recovery passed with real occupied-table-aware fixtures. |
| Order | PASS | Full B5 multi-round/tracking regression rerun passed. |
| SignalR | PASS | Instrumentation verified encrypted-session profile load and connection logic where applicable; no core SignalR behavior was rewritten. |
| Service request | PASS | B5 create/list/cancel regression passed. |
| Checkout | PASS | Existing B6 quote and server-owned totals remain unchanged. |
| Split bill | PASS | Three-part ONSITE API, two-part Samsung/Cashier UI and mixed signed-internal ONLINE/ONSITE retry passed. Intermediate parts do not close session; final part does. |
| Payment onsite | PASS | Real authorized Cashier confirmation → mobile SUCCEEDED → invoice → CLOSED; Samsung polling, duplicate/concurrent confirmation and Orders outage recovery proven with VNPAY disabled. F01 closed. |
| VNPAY | BLOCKED_EXTERNAL | Real sandbox credentials, a publicly reachable IPN/webhook URL, and provider E2E acceptance are still required. |
| Invoice | PASS | Canonical invoice issued for non-split and final split; duplicate/reconciliation replay preserved one invoice. |
| FAQ | PASS | Device instrumentation opened/reloaded backend FAQ items without duplication. |
| Review | PASS | B7 passed and Samsung performed create → history → update → delete after real ONSITE split settlement. |
| Support | PASS | Real support-request API remains; unconfigured hotline and live chat are hidden. |
| Accessibility | MANUAL_ACCEPTANCE | Critical icon actions and touch targets were fixed and lint passed; full TalkBack traversal/contrast certification across all screens remains manual. |
| Release build | PASS | `:app:assembleRelease :app:lintRelease` passed using a non-committed HTTPS validation origin. APK is unsigned, as expected without release signing material. |
| Release signing | BLOCKED_EXTERNAL | Production keystore/signing policy has not been supplied. |
| Production API | BLOCKED_EXTERNAL | A real HTTPS Gateway domain and trusted certificate are not yet supplied. |
| Database ownership | PASS | All five service DBs now have 0 synonyms and 0 cross-DB SQL objects; Gateway has no DB. Global guard exits 0. Least-privilege SQL credentials and legacy DB retirement are separate work. |
| Backend regression | PASS_SCOPED | Docker .NET suite 108/108 from earlier audit; after Customers cutover, B3/B4/B6/split/B7, ONSITE paid invoice and internal signed provider passed. B5 remains Orders-cutover evidence. External provider acceptance remains blocked. |
| Git baseline | PASS | Baseline `20a32b9` remains in history; this task is committed separately and no remote is configured/pushed. |

## Database ownership matrix (post-cutover, 2026-09-23)

| Service | Status | Evidence |
|---|---|---|
| Catalog | PASS | Own DB, 0 synonyms, 0 SQL cross-DB dependencies. |
| Orders | PASS | 26 unused aliases retired, 0 SQL cross-DB dependencies; cold restart, fresh `db-init` rerun and B4/B5/payment regressions passed. |
| Customers | PASS | 30 unused aliases retired; 0 SQL cross-DB dependencies, cold restart, two `db-init` reruns and B3/B4/B7/paid-invoice regression passed. |
| Identity | PASS | Own DB, 0 synonyms, 0 SQL cross-DB dependencies. |
| Billing | PASS | Local `billing.Bills`, 0 synonyms, 0 SQL cross-DB dependencies. |
| Gateway | PASS | HTTP orchestration; no business DB context/connection. |

## Artifact sizes

- Debug APK: approximately 37.86 MiB (39,700,132 bytes).
- Unsigned release APK: approximately 33.18 MiB.

## Future hardening

- Define production signing/versioning ownership and release distribution procedure.
- Decide whether/when to enable R8; minification remains off by design and was not enabled opportunistically.
- Continue resource/string extraction and remove only legacy resources proven unreachable.
- Add stable backend fixtures for full reservation-to-payment lifecycle automation.
