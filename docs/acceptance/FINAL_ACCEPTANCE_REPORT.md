# VietTable — full system acceptance (2026-10-12)

**Verdict: CONDITIONAL / PARTIAL ACCEPTANCE.** This is a read-only acceptance round, not a production-source change or release authorization. Evidence for each case is indexed in [FULL_ACCEPTANCE_MATRIX.csv](FULL_ACCEPTANCE_MATRIX.csv) and [the new run ledger](evidence/RUN_2026-10-12.md). `PASS HISTORICAL` is deliberately distinct from a new `PASS`.

## 1. Executive summary

VietTable has a substantial implemented B0–B7 customer journey and historically completed a real isolated Quận 1 Customer → Chef → Cashier → Invoice flow. In this round, all four web builds, two Android variants, 54 Android unit tests, 75 CloudHost tests, and 143 backend contract tests passed (one contract skipped). Local services and stable/candidate health/readiness answered 200. The actual Samsung app restored branch Bình Thạnh and its Bàn 2 card; actual candidate Cashier browser displayed Quận 1 tables and the previously paid QA payment. Populated Q1 invoice/order histories remained consistent in 12/12 bounded reads. WSS subscription authorization/reconnect passed 17/17.

Those facts **do not** complete handoff: the ordinary Android APK is still blocked by Vercel Authentication on the candidate, no negative cross-customer **event-payload** delivery experiment exists, the manual recovery endpoint cannot be safely scoped to a QA fixture, and current physical-device populated histories/full B0–B7 write paths were not rerun. Samsung active-card observation reached 29.75 s on one of three cold launches. Stable alias was not changed.

## 2. Architecture and environment inventory

| Layer | Actual project / responsibility | Acceptance boundary |
|---|---|
| Customer Android | `app`, package `com.restaurant.viettable`; `MobileRepository` → `ApiClient` → Gateway | Samsung uses installed stable-alias APK; no install/data clear this round |
| Customer Web | `SelfRestaurant-master/src/Frontend/selfrestaurant-customer-web` | Build passed; live browser business flow not rerun |
| Staff Web | `selfrestaurant-chef-web`, `selfrestaurant-cashier-web`, `selfrestaurant-admin-web` | All built; only existing Cashier cookie used for a new browser read |
| Gateway | `SelfRestaurant-master/src/Gateway`, mobile prefix `/api/mobile/v1`, staff routes/hubs | No mobile direct call to 5101–5105 |
| Domain services | Identity, Catalog, Orders, Customers, Billing | Historical ownership contracts; no cross-domain DB write this round |
| Infrastructure | Local SQL Server/RabbitMQ containers; staging Supabase PostgreSQL/Storage and Vercel CloudHost | Local health != cloud DB or broker transaction proof |
| Realtime | SignalR mobile/staff hubs with cross-instance backplane | New subscribe/reconnect checks, old event-delivery fixture |
| Cloud targets | Stable `https://viettable-staging-hienkhanhvu981.vercel.app`; candidate `https://viettable-k8j32aegw-hienkhanhvu981-7145.vercel.app` | Candidate `dpl_7MxW7oBh8H1DCYmPSefcYX7Hf8fE`, Ready/Preview; stable unchanged |

The source/contract inventory was checked against `AGENTS.md`, mobile API contract/traceability, B0–B7 status/checklists, repository modules, and existing Q1/performance audits. A declared feature or historical test is not counted as fresh runtime proof.

## 3. Features inventory and functional acceptance

| Area | Implemented surface in source | Current acceptance |
|---|---|---|
| B1–B2 | Register/login/refresh/reset/logout; profile, loyalty, preferences | QA login/read PASS now; full UI lifecycle PASS HISTORICAL |
| B3–B4 | Branch/menu, reservation/pre-order, QR camera/manual validation, check-in/walk-in/session | Real Q1 menu read PASS now; write paths historical |
| B5 | Multiple order rounds, kitchen item status, service requests, history | Q1 rounds #42/#43 and request #24 historical; new owned history reads PASS |
| B6 | Quote, ONSITE payment, split bill, invoice and closure | QA payment/invoice #23 historical; new invoice read PASS; split cloud E2E not rerun |
| B7 | Favorites, notifications, reviews, FAQ/support/settings | Source and historical tests only this round |
| Admin | Dashboard, categories/dishes, tables/employees and scoped role surfaces | Q1 browser historical; new current Admin browser CRUD not run |

