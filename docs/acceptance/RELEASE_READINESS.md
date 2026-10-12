# Release readiness decision — 2026-10-12

**Decision: NOT APPROVED for full handoff or stable-alias promotion.** Acceptance verdict is **CONDITIONAL / PARTIAL ACCEPTANCE**, not `FULLY ACCEPTED`. This document does not authorize a deployment.

## Gates satisfied in this round

- Local Gateway and five service health checks, stable health/readiness, protected candidate CLI health/readiness: HTTP 200.
- Candidate deployment identity `dpl_7MxW7oBh8H1DCYmPSefcYX7Hf8fE`, target `preview`, `Ready`; stable alias unchanged.
- Android debug/cloudpreview builds and 54 unit tests; CloudHost 75 tests; backend contracts 143 passed/1 skipped; all four web builds.
- Quận 1 real menu 10 categories/52 dishes and QA completed invoice/order histories: 12/12 populated reads returned HTTP 200 with expected IDs/amounts.
- WSS 17/17 subscription/branch/reconnect checks; actual Cashier browser read; Samsung existing Home/branch/session card read with no crash in bounded observation.

## Hard gates not satisfied

1. Ordinary candidate Android APK reaches Vercel Authentication 401; emulator-only local bridge is not an acceptable distributable cloud-access solution.
2. Negative **event-payload** isolation between two authenticated customers not observed. Wrong-branch subscription denial is not equivalent.
3. Isolated failure/restart recovery cannot safely run because exposed manual endpoint includes global idle cleanup.
4. Target Samsung populated Customer Order/Invoice History and full cross-role customer UAT were not freshly accepted; protected Session #20 must not be used as a destructive test fixture.
5. Variable cold response/card latency and candidate Order History tail are not within an approved, measured SLA. No matched stable/candidate UI benchmark proves improvement.

## Safe demo versus handoff

The historical isolated Q1 scenario (Session #24, Orders #42/#43, Payment/Invoice #23) demonstrated a real Customer→Chef→Cashier closure and can be presented **as historical demo evidence** with date/environment labels. It is not a substitute for the remaining gates. Current Samsung user data is not a QA fixture: latest owner-backed state is Session #20 `PAYMENT_REQUESTED`, Order/item #38 `READY`, Table 37 occupied, no payment/invoice. No mutation was made here.

## Before a future release decision

Obtain user-approved QA isolation and native Preview access design; run two-customer negative event test; perform recovery in a dedicated/scope-safe fixture; verify populated Android histories and role UIs on actual intended build; set and test latency/reliability criteria. Recheck secrets/CORS/authorization and all critical B0–B7 role flows. Only then consider candidate promotion with a separate explicit deployment decision.

No Production deployment, Vercel alias change, Git push, database reset, app data clear, business payment/order mutation, or global cleanup occurred in this acceptance round. Existing uncommitted source/scripts and `.idea` changes remain under their prior ownership.
