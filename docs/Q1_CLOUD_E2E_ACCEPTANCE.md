# Quận 1 Cloud Preview acceptance — 2026-10-12

Status: **PARTIAL — candidate Vercel Authentication blocks native Android; scoped recovery and negative event-delivery gates remain incomplete**.
No production deployment, stable-alias change, database reset, global recovery,
Samsung logout/data clear, commit or push. Existing `.idea` and routing edits retained.

## Target and accounts

Candidate: https://viettable-k8j32aegw-hienkhanhvu981-7145.vercel.app

Deployment `dpl_7MxW7oBh8H1DCYmPSefcYX7Hf8fE`: inspected READY, target `preview`.
Health and readiness HTTP 200, respectively 0.689s and 1.892s (warm observations,
not cold startup measurements). Stable alias was not modified.

Real user-assisted browser logins: `admin`, `chef_hung`, `cashier_lan` at branch 1.
No password, cookie, JWT or connection string is stored in this report.
Admin entry `/app/admin/login`; Cashier `/app/cashier/login`; shared staff form
`/app/chef/Staff/Account/Login`. Shared cookie means staff roles are tested sequentially.
Do not perform API login with the same account during its browser acceptance:
the previous Admin browser session was rejected after verification logins. A fresh
manual Admin UI login resolved it; this is not claimed a fixed authentication bug.

## Isolated fixture

| Entity | ID / evidence |
|---|---|
| QA customer | 23, `q1uat_20261012` |
| Branch | 1 — Quận 1 |
| Table | ID 1 / Bàn 1; available in Admin UI and canonical table layout before walk-in |
| QR | `QR-Q1-B01`, observed in Admin UI and validated through mobile Gateway |
| Dining session | 24, `DS-20261012035128-097028F435F441B` |
| Orders / items | 42 / 42 and 43 / 43, two separate rounds |
| Dish | Catalog dish 41 — Thêm đồ chua, 5,000 VND per round |
| Service request | 24, WATER, note identifying isolated Q1 UAT |
| Quote | 30, 10,000 VND |
| Payment | 23, `PAY-20261012041607-709F19DD113B4AD88DD72`, ONSITE/CASH/CASHIER |
| Invoice | 23, `INV-17aeec74d2ca432d847b7c01e606172e`, 10,000 VND |

Before creating the fixture: QA customer had no active session (404), no active
reservation, and table availability was checked live. Writes use stable per-command
idempotency keys and guard branch/customer/session/order/table ownership. A failed
order request was not treated as success or retried with a new key/session.
Runtime ID receipt is stored outside the repository, without tokens/passwords.

Protected Bình Thạnh data: no writes to Table 37 / Session 20 / Order 38. Read-only
mobile table layout confirms Table 37 remains occupied. QA customer GET Session 20
returns 403: its canonical ACTIVE/CONFIRMED/amount state cannot be asserted from this
customer's permissions. User-supplied protected state is not relabeled runtime-verified.

## Actual UI and API results