For exact case-by-case status, see matrix. No new business write was performed to force a PASS.

## 4. Backend and data acceptance

Local Gateway health/ready and each of five services' health returned 200. Stable and protected-candidate health/ready also returned 200 using appropriate access. CloudHost 75/75 and backend contract 143/144 (1 skipped) passed. Candidate and stable returned 10 real categories/52 dishes and preserved invoice #23 (10,000 VND) and completed orders #42/#43, each once, through QA customer-scoped reads. This checks a narrow real fixture, not every table/payment ledger or all B0–B7 HTTP paths.

Database ownership is documented and historically audited; no new schema ownership/role isolation SQL probe was executed. Local container health does not prove RabbitMQ publish/consume, transaction rollback, retries, idempotency, or timeout behavior. Historical Q1 event delivery and payment closure evidence is retained as historical. No global recovery/cleanup endpoint was invoked.

## 5. Android Customer and real device

`adb devices -l` detected Samsung SM-A528B, Android 14. Existing app version 1.0/versionCode 1 was launched without uninstall, data clear, logout, adb reverse, or APK overwrite. Real Home showed Bình Thạnh Catalog and the Bàn 2 “Đang yêu cầu thanh toán” card. This agrees with the latest owner-backed **historical** state: Session #20 `PAYMENT_REQUESTED`, Order/item #38 `READY`, Table 37 occupied, 35,000 VND, no payment/invoice. It contradicts the older `ACTIVE`/`CONFIRMED` baseline copied into the task; this round did not change or re-query the owner's canonical state.

Three cold relaunches showed real category content by 4.79/8.41/5.81 s and card by 29.75/8.41/5.81 s (coarse UIAutomator upper bounds). Local Catalog cache restore itself was 34–46 ms; two cold branch requests had ~24–26 s TTFB. No crash/ANR was seen in bounded filtered logcat. Physical Samsung populated Order/Invoice History, landscape, offline/network-loss, QR scan, reservation and payment were not exercised; prior local/emulator evidence is not substituted.

Candidate native access remains blocked: historical ordinary CLOUD_PREVIEW APK received Vercel Authentication 401 before Identity. A historical emulator-only loopback bridge proved Q1 login/Home/history using short-lived memory-only protection access, but it does **not** turn a distributable candidate APK into an accessible build. No bridge or secret was restarted/embedded here.

## 6. Chef, Cashier and Admin

- **Chef:** Q1 authenticated browser Dashboard/History, two QA rounds, valid kitchen transitions and 4-second queue polling are `PASS HISTORICAL`. No fresh role login or mutation this round; current browser cookie was Cashier.
- **Cashier:** Fresh actual candidate browser loaded Quận 1/cashier_lan Dashboard, 20 free tables, old QA payment reference #23 “Đã thu tiền”, empty active service queue; History route opened. Its 0 **legacy cashier invoices** must not be misreported as missing customer mobile invoice #23. No confirm clicked.
- **Admin:** Q1 Dashboard, Catalog image/search/table read and historical isolated local CRUD are `PASS HISTORICAL`. Current Admin login/CRUD was not repeated. Admin's accepted staging permissions include shared Catalog/customer management; it is not universally branch-limited.

Current browser measurements were not made in matched cohorts. The historical candidate Cashier table-ready p50 2.10 s/p95 5.38 s (n=3) and earlier 12.18 s/p95 12.84 s are different process/network conditions, not a demonstrated optimization. No browser waterfall was available in this round.

## 7. Cloud, realtime and security

