# Full-system performance audit

Ngày đo mới: 2026-10-10 (Asia/Ho_Chi_Minh). Base: `38f6762`, branch
`cloud/postgres-migration`. Không lấy số đo lần trước làm baseline lần này.

**VIETTABLE FULL SYSTEM PERFORMANCE: PARTIAL**

Đây là baseline/chẩn đoán đang dang dở, **chưa phải nghiệm thu tối ưu**.
Chưa sửa source, chưa build APK mới, chưa redeploy, chưa chuyển alias.

Origin được đo: `https://viettable-staging-hienkhanhvu981.vercel.app`.

## A. Root causes và mức độ bằng chứng

### 1. Public Staff assets phụ thuộc CloudHost startup và bị no-store

- Browser thật đã mở Chef Login, quan sát bundle
  `/app/chef/assets/index-DHzomjAt.js` và CSS `index-tcNA08Oq.css`.
- HTTP diagnostic của chính asset này, gzip bật: JS TTFB **25.243 s**,
  total **25.848 s**, transfer **86,535 bytes**. Lượt sau TTFB **1.288 s**,
  total **1.712 s**, transfer **86,513 bytes**. Đây là HTTP asset measurements,
  không phải browser time-to-interactive.
- Cả hai lượt: `Cache-Control: no-store, no-cache, must-revalidate, max-age=0`,
  `X-Vercel-Cache: MISS`, `Content-Encoding: gzip`.
- Correlations: `2d9f3c88bdac4c5bbcee10d9b0c5fdc3` (chậm),
  `6bd9280e036849c4a372131c4873d65c` (lượt kế tiếp).
- Log row của request chậm chứa startup cả năm module. ASP.NET static handler
  durations quan sát trong row này chỉ **47.739 ms / 7.677 ms**; stdout container
  có thể broadcast nhiều request nên không ghép hai duration thành một trace.
- Source: `deploy/staff-preview/vercel.json` rewrite toàn bộ tới container;
  `GatewayModule.cs` áp no-cache cho `/app` bằng `OnStarting`, đồng thời
  `MapSpa` áp no-cache cho mọi static file. Không thể chỉ đổi một callback
  rồi kết luận browser/CDN cache đã được sửa.
- Proven: hashed JS cũng đi qua container và không được cache. Chưa proven:
  breakdown chính xác platform provision/image start/application initialization.
- Candidate nhỏ: chỉ immutable hashed JS/CSS được cache; HTML và dữ liệu
  authenticated tiếp tục no-store. Chưa thực hiện trước khi đủ baseline UI.
- Candidate kiến trúc: public SPA shells/assets phục vụ bằng static routing trong
  **cùng project/origin**, API và hubs vẫn tới CloudHost. Cần user phê duyệt trước
  thực hiện theo yêu cầu dừng trước thay đổi deployment lớn.
- Trade-offs: giảm phụ thuộc cold API khi tải UI; phải kiểm tra route precedence,
  deep-link fallback, đồng bộ asset hash/release, cookie path và deployment
  protection. API/role/branch authorization không được chuyển thành frontend-only.
  Không tạo project mới, không tăng gói, không cache API riêng tư. Không cam kết
  target thời gian trước khi triển khai và đo lại.

### 2. Cold response không đồng nghĩa SQL/application handler chạy 25 giây

- `GET /api/mobile/v1/branches/3/menu`: TTFB **25.578 s**, total **25.591 s**,
  24,319 bytes; correlation `55559f42661f40f98372885cfb4131e3`.
- Instance mới `5a9624d2f10341eca17c83c01195fde7`, khác các probe trước.
  Runtime log chứa startup năm module. Gateway HTTP execution **1,622.394 ms**,
  Catalog internal execution **1,422.171 ms**.
- Khoảng **23.97 s** total nằm ngoài HTTP execution được đo, gồm platform/container
  start và application startup trước pipeline; không gọi toàn bộ phần này là
  platform latency hoặc DB latency khi chưa có startup span.