| Test | Result | Evidence / boundary |
|---|---|---|
| Admin login / Dashboard | PASS | Actual browser shows Quận 1, 3 employees, 20 tables |
| Admin categories | PASS | 10 categories; reload retains data and session |
| Admin dishes / search / images | PASS | 52 dishes; ten displayed images complete with naturalWidth > 0; Bánh mì search returns correct 35,000 VND item |
| Admin tables / branch scope | PASS | 20 Quận 1 tables; branch selector restricted to Quận 1 |
| Admin order management | LIMITED | Dashboard order counts exist; no invented standalone order CRUD menu is claimed |
| Chef login / Dashboard | PASS | Actual Quận 1 user `chef_hung`, QA order 42 visible |
| Chef new order arrival | PASS — polling | Round 43 appears without page refresh; source uses controlled 4s polling, not WSS |
| Chef kitchen actions | PASS | Real UI BẮT ĐẦU NẤU / HOÀN THÀNH for QA orders; REST PREPARING/READY |
| Chef History | PASS | Actual UI retains both QA rounds in newest-first order, correct table/item/status |
| Customer item receive | PASS — API only | READY -> SERVING via existing customer Gateway endpoint, not fake UI |
| Cashier login / Dashboard | PASS | Actual Quận 1 user `cashier_lan`, 19 free tables / QA table occupied |
| Cashier service arrival | PASS — polling | WATER 24 appears without refresh; component controlled polling 5s |
| Staff service actions | PASS | Actual Cashier UI Tiếp nhận -> Hoàn tất; canonical customer REST request COMPLETED |
| Cashier payment arrival | PASS — polling | Actual UI shows payment reference, session, 10,000 VND and pending collection, without page refresh |
| Customer checkout/payment | PASS — API only | Persisted quote/payment; Session PAYMENT_REQUESTED, order SERVING |
| Cashier collection | PASS | Following user confirmation, UI already showed Đã thu tiền and no confirm button; no duplicate collection clicked |
| Invoice / closed customer history / table release | PASS — API and Cashier table UI | Payment 23 SUCCEEDED, Session 24 CLOSED, orders 42/43 COMPLETED; invoice 23 exactly once, two 5,000 VND lines; table 1 released; Cashier shows 20 free / 0 occupied |
| Cashier legacy History | PASS read-only, not populated mobile invoice acceptance | Actual role/account page shows zero legacy bills before and after mobile collection; mobile invoice is verified through customer API, not this legacy list |

## Realtime evidence

Customer WSS handshake and event delivery were tested with real authenticated
connections, not mocked SignalR frames. Only QA session/order/request frames are
reported; credentials and other customers' payloads are not logged.

- First observer established distinct instances but its original filter discarded
  Chef frames lacking `diningSessionId`. This harness bug was fixed to recognize
  known QA `orderId`; discarded events are not retroactively claimed received.
- Observed order 42 READY/SERVING and order 43 CONFIRMED/PREPARING/SERVING delivery.
  For order 43 creation, publisher API instance `d51d2079de70411eb8b33cc01797a292`
  differed from receiving instance `336a2ec3941940c389aec1ca575d26a0`.
  Both connected clients received the CONFIRMED frame once.
- After transport reconnect, socket A instance
  `743afd8ba22d46a5a57213e02521bb70`, socket B instance
  `e61a6d40f95e447f9daf50b19ab4593e`. Both received request 24 ACKNOWLEDGED
  at 04:15:14.915 UTC and COMPLETED at 04:16:34.608/609 UTC, once per socket/state.
  Distinct receiver instances are proven, not inferred from socket count.
- UpdatedAtUtc -> client receive: ACK 817ms; COMPLETE 615/616ms. These include
  application/broker/network delays and are not pure broker or UI render latency.
- UI click -> received notification: order 42 READY 15.755s; order 43 PREPARING
  11.404s; request ACK 2.386s. Slow mutation processing is distinct from broker delay.
- WSS closes with code 1006 repeatedly at approximately 310 seconds. Cashier
  browser log confirms automatic reconnects at 04:18:11.450, 04:23:23.009 and
  04:28:33.972 UTC, about 1.257/1.744/1.234s after the respective disconnects.
  Observer reconnect also restored delivery on new instances. Cashier source
  rejoins its branch and REST-resyncs on reconnect; six existing tests verify
  this behavior. No unbounded retry or write replay was added to the app.