Candidate CLI inspect: Preview/Ready, not Production. Protected `/healthz` and `/readyz` returned 200 through CLI; public native access still must not be inferred. Fresh WSS run passed 17/17 own/wrong-branch and reconnect subscription checks, including anonymous/customer staff-hub denial. Historical Q1 positive event delivery to distinct instances and UI polling/reconnect remains valid **historical** evidence. A second authenticated customer did not listen during an isolated event in this read-only round: cross-customer *payload* isolation is **BLOCKED/UNVERIFIED**, not PASS. New run's staff sockets landed on one instance, so new multi-instance fan-out is not claimed.

Contract tests and prior Q1 HTTP role/branch 401/403 checks support authorization, but a complete current CORS/JWT expiry/admin mutation/cross-customer data-leak and secrets audit was not run. No security weakness was deliberately introduced or protection disabled. Connector Vercel inspection returned scoped 403; linked local CLI supplied the same read-only deployment inspection successfully. No token, cookie, password or connection string is retained.

## 8. Performance

| Metric / environment | New n | New p50 | New p95 | Historical comparator | Interpretation |
|---|---:|---:|---:|---|---|
| Samsung stable APK Activity start | 3 | 1.215 s | 1.274 s | Prior 1.421 s p50, different observation cohort | Not data-ready or controlled before/after |
| Samsung stable APK real Home categories observed | 3 | 5.81 s | 8.41 s | Prior 5.094/5.208 s, different cohort | Coarse UI upper bound; no causal improvement |
| Samsung stable APK session card observed | 3 | 8.41 s | 29.75 s | Prior not seen by ~17 s in 3 runs | Material variability; open release risk |
| Candidate invoice QA API | 3 | 0.909 s | 2.599 s | Stable 1.321/1.442 s, n=3 | Candidate cohort mixes process context |
| Candidate order-history QA API | 3 | 3.284 s | 11.161 s | Stable 3.354/3.831 s, n=3 | 11.161 s was new process/sequence 1 |
| Cashier browser tables usable | 0 new | — | — | Historical 2.10/5.38 s, n=3 | Not a new UI speed claim |

Percentiles use nearest-rank. API 200, Activity launch, cached Home render and browser usable are separate metrics. There is no approved SLA. Proposed **future** gate: Home meaningful content p95 ≤ 5 s and existing-session card p95 ≤ 10 s under a controlled network/process cohort; these are recommendations, **not** criteria retroactively imposed on this run. Also collect n≥20 and first-request vs warm instance IDs before declaring speed improvement.

## 9. Data consistency, known gaps and readiness

The isolated QA invoice/payment/session closure was verified historically; new repeated customer reads show exact invoice amount and two unique completed orders. There is no evidence of duplicate records in this fixture, but no global duplicate audit. Protected Session #20 was not paid/closed. Read-only settled recovery inspection of QA #24 is historical; the available manual endpoint includes unscoped idle cleanup, so safe fault/restart recovery acceptance is blocked. Supabase Storage image response is historical, not retested upload/read/delete.

Detailed P0/P1/P2 findings and reproduction/evidence boundaries are in [REMAINING_ISSUES.md](REMAINING_ISSUES.md). Release gates are in [RELEASE_READINESS.md](RELEASE_READINESS.md). Stable alias stays where it is; candidate is not approved for promotion or handoff as a fully accepted release. Working tree's pre-existing source/scripts and `.idea` edits were preserved. No deploy, commit, push, reset, or Production operation in this round.

## 10. Acceptance verdict

**CONDITIONAL / PARTIAL ACCEPTANCE.** The system is demonstrable for historical isolated Q1 role flow and current read-only stable/candidate data. Full handoff needs a native-accessible candidate strategy without embedded secret, an isolated negative two-customer WSS event test, safe fixture-scoped recovery evidence (or explicitly accepted exclusion), current physical-device populated-history and core UI acceptance, and a controlled latency/reliability gate. Do not infer `FULLY ACCEPTED` from passing builds or unit suites.