- SQL command durations quan sát quanh trace là khoảng **207–271 ms**.
  Đây là elapsed EF command (gồm network), không phải PostgreSQL engine CPU time.
- Source CloudHost build/start năm module tuần tự. Mỗi module await PostgreSQL
  migration validation trước Gateway listener. Cần instrument riêng từng build,
  validation, StartAsync, listener-ready; chưa chứng minh tỷ trọng từng bước.
- Header thực tế `X-Vercel-Id: hkg1::iad1::...`. `iad1` là runtime-region signal
  của các request đo, không suy ra toàn bộ deployment luôn chạy duy nhất region đó.
  Supabase region đã xác nhận trước: Sydney `ap-southeast-2`.
- Chưa đo connection acquisition, TLS DB, pool waits, serialization riêng biệt;
  không tăng pool hoặc tạo index khi chưa có bằng chứng/EXPLAIN.

### 3. Android bootstrap để Catalog/private ancillary reads chặn phiên bàn

- Source `MobileRepository.refreshBootstrap`:
  profile song song, nhưng chain còn lại là
  `branches → menu → favorites → reservations → active session → active orders`.
- `loadSelectedBranchMenu` chỉ tới active session khi cả menu/favorites/
  reservations thành công. Lỗi dữ liệu phụ cũng có thể chặn khôi phục phiên.
- `loadActiveSession` và history còn dùng `tableAware`: chờ table-layout metadata
  trước callback. Active session có single-flight, orders chưa có ở repository.
- Một lượt Samsung cold-app: first Activity **1.281 s**, thấy cached categories/
  branch ở observation **4.870 s**, thẻ phiên ở observation **35.059 s**.
  Các UI dumps tốn khoảng 2–4 s nên đây là upper bounds, không exact render times.
  Một observation giữa lượt lại thấy History dù không có lệnh tap: lượt này chưa
  đủ điều kiện baseline controlled; cần đo lại, không dùng để tuyên bố median.
- Candidate: khởi động active-session độc lập Catalog, coalesce scope-active
  orders, giữ correctness/customer isolation và callback lifecycle; không thêm
  snapshot ACTIVE giả. Chưa implement/accept.

### 4. Chef dashboard aggregation tuần tự và polling có thể chồng nhau

- Ba API samples: **11.370 / 7.452 / 7.743 s**, 41,858 bytes, HTTP 200.
- `StaffChefGatewayController.BuildDashboardAsync` await tuần tự orders, history,
  menu, ingredients. Không phải chỉ một request SQL.
- Runtime trace `9fd907139f22393fd12c6aa8e51e7bb7`: Gateway **6,875.491 ms**;
  internal orders **2,158.097 ms**, history **2,377.844 ms**, menu **1,049.427 ms**,
  ingredients **845.532 ms**. Các internal trace IDs riêng biệt; source xác nhận
  chúng chạy tuần tự. Client sample total **7.452 s**.
- Chef React `DashboardPage` gọi full dashboard bằng `setInterval(..., 4000)`;
  `load` không có in-flight guard. Với server 7–11 s, request overlap có thể xảy ra.
  Chưa capture waterfall authenticated browser để đếm overlap thực tế.
- Candidate: bounded parallel independent aggregation + single-flight/next poll
  sau completion; không tăng DB pool và không bỏ branch auth. Chưa sửa.

### 5. Admin eager load có dependencies không liên quan tới màn hiện tại

- `AdminConsolePage.loadAll` chờ session/dashboard/categories/reports, sau đó
  dishes/units/tables/table summary trước `initialized`.
- Sau `initialized=true`, effects dishes/tables chạy thêm fetch; App cũng có
  session GET riêng. Source-backed redundant/eager reads, chưa browser trace.
- Candidate: section-specific loading với auth hiện có; tránh initial duplicate
  requests, không cache chéo role/customer. Chưa sửa.

## B. Android before / after

