# VietTable release readiness

Evidence date: 2026-09-23. This document distinguishes proven behavior from external or manual acceptance work.

| Area | Status | Evidence / remaining condition |
|---|---|---|
| Backend health | PASS | Docker services were running and Gateway `/health` returned HTTP 200; no backend or volume was changed. |
| Android debug build | PASS | `:app:assembleDebug` completed successfully. |
| Unit tests | PASS | `:app:testDebugUnitTest` completed successfully, including display formatting regression tests. |
| Android lint | PASS | `:app:lintDebug` completed with no errors. Remaining warnings are classified in `ANDROID_UI_AUDIT.md`. |
| Instrumentation tests | PASS | Tier `All` completed on Samsung SM-A528B: 11 reported tests, with the explicitly opt-in existing-session test skipped by assumption. |
| Token security | PASS | Device tests covered Keystore migration, rotation, corruption handling, logout, concurrent 401 refresh, and relogin. |
| HTTPS enforcement | PASS | Release Gradle validation requires a non-local HTTPS origin; merged release manifest has `usesCleartextTraffic=false`. |
| Login/password | PASS | Real Gateway login success/failure/logout passed instrumentation; forms now expose field-local errors and guarded submits. |
| Google Sign-In | BLOCKED_EXTERNAL | Implementation/config guard exists, but Google Cloud consent/client configuration and a real external account E2E are not accepted here. |
| Branch/menu | PASS | Authenticated cold launch loaded canonical branches/menu in device instrumentation; fake distance/rating/open/map data is no longer presented. |
| Reservation | MANUAL_ACCEPTANCE | Backend integration and regression suite exist; a complete availability → table → create/change/cancel lifecycle still needs a stable acceptance fixture. |
| QR manual | PASS | Permission-denied + invalid/valid manual Gateway validation passed device instrumentation. |
| QR camera | MANUAL_ACCEPTANCE | CameraX/ML Kit build and lifecycle checks pass; a physical printed QR scan remains a manual acceptance item. |
| Dining session | PASS | Existing-session launch showed the canonical active session without scenario fallback; empty/error states remain server-backed. |
| Order | PASS | Canonical order state and SignalR/poll fallback code remain; current/tracking screens now have explicit empty/error/retry states. |
| SignalR | PASS | Instrumentation verified encrypted-session profile load and connection logic where applicable; no core SignalR behavior was rewritten. |
| Service request | PASS | B7/B5 integration remains; list now has no-session/empty/error/retry and no fake staff acknowledgement. |
| Checkout | PASS | Existing B6 quote and server-owned totals remain unchanged. |
| Split bill | PASS | Real amount-split API remains; UI only advertises equal amount parts and leaves item split disabled. |
| Payment onsite | PASS | Existing onsite payment request/polling flow remains. |
| VNPAY | BLOCKED_EXTERNAL | Real sandbox credentials, a publicly reachable IPN/webhook URL, and provider E2E acceptance are still required. |
| Invoice | PASS | Existing server-issued invoice flow remains; user-visible timestamp formatting was corrected. |
| FAQ | PASS | Device instrumentation opened/reloaded backend FAQ items without duplication. |
| Review | PASS | Existing B7 create/edit/delete integration remains; rating accessibility and submit feedback were improved. |
| Support | PASS | Real support-request API remains; unconfigured hotline and live chat are hidden. |
| Accessibility | MANUAL_ACCEPTANCE | Critical icon actions and touch targets were fixed and lint passed; full TalkBack traversal/contrast certification across all screens remains manual. |
| Release build | PASS | `:app:assembleRelease :app:lintRelease` passed using a non-committed HTTPS validation origin. APK is unsigned, as expected without release signing material. |
| Release signing | BLOCKED_EXTERNAL | Production keystore/signing policy has not been supplied. |
| Production API | BLOCKED_EXTERNAL | A real HTTPS Gateway domain and trusted certificate are not yet supplied. |
| Git baseline | PASS | Baseline `20a32b9` remains in history; this task is committed separately and no remote is configured/pushed. |

## Artifact sizes

- Debug APK: approximately 36.21 MiB.
- Unsigned release APK: approximately 33.18 MiB.

## Future hardening

- Define production signing/versioning ownership and release distribution procedure.
- Decide whether/when to enable R8; minification remains off by design and was not enabled opportunistically.
- Continue resource/string extraction and remove only legacy resources proven unreachable.
- Add stable backend fixtures for full reservation-to-payment lifecycle automation.