- This pattern is consistent with the documented Vercel WebSocket duration
  lifecycle, but the exact deployment timeout cause is **not proven** by a matching
  invocation termination trace. The bounded CLI log query returned no parsed
  RealtimeTrace records; do not invent broker event IDs or claim missing logs
  prove absence of events. [Vercel reconnect guidance](https://vercel.com/docs/functions/websockets#handle-disconnections-and-reconnects).
- Other-customer negative WSS delivery and wrong-branch WSS subscription were
  not newly exercised in this round. HTTP role/branch isolation below is separate.

## Authorization

- Customer JWT -> Chef mutation: 401; valid Admin create-category mutation: 401.
  No category was created. Initial invalid-body 400 is excluded as an auth proof.
- Chef -> Admin Dashboard: 401 (current runtime).
- Cashier UI navigating Admin Dashboard redirects back to Cashier, not Admin.
  Cashier API -> Admin denial was verified in account provisioning and is reused,
  not relabeled a new API measurement while its UI session is active.
- Admin employee/table reads requesting branch 3 remain branch 1 (current runtime).
- Existing QA Cashier branch 2 -> request 24 acknowledge: 403.
- Existing QA Cashier branch 2 -> payment 23 confirm: 403; payment remains PENDING.
- Existing ADMIN permissions intentionally include shared Catalog/customer
  management, accepted by the operator; do not claim all Admin operations are branch-only.

## Performance: no application optimization or controlled before/after this round

API timing is curl total with instance/request sequence metadata. Browser timing
is UI action -> a fresh visible AX heading; includes automation overhead. These
are different measurements. UI timing cannot be split into cold/warm processes
because this browser API does not expose Network Performance entries.

| Measurement / cohort | n | p50 seconds | p95 seconds | Observed failures |
|---|---:|---:|---:|---:|
| Chef cached Dashboard navigation UI | 3 | 0.830 | 0.832 | 0 |
| Chef cached History navigation UI | 3 | 0.600 | 0.664 | 0 |
| Admin Dashboard navigation UI (process unclassified) | 2 | 2.649 | 12.240 | 0 accepted observations |
| Admin dishes navigation UI | 2 | 4.969 | 5.054 | 0 |
| Cashier History navigation UI | 1 | 10.297 | 10.297 | 0 |
| Cashier secondary Dashboard UI deadline | 1 | Not usable within 12.896 | Censored | 1 observation deadline, not proven API error |
| Chef Dashboard warm same-process API | 4 | 2.886 | 2.936 | 0 |
| Chef Dashboard first observed route, warm process | 1 | 3.733 | 3.733 | 0 |
| Admin Dashboard new-process first request API | 2 | 11.008 | 13.145 | 0 |
| Admin Dashboard warm same-process repeat API | 1 | 1.396 | 1.396 | 0 |
| Admin Dashboard first observed route API | 1 | 3.121 | 3.121 | 0 |
| Admin Dashboard cross-instance return API | 1 | 1.338 | 1.338 | 0 |

Chef Dashboard API completed 5/5 reads; Admin 5/5. Percentiles use nearest-rank
successful-response samples, not fastest-run selection. Small samples are smoke
evidence, not capacity claims. Login UI entry/submission timing was not instrumented.
A stale 237ms Admin heading observation is invalidated/excluded. Early fixture
helper 400/404 and malformed client URL attempts are retained as harness errors,
not hidden retries or representative production API stability samples.

Warm Chef critical read: history around 2.18s, 8 SQL reads, ~1.73s SQL plus ~0.44s
acquisition; active-order read around 0.21s, one SQL read. Reads run concurrently,
so summed SQL/phase durations must not be equated with total request wall time.
History remains the measured critical path; no speculative index/cache change.

## Tests and changes

- Admin, Chef, Cashier `npm run build`: PASS, new executions.
- Controlled read + staff realtime tests: 11/11 PASS, new execution.
- Preview benchmark metadata/cohort tests: 6/6 PASS, new execution.
- Secret scan: 19 candidate text files, 17 DPAPI secret values, no matches; `.idea`
  excluded. No APK scan was performed.
- Helper PowerShell / Node syntax PASS; `git diff --check` PASS.
- No Android source/install/data change. No new app/CloudHost deployment needed.
- Only local QA harnesses/documentation changed in this acceptance task. Helper
  fixes: Windows JSON encoding, correct contract envelope/route, URI array guard,
  event scope filter and bounded observer reconnect. These are **not** app bug fixes.
- HEAD remains `7652ee8`. Existing uncommitted account-bootstrap/access helpers,
  Gateway/routing changes and two `.idea` changes are preserved.

## Post-collection verification and remaining gates

Final read-only verification completed successfully. Invoice 23 contains exactly
two lines, each 1 × Thêm đồ chua at 5,000 VND; total 10,000 VND. Both order rounds
42/43 occur once in customer history and are COMPLETED. Request 24 remains COMPLETED.
No additional fixture/payment was created. Cashier UI reflects payment collected
and table 1 free. Initial final-helper assertion failed because it treated the
invoice-history `{ items, nextCursor }` envelope as an array. Source inspection
identified this harness mistake; only the helper was corrected. No API/data fix.

Three post-collection invoice-list reads: HTTP 200 on all, total seconds
1.084459, 2.215270, 9.296128. The last is a new-process sequence-1 request,
so these are not a homogeneous warm benchmark. Three order-history reads:
3.173013, 10.697934 (sequence 1), 2.860098 seconds; all HTTP 200. This is limited
stability evidence, not a 20–30 sample claim or a proven fix of historical failures.

Remaining: actual customer/Samsung UI acceptance, other-customer negative WSS
delivery and wrong-branch WSS subscription; Cashier API cohort benchmark and
controlled UI login-to-usable timings. Cashier legacy history is not the mobile
invoice list and must not be marked populated mobile-history UI PASS.
No stable alias promotion or new deployment. No commit/push in this acceptance
round; existing mixed working-tree edits and staged `.idea/vcs.xml` retained.

## Final validation round — 2026-10-12

This section supersedes the earlier remaining-gates list, not its historical evidence.

### Native Android and real Samsung

Samsung `SM-A528B` reconnected. Its installed application/account/environment were
not replaced, logged out or cleared; branch remained Bình Thạnh. Read-only navigation
showed real Home categories and Bánh mì ốp la / Bò né. Invoice History and closed-session
history showed empty states for this customer: **not populated Q1 history acceptance**.
Three process-cold launches measured Activity 1442/1293/1421ms and Home real Catalog
observed at 5208/4824/5094ms. Home p50 5.094s, nearest-rank p95 5.208s, n=3, 0/3
observation failures. UI dump overhead is included; public cached Catalog rendering
is not a fresh network measurement. Active-session card was not observed by
17638/17184/17678ms after launch (3/3 observation deadlines); a subsequent read did
show Bàn 2 Bình Thạnh again. No restore percentile or fast-restore PASS is claimed.

Small_Phone was reopened with its AVD data retained. A temporary local Gradle init
artifact built an emulator-only CLOUD_PREVIEW APK with **candidate** BuildConfig
origin, not LOCAL or stable. Build and emulator install passed; Samsung was not
reinstalled. App opened its real onboarding screen, Activity 1966ms, not Catalog.
The first instrumentation attempt reported process crashed during startup (cause
unproven); the bounded repeat completed both tests and **2/2 failed**: system-TLS
`/healthz` HTTP 401 and ApiClient `Protected by Vercel Authentication`. Candidate
Deployment Protection is a proven native access blocker before VietTable login.
QA credential entry was therefore not attempted. No OIDC/bypass secret is embedded
in APK, no protection rule was changed, no reverse tunnel or Docker backend was used.
The customer helper supports `-Role Customer -CheckOnly`; current-user DPAPI check
passed without displaying its value. Once an approved native-accessible QA target
exists, use the LOCAL ONLY masked dialog and enter credentials manually on emulator,
never Samsung. Do not send passwords through ADB arguments or chat.

### Official protected state — read-only owner queries

`CloudRecoveryInspect` now permits missing invoices and projects status/timestamps
from each owning DbContext, without SaveChanges or recovery calls. SDK build passed
with zero warnings/errors. Staging verified-TLS reads showed:

- Session 20, branch 3, table 37: **PAYMENT_REQUESTED**, not historical ACTIVE.
  UpdatedAtUtc `2026-10-10T12:51:18.538082Z`; payment-request timestamp
  `2026-10-10T12:51:18.538064Z` (19:51:18 Vietnam on October 10). EndedAtUtc null.
- Order/item 38: **READY**, one item 35,000 VND. Order entity has no UpdatedAt field;
  latest related business audit `ORDER_ITEMS_READY` is
  `2026-10-11T17:50:30.472402Z` (00:50:30 Vietnam on October 12).
  Audit also contains earlier PREPARING actions. These precede this validation round;
  this report does not attribute the actor or invent a later timestamp.
- Table 37 OCCUPIED, no payments, invoiceCount 0. No business mutation was performed.
- Android mapping deliberately maps PREPARING/READY to aggregate IN_PROGRESS,
  displayed ĐANG XỬ LÝ; item READY displays Đã sẵn sàng. This explains labels but
  does not reverse the actual DB changes from the historical baseline.

### Negative WSS subscription controls

New native WSS harness passed **17/17** checks on candidate. Anonymous mobile hub
401; customer JWT to staff hub 403; customer cannot invoke arbitrary SubscribeBranch,
SubscribeTable or SubscribeCustomer on mobile hub. Existing QA Chef branch 3 and
QA Cashier branch 2 could subscribe their own branches; branch 1 and legacy table
subscriptions were rejected. Disconnect/reconnect repeated all branch checks
successfully. These are non-mutating subscription tests, not operations on branch 3.
The final bounded rerun again passed 17/17; Chef connection moved from instance
`71256b1eec8b47dd87cc292fe9ae91aa` to `0945ab30154c4a5aa41b690295f5fbef`
on reconnect, with own-branch success and wrong-branch rejection preserved.
Only boolean results, role and instance IDs are emitted; unsolicited business
payloads are discarded. Existing Q1 cross-instance positive delivery is reused.
**Negative event-delivery isolation remains UNVERIFIED**: no new cross-customer
event was produced in this read-only round. Do not equate invocation denial with a
two-customer payload-leak experiment.

### Recovery boundary

QA 24 read-only durable inspection: one invoice 23 / 10,000 VND, payment SUCCEEDED,
session CLOSED, orders 42/43 COMPLETED, table AVAILABLE, settled=true,
RecoveryAttemptCount=1, pendingReady=0, readyEvents=2, notificationCount=2.
This confirms settled data, **not failure/restart recovery acceptance**.
Standalone manual recovery is **BLOCKED**: endpoint has no session/branch filter
and calls `RunAsync(includeIdleCleanup: true)`. No global recovery/cleanup was invoked,
no fault injected, and no automatic/unattended scheduler enabled by this task.

### UI performance and paired API reads

Cashier candidate Dashboard reload -> actual table data: 12.177/12.836/11.795s,
n=3, p50 12.177s, nearest-rank p95 12.836s, 0/3 completed observation failures.
Three earlier 3.522/3.636/3.489s loading-only observations are censored/excluded:
they are not Dashboard usable samples or proved HTTP failures. Selector deadline
was also a harness observation failure, not a login failure; later UI retained data.
Cold/warm browser-process class, network errors and duplicate-request counts are
unmeasured. Prior Chef/Admin actual UI timings remain baseline, not new comparisons.

Same QA account/data, three invoice + three order reads per origin: **12/12 HTTP 200**,
exactly one QA invoice and both completed orders per read. Stable/candidate each
returned 10 categories/52 dishes. Native candidate 401 is reported separately.

| API cohort | n | Seconds / p50 / p95 | Failures |
|---|---:|---|---:|
| Candidate invoices, first observed/cross-instance (not pooled warm) | 3 | 1.231292 / 1.728532 / 13.119496 | 0 |
| Candidate Orders first observed route | 1 | 4.010740 | 0 |
| Candidate Orders new process sequence 1 | 1 | 14.239192 | 0 |
| Candidate Orders warm same-process | 1 | 3.646636 | 0 |
| Stable invoices first observed route | 1 | 1.871588 | 0 |
| Stable invoices observed same-instance repeats | 2 | p50 1.408024 / p95 1.476473 | 0 |
| Stable Orders first observed route | 1 | 3.565130 | 0 |
| Stable Orders observed same-instance repeats | 2 | p50 3.388319 / p95 3.455104 | 0 |

Candidate warm Orders instance `d69699b740054c1782b1a9c2f34a4c32`; all stable reads
instance `9011e6e0bb994ed6abbd826de5c76f4e`, stable sequence/timing absent so cold
classification is not proven. Candidate invoice 13.119496s used sequence 5 with
preheaders 333.6ms and SQL 210.2ms: most time is outside measured application phases;
root cause is not established. Stable first observed login 27.573458s is not an
instrumented browser login or a proven new-process sample. No equivalent UI
stable/candidate cohort, improvement percentage or first-meaningful-paint claim.

### Release decision, tests and change boundaries

Candidate inspected again: `dpl_7MxW7oBh8H1DCYmPSefcYX7Hf8fE`, READY, **preview**.
Authenticated CLI health/readiness 200 at 1.798396/4.298830s; anonymous native
candidate health 401 is deliberately not hidden by those protected CLI successes.
No redeploy/alias promotion. Storage positive runtime evidence is reused from the
prior round, not a new upload/read/delete; cached phone images are not Storage API proof.

New tests: CloudPreparation build PASS; RealtimeDiagnosticsTests 6/6 PASS; benchmark
helper tests 6/6 PASS; negative WSS checks 17/17 PASS; helper syntax PASS; Android
candidate/test APK build PASS; emulator native tests 0/2 PASS, blocked by protection.
First .NET no-restore attempt failed NETSDK1064 missing cached EF InMemory; exact
existing dependency restore in test NuGet volume allowed the targeted suite to pass.
Frontend builds 3/3, controlled read/realtime 11/11 and Android unit results are
reused prior baseline, not rerun this round. No Android production source edit.

Stable alias remains unchanged. **Candidate is not eligible for promotion**:
native Android gate blocked, populated Q1 UI/restore and negative delivery incomplete,
scoped recovery unavailable, and tail/UI latency not proven improved. Commit only
validated inspection/test helpers and audit documents; exclude old routing/Gateway,
bootstrap/account-provisioning edits, `.idea`, APKs and temporary artifacts. Push NO.

## 2026-10-12 — protected candidate on isolated Emulator (follow-up)

The Vercel 401 is Deployment Protection, before VietTable Identity. A debug-only
QA APK on `Small_Phone` used `http://127.0.0.1:48127`, forwarded with ADB reverse to
a local bridge bound exclusively to Windows loopback. The bridge uses the short-lived
Vercel CLI trusted OIDC token in **process memory**, adds it only on the outbound
request to the exact candidate, and supports the SignalR WebSocket route. No bypass
credential is in the APK, Gradle config, URL, Git or logs; Vercel Authentication
and the stable alias were unchanged. This is **not** a public-cloud APK fix: the
ordinary CLOUD_PREVIEW APK still receives the candidate protection 401.

Native test `LoopbackPreviewConnectivityTest`: 1/1 PASS. `/healthz` and branches
returned 200; deliberately empty Identity login reached VietTable validation 400,
not the Vercel login page. Anonymous WSS through the bridge returned backend 401;
bridge health remained 200 after rejection. Authenticated WSS through this bridge
was not exercised, so its event delivery is UNVERIFIED.

Native `Q1PreviewReadOnlyUiTest`: 1/1 PASS on the isolated Emulator using the
existing Q1 customer QA fixture via one-shot loopback DPAPI handoff. Real UI login,
branch selection Quận 1, Home 10 categories/52 dishes, Menu, completed session
#24 and invoice `INV-17aeec74d2ca432d847b7c01e606172e` appeared. Gateway
Catalog/Identity returned 200. Active-session endpoint returned 404 for this
closed QA fixture as expected, so **populated active-session restore is unverified**.
The test triggers native view actions in `ActivityScenario`; emulator window focus
was unstable, so it is not physical touchscreen acceptance. It made no new order,
payment or session and did not touch Samsung data.

Existing WSS authorization/reconnect 17/17 and prior cross-instance positive
delivery remain valid baseline. A negative **cross-customer payload-delivery**
experiment is still UNVERIFIED: subscription denial alone does not prove it.
Scoped recovery is BLOCKED because the available manual endpoint includes global
idle cleanup; no recovery job was run. Samsung could not be measured this follow-up:
Windows/ADB listed only `emulator-5554` despite USB reauthorization. Candidate is
still **not eligible for stable-alias promotion**. No Preview redeployment,
Production deployment, database reset or push occurred.