| Case | New before | After |
|---|---|---|
| Cold app first Activity | 1.281 s (`am start -W`, không phải tap icon) | Chưa chạy |
| Branch 3 / cached Home categories | Thấy ở dump 4.870 s; upper bound | Chưa chạy |
| Active-session card restore | Thấy ở dump 35.059 s; lượt cần đo lại | Chưa chạy |
| Current order | UI hiển thị #38, CONFIRMED, 1 × Bánh mì ốp la, 35.000đ | Chưa chạy |
| User/profile restore | Chưa đo | Chưa chạy |
| Samsung Order/Session/Invoice History + detail | Chưa đo controlled | Chưa chạy |
| Menu, branch switch, background/foreground | Chưa đo | Chưa chạy |
| 23-order QA history | API verified 23 rows; không phải Samsung account/UI | Chưa chạy |

Samsung sau unlock hiển thị bàn 2/Bình Thạnh và Order #38. Thẻ phiên hiển thị
“Đang yêu cầu thanh toán”, timestamp 11:18 / 10-10-2026; Order #38 hiển thị
11:20 / 10-10-2026. Khác fixture ACTIVE ban đầu: **chưa xác minh trạng thái canonical
Session #20**, không tự sửa/cancel/payment để làm khớp fixture.

Trong quá trình baseline Samsung `R5CRC14LFYD` mất kết nối. `adb devices` sau đó
chỉ thấy `emulator-5554`. Không logout/clear data/uninstall/đổi tài khoản/đặt món
hay tạo payment trên Samsung. Không ADB reverse.

## C. Staff Web before / after

- Browser thật: Chef Login render đúng username/password/login controls; reload
  tool wall time 25.846 s chỉ là observation, không chính xác TTFB/TTI.
- Browser asset inventory xác nhận JS/CSS của trang, gồm external Bootstrap/icons/
  Google Fonts. Chưa profile contribution của các external assets.
- Browser chưa login QA, đã đề nghị user nhập credential tại UI (không qua chat).
- Browser tool có DOM/AX, asset inventory, console; read-only scope không có
  `performance.getEntriesByType`, không có advertised network waterfall/long-task
  capability. Không dùng raw CDP/Playwright ngoài công cụ để bypass giới hạn.
- Chef/Cashier/Admin authenticated dashboard, navigation, refresh và reconnect
  **UNVERIFIED ở browser**. API samples bên dưới không thay thế acceptance UI.
- Không có After/TTI improvement, không tuyên bố React rerender là root cause
  đo được. Bundle JS có gzip, không kết luận thiếu compression.

## D. Cloud baseline mới

`values` trong measurement là HTTP status, TTFB, total, downloaded bytes.
Mọi sample dưới là GET; không đưa token/cookie/query customer identifiers vào report.
Không tính P95 với n=3. Warm = successive samples trên cùng opaque instance;
new instance chỉ coi cold khi có startup log bổ sung.

| API / scope | Total samples (s) | Median / interpretation | Bytes |
|---|---|---|---:|
| QA active session | 1.321, 0.953, 2.437 | 1.321; 404 đúng no-active-session QA, không đo populated session | 190 |
| QA orders/history | 5.398, 3.343, 3.758 | 3.758; 200, 23 rows | 15,083 |
| QA invoices | 1.860, 1.260, 1.549 | 1.549; 200 | 9,723 |
| Chef session | 1.279, 1.127, 24.628 | Sample 3 instance đổi; không gộp cold/warm | 298 |
| Chef dashboard | 11.370, 7.452, 7.743 | 7.743; cùng instance | 41,858 |
| Cashier session | 0.954, 1.236, 1.029 | 1.029 | 301 |
| Cashier dashboard | 4.968, 4.587, 5.379 | 4.968 | 1,834 |
| Cashier service requests | 1.279, 1.119, 1.097 | 1.119; empty list, không populated UI | 2 |
| Cashier onsite payments | 1.432, 1.527, 2.812 | 1.527 | 7,755 |
| Admin dashboard | 3.099, 27.420, 2.919 | Raw samples; middle instance mới, cold breakdown chưa đủ | 4,398 |
| Admin categories | 2.200, 2.220, 1.783 | 2.200 | 1,597 |
| Admin dishes | 2.057, 3.239, 2.098 | 2.098 | 5,313 |
| Admin tables | 2.253, 1.823, 2.057 | 2.057 | 3,058 |

