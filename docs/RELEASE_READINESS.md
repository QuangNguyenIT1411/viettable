# VietTable release readiness

Evidence date: 2026-09-23. This document distinguishes proven behavior from external or manual acceptance work.

Final audit supersedes earlier broad PASS claims: **Code baseline NOT_READY_FOR_RELEASE_CANDIDATE; Deployment BLOCKED.** See [FINAL_RELEASE_AUDIT.md](FINAL_RELEASE_AUDIT.md) for runtime evidence, fixture limitations and risk levels. Mobile ONSITE completion and active shared-database dependencies are unresolved.

| Area | Status | Evidence / remaining condition |
|---|---|---|
| Backend health | PASS | Docker services were running and Gateway `/health` returned HTTP 200; no backend or volume was changed. |
| Android debug build | PASS | `:app:assembleDebug` completed successfully. |
| Unit tests | PASS | `:app:testDebugUnitTest` completed successfully, including display formatting regression tests. |
| Android lint | PASS | `:app:lintDebug` completed with no errors. Remaining warnings are classified in `ANDROID_UI_AUDIT.md`. |
| Instrumentation tests | PASS | Final audit ran Tier `All -RunExistingSessionSecurity` on Samsung SM-A528B: 11 reported/executed/pass, 0 skipped/fail. Unit suite: 31 pass, 0 skipped/fail. |
| Token security | PASS | Device tests covered Keystore migration, rotation, corruption handling, logout, concurrent 401 refresh, and relogin. |
| HTTPS enforcement | PASS | Release Gradle validation requires a non-local HTTPS origin; merged release manifest has `usesCleartextTraffic=false`. |
| Login/password | PASS | Real Gateway login success/failure/logout passed instrumentation; forms now expose field-local errors and guarded submits. |
| Google Sign-In | BLOCKED_EXTERNAL | Implementation/config guard exists, but Google Cloud consent/client configuration and a real external account E2E are not accepted here. |
| Branch/menu | PASS | Authenticated cold launch loaded canonical branches/menu in device instrumentation; fake distance/rating/open/map data is no longer presented. |
| Reservation | MANUAL_ACCEPTANCE | Backend integration and regression suite exist; a complete availability → table → create/change/cancel lifecycle still needs a stable acceptance fixture. |
| QR manual | PASS | Permission-denied + invalid/valid manual Gateway validation passed device instrumentation. |
| QR camera | MANUAL_ACCEPTANCE | CameraX/ML Kit build and lifecycle checks pass; a physical printed QR scan remains a manual acceptance item. |
| Dining session | MANUAL_ACCEPTANCE | B4 check-in/replay/ownership passed; complete script stopped at an occupied walk-in fixture. No full B4 acceptance claim. |
| Order | MANUAL_ACCEPTANCE | B5 stopped at an occupied table fixture; B7 kitchen-ready notification projection passed. Full multi-round/tracking regression must be rerun with safe fixtures. |
| SignalR | PASS | Instrumentation verified encrypted-session profile load and connection logic where applicable; no core SignalR behavior was rewritten. |
| Service request | MANUAL_ACCEPTANCE | Source integration exists, but this audit's B5 did not reach create/list/cancel assertions because its table fixture failed. |
| Checkout | PASS | Existing B6 quote and server-owned totals remain unchanged. |
| Split bill | MANUAL_ACCEPTANCE | Creation/exact sum/replay/duplicate/reload assertions passed. Part settlement/retry/all-paid closure blocked by unavailable online provider and missing ONSITE bridge. |
| Payment onsite | FAILED | Create/read returns PENDING; mobile payment completion only exists through VNPAY IPN. Legacy cashier checkout does not settle MobilePayments/Invoices. Release blocker F01 in final audit. |
| VNPAY | BLOCKED_EXTERNAL | Real sandbox credentials, a publicly reachable IPN/webhook URL, and provider E2E acceptance are still required. |
| Invoice | MANUAL_ACCEPTANCE | Issuance source exists, but this audit could not complete a new paid fixture. Do not infer full invoice E2E from source alone. |
| FAQ | PASS | Device instrumentation opened/reloaded backend FAQ items without duplication. |
| Review | MANUAL_ACCEPTANCE | CRUD/eligibility/validation/ownership source exists. B7 stopped before paid-invoice CRUD at provider unavailable; no fresh CRUD/UI E2E PASS. |
| Support | PASS | Real support-request API remains; unconfigured hotline and live chat are hidden. |
| Accessibility | MANUAL_ACCEPTANCE | Critical icon actions and touch targets were fixed and lint passed; full TalkBack traversal/contrast certification across all screens remains manual. |
| Release build | PASS | `:app:assembleRelease :app:lintRelease` passed using a non-committed HTTPS validation origin. APK is unsigned, as expected without release signing material. |
| Release signing | BLOCKED_EXTERNAL | Production keystore/signing policy has not been supplied. |
| Production API | BLOCKED_EXTERNAL | A real HTTPS Gateway domain and trusted certificate are not yet supplied. |
| Database ownership | FAILED | Runtime Billing dbo.Bills is a synonym to shared RESTAURANT.dbo.Bills and is actively used by cashier code. Other legacy synonyms remain; service isolation is incomplete. |
| Backend full regression | MANUAL_ACCEPTANCE | B1–B3 passed. B0 xUnit could not run without a .NET SDK; B4/B5 fixture conflicts and B6/split/B7 provider dependencies prevent full green regression. |
| Git baseline | PASS | Baseline `20a32b9` remains in history; this task is committed separately and no remote is configured/pushed. |

## Artifact sizes

- Debug APK: approximately 37.86 MiB (39,700,132 bytes).
- Unsigned release APK: approximately 33.18 MiB.

## Future hardening

- Define production signing/versioning ownership and release distribution procedure.
- Decide whether/when to enable R8; minification remains off by design and was not enabled opportunistically.
- Continue resource/string extraction and remove only legacy resources proven unreachable.
- Add stable backend fixtures for full reservation-to-payment lifecycle automation.