Correlations tiêu biểu (sanitized metadata only):

- Orders history `perf-1361769a-4193-4508-86e5-81fce79c6e7b`: TTFB 3.747 s.
- Invoices `perf-a4ae5b49-bbf3-4968-9136-29cb246b1a3c`: TTFB 1.441 s.
- Chef `perf-02192e9e-c6fb-4c25-920b-37666e728e01`: TTFB 7.405 s.
- Cashier `perf-9a1135e9-058b-40ce-8323-3a101edd3d16`: TTFB 4.955 s.
- Admin `perf-31509edf-fb08-412f-a552-bf893b2a9abb`: TTFB 27.403 s,
  instance `385790c81ad44cb7887cebb04a1ee890`.
- Additional history read verified 23 rows, total 28.261 s, new instance
  `0d3323131b2c4cc286e3b11e58e0b230`; not included in warm n=3 median.

### One bounded concurrency sample (4 clients, no loop/load test)

| Client/API | TTFB / total (s) | HTTP | Instance |
|---|---|---:|---|
| QA orders/history | 6.241 / 6.257 | 200 | 385790c81ad44cb7887cebb04a1ee890 |
| Chef dashboard | 9.565 / 9.694 | 200 | 385790c81ad44cb7887cebb04a1ee890 |
| Cashier requests | 1.198 / 1.210 | 200 | 385790c81ad44cb7887cebb04a1ee890 |
| Admin dashboard | 27.522 / 27.534 | 200 | aa3fa7cae86e4ca6a5cbcf6939a7f93f |

Observed fan-out to another instance; not proof of DB pool saturation or realtime
multi-instance acceptance. No 5xx in this bounded sample. Correlations respectively:
`perf-a194b9cb-d62e-4cc3-950f-c56cd7489ef6`,
`perf-d7fa95e7-8756-49e9-a1ab-e3406d14551d`,
`perf-32f45776-71a7-43fc-9ba3-9170b7a01c66`,
`perf-e497475e-71c6-4c5d-81a8-8df6b9d8533c`.

Health at end: `/healthz` 200 `status=ok`, total 0.521 s;
`/readyz` 200 `status=ready`, total 1.799 s. Not used to declare business APIs fast.

## E. Regression & security

- Customer QA password login API: 200; Chef/Cashier/Admin QA login APIs: 200.
  Credential read via DPAPI into process RAM; no plaintext credential file/report.
- No business POST/PUT/DELETE, no new customer/session/order/payment/invoice.
- No DB reset, migration, index, pool setting, scheduler, Production or alias change.
- Existing `.idea/misc.xml` modified and `.idea/vcs.xml` staged add preserved.
- No source edit: Android LOCAL/CLOUD builds and unit suites **not rerun**;
  previous 54-test baseline is not a new performance/regression PASS.
- Role/branch negative tests, SignalR WSS/fan-out/reconnect, recovery, Storage and
  Samsung fixture canonical verification **not rerun / UNVERIFIED this round**.
- Candidate document secret scan: PASS (1 file, 12 DPAPI values, no secret/JWT
  match; APK not scanned because no new APK). Staged document whitespace check:
  PASS. Final diff review restricted to this document; `.idea` excluded.

## F. Remaining limitations / next execution boundary

1. Samsung reconnect/unlock needed for controlled n>=3 launch/session/history UI
   baseline. No production-code optimization before completing relevant baseline.
2. Authenticated browser sessions needed for all three staff UI baselines. Do not
   turn API login results into browser PASS; no secret handoff through chat.
3. Exact startup/pool/SQL/server/client-render breakdown still incomplete;
   stdout broadcast must be correlated before counting per-request queries.
4. Major static/CDN deployment separation awaits explicit approval; proposal above
   does not change deployed routing. Small cache-header/polling/bootstrap fixes
   can be evaluated separately after baseline and tested before committing.
5. Full Before/After, offline/slow-network, populated Samsung histories, four-client
   after sample and security regression remain required. No performance PASS yet.

**Push: NO.**

## G. Optimization continuation — 2026-10-11 (PARTIAL)

The current candidate separates staff public bundles into a Vercel static service,
coalesces Chef/Cashier reads, parallelizes independent Chef/Cashier dashboard HTTP
reads, removes eager Admin section reads, and separates Android active-session
bootstrap from unrelated catalog/profile/reservation work. Session history now
loads when its tab is selected. These changes do not add business mutations.

Latest candidate: `dpl_DbhjvMbVgY6hh4Y6iY8hDQPFHgie` at
`https://viettable-qoq3x94f5-hienkhanhvu981-7145.vercel.app`.
CLI inspection confirms **target=preview, Ready**. Health/readiness returned 200
(`ok` / `ready`). Stable staging alias has NOT moved to this candidate.

### Actual measurements (HTTP, not authenticated browser UI)

| Scenario | Observed samples / seconds | Interpretation |
|---|---|---|
| Chef hashed JS, first static candidate | 1.202, 0.801, 0.964, 0.976, 0.561 | First MISS, four HIT; warm median 0.882 s vs earlier repeat 1.712 s. |
| Chef dashboard, first static candidate | 30.209, 3.648, 4.283, 3.663, 5.840 | Four subsequent samples median 3.973 s; initial latency remains high. |
| Cashier dashboard, latest candidate after parallel reads | 29.278, 4.667, 4.786, 4.626, 4.776 | Subsequent median 4.722 s; no convincing improvement over baseline 4.968 s median. |
| Admin dashboard, first static candidate | 5.375, 2.051, 1.679, 1.662, 1.644 | Subsequent median 1.671 s; not a browser TTI measurement. |
| QA order history, third candidate | 27.793, 3.858, 3.669, 3.853, 6.615 | Subsequent median 3.855 s vs baseline 3.758 s: no improvement proven. |
| QA invoice history, third candidate | 2.835, 1.691, 1.682, 1.512, 1.515 | Subsequent median 1.598 s vs baseline 1.549 s: no improvement proven. |

Hashed public staff JS responses show `X-VietTable-Static: staff`, immutable
one-year caching, CDN HIT on repeated reads, and no CloudHost instance header.
Staff deep links return HTML with no-store. Missing assets return 404 rather than
SPA HTML. These establish static-service routing, not authenticated UI acceptance.

**Unresolved cache acceptance:** authenticated/anonymous API and hub responses on
the candidate still expose `public,max-age=0,must-revalidate` at the platform,
despite no-store assertions passing in local module transport and configured
private routing headers. No private-response CDN HIT was observed, but that does
not prove no-store. Do not move the alias or claim private-cache acceptance yet.

### Samsung and independent tests

Updated CLOUD_PREVIEW APK installed without clearing app data or logging out.
Two UIAutomator-observed force-stop launches reached Home/session card at
4.766/32.903 s and 6.306/6.306 s respectively. Sampling has coarse observation
bounds and no matched before baseline; n=2 does not establish performance PASS.
The card shows Bình Thạnh, Bàn 2, and payment-requested text. This differs from
the requested ACTIVE fixture expectation; no session/order/payment was changed
to force a result. Canonical fixture verification remains outstanding.

Android LOCAL/CLOUD builds and unit task passed in this round. CloudHost tests:
49/49 PASS using the Docker SDK test host (Windows Application Control prevents
the native test host). Shared controlled-read tests: 5 PASS; staff realtime tests:
6 PASS; production-bundle tests: 3 PASS. All three staff web builds passed.
Emulator real HTTPS connectivity/public-cache instrumentation: 3/3 PASS against
the unchanged stable alias, not the protected new candidate. Prior candidate
secret scan passed (24 candidate files, 2,608 APK encodings, 12 DPAPI values);
it is not a new scan of this documentation addition.

### Chef login investigation — local account versus staging

The authenticated Admin employee search for `chef_hung` returns HTTP 200,
`totalItems=0`. Source confirms this search includes inactive employees by default.
Thus this deployed Identity employee list has no `chef_hung`; active/locked/role/
branch details cannot be reported for a nonexistent staging account. No existing
password was read, guessed, decrypted or reset. Identity staff login deliberately
returns the same generic error for an absent active employee and a wrong password.

Existing QA Chef `hostqa_8c074c45aa3ed29c022249edchef0` is employee #18, active,
role `CHEF`, branch #3 (Bình Thạnh). Its authenticated Dashboard API returned 200;
a fresh normal staff login using the rotated QA credential returned 200 and
`success=true`. An initial diagnostic request had malformed double-encoded JSON
and returned 400; correcting only the test request produced the successful login.
The actual browser displays the Chef login form and has NOT reached an authenticated
dashboard in this continuation. QA credentials remain in the DPAPI/RAM workflow;
the browser tool currently has no secure DPAPI credential handoff, so credentials
were not embedded in browser commands or injected as cookies to fabricate UI PASS.

Cloud provider source uses the owning Identity connection with Npgsql when
configured Postgres and expressly forbids falling back to the local SQL Server
connection. This is source evidence; a fresh runtime provider/host diagnostic was
not obtained in this continuation. Do not confuse source evidence or account
presence with a direct runtime connection attestation.

### Remaining acceptance boundaries

- Authenticated Chef/Cashier/Admin browser UI and TTI: UNVERIFIED.
- Candidate SignalR WSS/reconnect/branch isolation: UNVERIFIED this round.
- Recovery/Storage business regression on candidate: not rerun.
- Android populated History rendering and controlled n>=5 restoration: incomplete.
- Cold-start latency around 28–30 s remains; only one Catalog build timing sample
  (5.682 s) was collected, not a complete startup/SQL/pool breakdown.
- Existing order-history source batches sessions/statuses/items and one Catalog
  snapshot per bounded page; no new N+1/index/pool change was justified by this read.

**VIETTABLE FULL SYSTEM PERFORMANCE: PARTIAL. No Production, database reset,
automatic recovery scheduler, alias promotion, `.idea` edit or push.**

Final continuation secret scan: PASS (26 candidate text files, 2,608 APK entry
encodings, 12 DPAPI secret values; `.idea` excluded). Whitespace diff check: PASS.
No commit yet: browser/runtime acceptance and complete source diff review remain
open; the existing dirty source tree and staged `.idea/vcs.xml` are preserved.

## H. Remaining bottlenecks continuation — 2026-10-11 (PARTIAL)

### Startup evidence and safe candidate

Complete application startup records were recovered for **two instances of the
preceding qoq3x94f5 Preview**, superseding section G's single-module limitation:

| Phase (ms) | Instance sample 1 | Instance sample 2 |
|---|---:|---:|
| Catalog build / start | 6344 / 57 | 5574 / 56 |
| Orders build / start | 4166 / 19 | 4118 / 18 |
| Customers build / start | 3824 / 11 | 3829 / 12 |
| Identity build / start | 4117 / 77 | 4242 / 71 |
| Billing build / start | 3997 / 14 | 3833 / 14 |
| Application listener ready | 23317 | 22524 |

This proves a substantial **application startup** bottleneck, not that platform
initialization accounts for most of the 28–30 seconds. These build phases combine
DI, application/module construction, EF model/bootstrap and any startup database
work; they do not isolate those dimensions. Container/platform initialization,
pool wait, transaction-pooler wait, TLS, JWT/DataProtection and serialization
remain individually UNVERIFIED. Runtime deployment inspection reports `iad1`;
the accepted Supabase project region is Sydney `ap-southeast-2`. Geographic distance
is context, not measured attribution of query or TLS latency.

A candidate now permits independent owner startup concurrently **only** for
VERCEL_ENV=preview + Postgres + migrations disabled + seed profile None. Production
and migration/seed runs retain sequential order. Preview-only EF metadata observers
report query/connection count and elapsed duration without SQL, parameters, claims,
headers, passwords or connection strings. Connection time is explicitly combined
pool/network/TLS, not an isolated pool-wait measurement. Cookie revocation and
role/branch/version checks execute concurrently in separate Identity scopes with
no auth cache or skipped validation. These candidate changes remain uncommitted
pending runtime telemetry/performance acceptance; unit success is not speedup proof.

New deployment: `dpl_3ha9wsyqp1STNeCQiJE216fHTAwR`.
URL: `https://viettable-o36qps373-hienkhanhvu981-7145.vercel.app`.
Executed from the isolated reviewed context:
`vercel deploy . --target preview --scope hienkhanhvu981-7145 --yes`.
CLI inspection: **target preview, READY**. Build took approximately 122 s.
Health returned HTTP 200/ok in 24.111 s; readiness HTTP 200/ready in 3.713 s.
These HTTP totals do not establish startup phase timings on the new deployment.

Bounded CLI log reads, including explicit deployment ID, returned zero records.
The runtime-log connector independently returned scope authorization **403**.
Therefore execution of the parallel-startup guard and new per-request EF/auth
measurements has NOT been proven on the new deployment. No platform/env workaround
or protection disable was introduced to manufacture telemetry.

### Before/after measurements and limits

| Scenario | Before | Latest observation | Evidence class |
|---|---|---|---|
| Cloud first request | 28–30 s | Health 24.111 s; valid Chef first request 29.516 s | HTTP; no consistent cold-start improvement proven |
| Chef JS warm | 0.882 s median | 0.704 s median, four HIT reads | Actual deployed hash, HTTP/CDN, not browser TTI |
| Chef dashboard warm | 3.973 s median | 3.868 s median | Authenticated API, n=4 subsequent samples |
| Cashier dashboard warm | 4.722 s median | 4.740 s median | Authenticated API, n=4 subsequent samples |
| Admin dashboard warm | 1.671 s median | 1.971 s median | Authenticated API, n=4 subsequent samples |
| Samsung session card | 4.766 / 6.306 s (n=2) | 6.813 s median (n=5) | Coarse UIAutomator observation, not matched A/B |
| Samsung table metadata | 32.903 / 6.306 s (n=2) | 8.502 s median (n=5) | Coarse UIAutomator observation, not matched A/B |
| Populated order-history API | 3.758 s baseline median | Previous candidate 3.855 s median reused | No improvement proven; new batch harness timed out and results discarded |
| Populated invoice-history API | 1.549 s baseline median | Previous candidate 1.598 s median reused | No improvement proven; not Samsung populated UI |

Valid Chef samples were 29.516, 4.568, 3.876, 3.761, 3.861 s (41858 bytes).
Cashier samples: 7.366, 4.758, 4.722, 4.794, 4.651 s (1834 bytes).
Admin samples: 3.213, 1.809, 2.624, 1.858, 2.084 s (4393 bytes).
An earlier Chef series returned 401 due to an obsolete QA cookie and is excluded
from performance statistics. Fresh normal QA login restored 200; no cookie bypass.

Five exact deployed Chef JS samples: 0.898 MISS, 0.874 HIT, 0.693 HIT, 0.551 HIT,
0.715 HIT, HTTP 200, 277857 bytes, X-VietTable-Static=staff. The first test used a
local-build hash and got 404; those reads are excluded, then the actual asset path
was read from served HTML. Private API/hub cache acceptance from G is still open.

### Android runtime acceptance (read-only, unchanged stable alias)

Bootstrap independently starts active session, reservations, profile and
branch/menu/favorites reads; bootstrap and per-scope order fetches are coalesced.
Active-session REST now updates state and starts orders without waiting for the
Catalog table directory. Table-label enrichment only applies to the same current
session object, and never infers display number from table ID. Session history is
lazy on its own tab; invoice history remains lazy. No fake snapshot permits mutation.

| Samsung run | Card observed ms | Table label observed ms | Activity start ms |
|---|---:|---:|---:|
| 1 | 40039 | 40040 | 1108 |
| 2 | 4852 | 29154 | 1139 |
| 3 | 4727 | 8151 | 1105 |
| 4 | 6813 | 6813 | 1111 |
| 5 | 8502 | 8502 | 1100 |

Run 2 directly demonstrates card availability independent of table metadata.
The cold outlier remains unresolved; these are sampled upper bounds, not frame-level
TTI or proof of uniform startup acceleration. Samsung's own Session History tab
displayed loading then a genuine empty state. Populated Samsung history/detail
performance remains UNVERIFIED; no business data was created to fill that screen.
The Invoice History tab likewise showed real loading then `Chưa có hóa đơn đã
phát hành`; this is empty-state evidence only, not populated invoice/detail PASS.

Samsung continues to show Bình Thạnh/Bàn 2 and payment-requested text rather than
the requested ACTIVE fixture expectation. No mutation/reset of session #20, order
#38, payment/invoice or user account was made to force it back. Canonical ownership/
state verification is still required; source/UI evidence is not a database audit.

### Tests, source review and commit boundary

- Android `assembleDebug`, `assembleCloudPreview`, `testDebugUnitTest`: PASS;
  XML totals **54 tests, zero failures/errors**. Installed APK with update-only,
  no logout/clear-data/ADB reverse.
- Emulator HTTPS connectivity + public catalog cache: **3/3 PASS**, 32.942 s,
  against unchanged stable alias, not candidate protected Preview.
- CloudHost Docker SDK test host: **61/61 PASS**, including startup guard/order,
  metadata-only diagnostics, concurrent cookie validation and fail-closed errors.
- All three staff `npm run build`: rerun PASS. Shared controlled-read tests:
  **5/5 PASS**, no overlapping polls, event coalescing, visibility resume, cleanup
  and error backoff. Earlier realtime 6/6 and production-bundle 3/3 are reused.
- React source review checked effect cleanup, latest callbacks, stale section/list
  generation guards and no mutation retries. No framework/architecture migration.
- Final candidate/APK secret scan: PASS, **32 candidate text files, 2608 APK entry
  encodings, 12 DPAPI values**, with values never printed and `.idea` excluded.
  `git diff --check`: PASS.
- Source review does not justify an index/pool/timeout change. History is batched;
  a prefetch of dish snapshots before successful live Catalog lookup is a remaining
  redundant-read candidate, not a proven or implemented optimization in this round.

Commit boundary: Android bootstrap/history and staff controlled-read/dashboard
changes with their required build inputs and targeted tests may be preserved as
tested source; browser acceptance and latency objectives remain PARTIAL. Startup,
auth timing and static-routing candidate work stays in the working tree where
runtime evidence is missing. Existing staged `.idea/vcs.xml` and modified
`.idea/misc.xml` are left exactly outside the task commit.

### Remaining blockers

Cold-start improvement and per-request DB/auth breakdown are not yet proven;
Cashier remains approximately 4.74 s, above the reference target. Authenticated
Chef/Cashier/Admin browser TTI is **UI BLOCKED/UNVERIFIED** (no supported secret-safe
DPAPI handoff to browser). Populated Samsung histories/details, candidate WSS/
reconnect, Storage, durable recovery and role/branch runtime regression remain
UNVERIFIED this round. No successful API read is substituted for these acceptances.

**VIETTABLE FULL SYSTEM PERFORMANCE: PARTIAL. Stable alias unchanged; no Production,
keep-alive, paid scaling, automatic recovery scheduler, database reset or push.**
