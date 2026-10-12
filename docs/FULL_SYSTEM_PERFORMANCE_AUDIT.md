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

## I. Startup root-cause investigation — 2026-10-11 (PARTIAL)

This round is restricted to CloudHost startup and read-only candidate verification.
No Android/React source was changed. The existing `.idea` changes and pending
static-routing work were preserved.

### Direct runtime evidence

Candidate 1: `dpl_FDDjds177Qt6EpQ2h1FHPhckQ4yR`,
`https://viettable-f1bau7tkq-hienkhanhvu981-7145.vercel.app`.
Final candidate: `dpl_CMLqcKJCqQN1SbHmKt3GpNmeoLhC`,
`https://viettable-cmi9haokw-hienkhanhvu981-7145.vercel.app`.
Both inspected as **target=preview, READY**. Deploy command was
`vercel deploy . --target preview --scope hienkhanhvu981-7145 --yes` from
separately prepared, reviewed, linked contexts; no Production or alias operation.

Preview health exposes fixed-name numeric `Server-Timing` phase metadata to
provide direct measurements despite the CLI runtime log reads still returning
zero records. No SQL, parameters, identity, cookie, token, certificate content or
connection string is projected to that header. Modules also log these phases.

| Final candidate phase | Catalog | Orders | Customers | Identity | Billing |
|---|---:|---:|---:|---:|---:|
| CreateBuilder (ms) | 81.2 | 3.6 | 2.4 | 1.7 | 2.4 |
| Registrations/config (ms) | 27.2 | 2.2 | 1.1 | 7.6 | 1.9 |
| builder.Build (ms) | 33.7 | 6.2 | 28.6 | 1.6 | 2.3 |
| DbContext resolution (ms) | 128.0 | 2.3 | 1.9 | 2.0 | 2.2 |
| Database/provider initialization (ms) | 894.8 | 108.5 | 112.9 | 113.6 | 87.0 |
| Bootstrap total (ms) | 5363.0 | 4202.4 | 4030.4 | 4137.1 | 4191.3 |

PostgreSQL pending-migrations I/O totaled **20294.9 ms across five overlapping
module checks**; schema validation total was 20417.6 ms. They are nested durations,
not additional wall-clock time. The earlier instrumented candidate independently
showed similar bootstrap (3984.7–5380.5 ms) and pending-migrations I/O total 20102.7 ms.
Gateway CreateBuilder/build/registrations were 3.8/11.4/50.1 ms. Identity JWT
registration was 5.8 ms (included in registrations, not added again).

**Proven cause:** each PostgreSQL bootstrap calls `GetPendingMigrationsAsync`
even when apply-migrations is false. Resolving DbContext is eager but does not itself
prove a connection. Provider/context initialization and actual schema I/O are now
measured separately. The preceding sequential application startup waited for five
approximately four-second schema checks. DI registration and builder.Build are
not the principal measured bottleneck. Seed/migration execution was not removed
or made implicit; production/migration/seed composition remains sequential.

**Fix:** all five independent owner modules start their required schema checks
concurrently only under the safe Preview/Postgres/no-migration/None-seed guard.
Gateway does not start until all owners finish. A required owner's failure fails
the composition; readiness checks remain real database checks. No security check,
schema validation, TLS policy, ownership boundary, pool limit or timeout was skipped.

### Startup benchmark and remaining measurement limits

- Before: direct preceding application-listener measurements **22.524 / 23.317 s**;
  first-request HTTP totals typically 24–30 s (section H).
- After: first instrumented candidate health HTTP **9.048 s**; a fresh-instance
  health on final candidate **8.731 s**. These demonstrate reduced observed startup
  request latency, not exact .NET-only wall-clock duration. A 0.585 s initial read
  of final candidate was already warm and is explicitly excluded from cold samples.
- Final readiness: **200**, 1.903 s on a warm sample.
- ApplicationStarted/main-entry logs and a direct listener-ready timing were added,
  but the listener-ready/certificate-registration values were absent from the served
  header in this round. Their runtime execution/projection remains **UNVERIFIED**;
  no exact new application critical-path total is invented from nested durations.
- Process creation, pre-Main .NET runtime, platform/container provisioning, isolated
  Npgsql pool wait, pooler wait, TLS and DataProtection initialization remain individually
  **UNVERIFIED**. Connection duration in EF instrumentation combines pool/network/TLS.
  Runtime `iad1` and Supabase Sydney are unchanged; geography is not a measured TLS cause.

### Read-only regression / warm API evidence

QA credentials stayed in DPAPI and process-memory stdin only. No password of
`chef_hung` or the Samsung account was read/guessed/reset. A mistaken diagnostic
Chef login URL returned 404; source inspection corrected it to the existing shared
staff login route, producing 200 and a real cookie. The 404 is not an app regression.

| Read | Previous warm median | New subsequent samples | Interpretation |
|---|---:|---|---|
| Chef dashboard | 3.868 s | 3.652 / 3.529 s | Candidate 1; n=2 median 3.591 s, not a full performance acceptance |
| Cashier dashboard | 4.740 s | 3.880 / 3.759 s | Final candidate; n=2 median 3.819 s |
| Admin dashboard | 1.971 s | 1.541 / 1.466 s | Final candidate; n=2 median 1.503 s |

Initial authenticated reads (Chef 7.764 s; Cashier 7.367 s; Admin 3.905 s) are not
mixed into those subsequent-read medians. Final candidate Chef login/dashboard
returned 200 (dashboard sample 6.575 s); Chef credential on Cashier dashboard
returned 401. Recovery without credential returned 401; it was not executed.
Mobile hub negotiate without JWT returned 401; this is an auth gate test, **not WSS PASS**.
Chef/Cashier/Admin positive logins and dashboard reads used their actual QA roles.

Catalog image returned 302 to the existing Supabase public storage origin, whose
HEAD returned **200 image/jpeg**. This proves media-read regression only, not upload
or all Storage operations. No orders, sessions, payments, invoices, reservations
or business-state mutations were submitted. Session #20 / order #38 were not changed.

### Tests and source boundaries

CloudHost tests rerun after source changes: **63/63 PASS** using Docker SDK.
Coverage includes five guarded startup configurations, independent startup/order,
required-owner failure, metadata-only diagnostics, cookie revocation/fail-closed
validation, existing logout/replay, origin and module-transport regression tests.
Secret scan: **24 candidate text files, 12 DPAPI values, PASS**; values never printed.
`git diff --check`: PASS. Android 54 tests/emulator 3 tests and three staff builds
from section H are **reused**, not rerun or turned into new Samsung/browser acceptance.

Runtime-tested startup/instrumentation source and targeted tests can be committed;
pending staff routing/private cache-header work remains outside that commit.
The working-tree Preview includes those preserved pending changes as well, so its
deployment is not asserted to be identical to a clean commit-only build.

### Final acceptance boundaries

**CLOUDHOST STARTUP OPTIMIZATION: PARTIAL.** Root cause and observed HTTP improvement
are supported by runtime phase evidence, but exact new listener-ready timing,
authenticated WSS/reconnect, Staff browser UI, durable recovery execution and full
Samsung session/history performance acceptance remain unverified. Warm SQL/auth/
serialization breakdown still lacks correlated runtime logs; no speculative query
plan/index/cache/pool fix was applied. Stable alias stays unchanged. No Production,
automatic recovery scheduler, database reset, Samsung logout/data clear or push.

## J. Real-world request round — 2026-10-11

Scope: continue the preserved working tree without changing runtime/language/hosting,
paid plans, Production, Android URL, Session #20 or Order #38. Measurements below
are real HTTP reads with normal QA authentication; credentials remained in RAM/DPAPI.
They are not substituted for browser dashboard or Samsung populated-history acceptance.

### Verified findings and minimal fixes

1. Orders' live dish batch performed an eager no-tracking snapshot query, then a
   second tracked query for upsert after Catalog succeeded. The first result was
   unused on success. Move that query to the fallback path: still prefer live
   availability, still persist snapshots, still propagate PostgreSQL concurrency
   conflicts, and still use Orders-owned fallback on Catalog failure. No TTL change.
2. Chef history applied `Take(take)` across all branches before filtering the Chef's
   branch in memory. Other branches could consume its page and suppress valid
   history. Apply the Catalog-resolved branch table IDs in SQL before ordering/limit.
   No cross-database read or branch authorization bypass was introduced.
3. Runtime instance turnover is directly observed. Dashboard batches were served
   by `2b9be50cc99644a8a19456bfb0a52d21`, then
   `97a9ae0bf4ec47aa8e85251fcf97a2af`. Those successive requests are NOT classified
   as warm. Immediate repeated Chef calls on `c8f3ac1154da4badb345e17762a9297d`
   were 15.059454 then 3.678678 s. This proves new-process request latency is
   materially different from same-process reuse; it does not identify the platform
   lifecycle policy or isolate every first-read DB/JIT/cache cost.

The dish optimization saves one successful-path query (execution test), but its
first before/after latency sample did not improve: 23-order baseline reads were
4.060149 / 3.534154 / 3.531126 s; intermediate Preview reads were
4.543024 / 3.661279 / 3.694338 s, same 15,083 bytes. No significant speedup is claimed.

### Final Preview and bounded read measurements

Final deployment: `dpl_GuudSyxMQNLEMcBfv6rTA5Y7RLs3`,
https://viettable-k6nf4m9yh-hienkhanhvu981-7145.vercel.app,
inspected `target=preview`, `READY`. Command: `vercel deploy . --target preview
--scope hienkhanhvu981-7145 --yes` from the reviewed isolated staff context.
It includes preserved pending routing/Gateway changes, not only the new Orders diff.
Intermediate deployment: `dpl_A8SjAZChAcZ52ogp2yNwjPDR97WM`.
No Production deploy or alias assignment was executed.

- Final `/healthz`: 200, first observed total HTTP 8.456293 s.
- Final `/readyz`: 200, total HTTP 1.963191 s.
- These HTTP timings include platform/network. New .NET-only listener-ready wall
  time remains UNVERIFIED; do not compare 8.456 directly with old .NET-only 22–23 s.
- Final Chef history (40 records, 10,540 bytes, same instance
  `98a3333df6b244c4a2f9bd98276e48ab`): 13.944071, 3.174056, 3.221177 s.
- Final Chef dashboard immediate pair: 200, 15.059454 / 3.678678 s, same instance.
- Final Cashier dashboard immediate pair: 200, 15.081156 / 4.376009 s.
- Final Admin dashboard immediate pair: 200, 4.313128 / 1.400648 s.
  Cashier/Admin pairs shared `82eb1c48b57f407d80d0179d9d6b8fb1`.
- Final customer history same-instance pair: 200, 11.940078 / 3.423984 s,
  23 orders, 15,083 bytes, `1bfd3d0113ea4b75891fd6e7fbb49f22`.
- Final invoice history: 200, 1.362376 s, 13 invoices, 9,723 bytes, same instance.
  The following protected request failed; raw output was suppressed and no sample
  was fabricated. Continuous-use stability is not accepted from these reads alone.
- History responses now observed `Cache-Control: no-store, no-cache,
  must-revalidate, max-age=0`; this narrows the previous cache-header limitation
  for the two measured routes, not all private routes/assets/errors.
- An expired QA JWT produced three 401s during an earlier invoice probe; those
  were excluded, followed by legitimate login (200). Neither Samsung login nor
  `chef_hung` was changed. Authenticated invoice reads on intermediate Preview were
  10.558963 / 1.250951 / 1.217963 s, 9,723 bytes.

### Requested experience table — layer boundaries are mandatory

| Function | Before | After / latest actual evidence | Improvement accepted? |
|---|---|---|---|
| CloudHost cold startup | .NET listener 22.524–23.317 s; earlier HTTP 24–30 s | Final first HTTP 8.456 s; .NET listener wall UNVERIFIED | HTTP reduction observed; no .NET-only percentage |
| Chef Web load | Prior API warm median 3.868 s; full browser UI unmeasured | Same-instance API repeat 3.679 s; first 15.059 s | Small API sample only; browser UI UNVERIFIED |
| Cashier Web load | Prior API warm median 4.740 s | Same-instance API repeat 4.376 s; first 15.081 s | Small API sample only; browser UI UNVERIFIED |
| Admin Web load | Prior API warm median 1.971 s | Same-instance API repeat 1.401 s; prior read 4.313 s | Small API sample only; browser UI UNVERIFIED |
| Android open app | Previous Samsung Activity timing ~1.11 s, not data-ready | This round HOT bring-to-front 226 ms, not cold launch | NOT COMPARABLE; no paired cold-launch acceptance |
| Android restore session | Previous Samsung card median 6.813 s, metadata 8.502 s, coarse observation | Existing stable-alias card Bàn 2/Bình Thạnh/payment-requested visible after history tab switch | No fresh startup-restore improvement measured |
| Android Order History | Populated QA API baseline subsequent median 3.534 s | Final QA API same-instance repeat 3.424 s, 23 orders | Small API sample; populated Samsung UI UNVERIFIED |
| Android Invoice History | Prior QA API median 1.598 s | Final QA API 1.362 s, 13 invoices; next request failed | Small API sample; populated Samsung UI UNVERIFIED |

Browser was inspected through Computer Use: existing Chef tab remains the real
login page on the earlier Preview, not a dashboard. No secret was injected into
browser automation logs and no dashboard UI PASS is claimed. Independent API
work continued. Samsung was brought forward without force-stop/data clear/logout;
its existing invoice-history view showed the real empty state. Switching to the
active tab showed `Bàn 2 • Chi nhánh Bình Thạnh`, payment-requested, one round.
No button submitting/cancelling orders, requests, payments or sessions was used.
Samsung uses the unchanged stable alias, not the protected new candidate. Its
customer has empty histories, so QA API populated results are reported separately.

### Regression and remaining work

- New execution tests: live Catalog authoritative/persisted with exactly one
  query compilation, fallback only after HTTP and only requested IDs, empty IDs
  issue no HTTP/query; two-branch Chef history keeps its own item at `take=1`.
  EF InMemory 8.0.11 is test-only, matching existing EF version; it is not production
  fake data and is not a PostgreSQL query-plan benchmark.
- CloudHost tests rerun: **67/67 PASS** (Docker .NET 8 SDK).
- MobileHistoryReadModelTests rerun: **4/4 PASS** (Docker).
- Remote Chef/Cashier/Admin builds and CloudHost publish: **PASS** on final Preview.
- Android source/APK unchanged: prior LOCAL/CLOUD_PREVIEW builds, 54 unit tests and
  3 emulator tests are reused, NOT rerun or called new device acceptance.
- Secret scan: **8 candidate text files / 12 DPAPI values PASS** before final docs;
  rerun after audit update before commit. No credentials/logs/screenshots in commit.
- `git diff --check`: PASS; two existing `.idea` changes preserved/excluded.
- Private-route/CDN negative-cache acceptance and full Staff UI authenticated
  navigation remain incomplete; preserved routing changes are not committed here.
- Stable alias unchanged; no claim that Samsung received these backend fixes.
- High first reads/process turnover, warm Cashier ~4.4 s, populated device histories,
  authenticated WSS/reconnect and continuous-use failure diagnosis remain open.

**REAL-WORLD PERFORMANCE: PARTIAL.** Two proven source defects fixed and tested,
bounded runtime reads recorded, no fabricated end-user speedup or full stability PASS.

## K. First authenticated request and invoice stability — 2026-10-11

### Preview provenance and measurement boundaries

- Diagnostic Preview: `dpl_95hNudshZN6ZHBTHJZqZG2Uy62oh`,
  `https://viettable-kud4i8fl9-hienkhanhvu981-7145.vercel.app`.
- Final candidate: `dpl_2awYCDKUP8SBiHBDC3QQGmDfokKi`,
  `https://viettable-8wtuspd9f-hienkhanhvu981-7145.vercel.app`.
  Vercel inspect confirmed **target=preview, READY**. Stable alias unchanged.
- Preview-only request/instance/sequence/process-age headers and Server-Timing
  expose fixed timing metadata, not SQL, cookies, JWTs or response payloads.
  Module dispatch explicitly links timing while retaining ExecutionContext
  suppression and HttpContext/identity isolation.
- Authentication is split into cookie authentication and live staff validation.
  MVC action/result, query count, SQL duration and connection duration are observed.
  Nested/parallel `*-sum` values overlap and MUST NOT be added as wall-clock phases.
  `app-preheaders` ends before response completion; completion is logged separately.
  Result formatting is not a separately isolated JSON serialization benchmark.
  Context count remained zero in these samples; EF first-model cost is unproven.
- The old REPL verifier retained an earlier deployment in a closure. Those probes
  were excluded from candidate comparisons. The checked-in verifier requires an
  explicit target/context per call and retains curl failure codes/timings while
  discarding raw stderr. Credentials/body stay in memory only, never artifacts.

### First request: what the evidence does and does not prove

On the final candidate, authenticated Chef returned 200 in **13.826579 s** on
instance `d29faaccb3b04e6f888a72b34b8f85a9`, request sequence 1, process age at
entry **7.221 s**. Application preheaders took **5.639 s**, live staff validation
**1.636 s**, cookie authentication **10.6 ms**, connection-duration sum **3.885 s**,
31 SQL commands. Its immediate same-instance repeat took **3.487836 s**, app
**3.103 s**, live validation **214 ms**, 27 commands.

Cashier sequence 1 on `fe2105cff865402d8fb61604751f191d` took **16.275006 s**,
process age **7.930 s**, app **7.288 s**, live validation **1.625 s**, cookie auth
**15.6 ms**, connection sum **2.497 s**, 20 commands. Same-instance repeats were
**3.575665 / 3.521925 s**, with 18 commands and connection sum **0.6 ms**.
Warm connection acquisition is therefore not the dominant Cashier cost.

Health on that instance reports application-listener-ready **7.805 s**. Per-owner
bootstrap phases run concurrently; the 22.3 s schema-validation sum is NOT a
22.3 s startup wall time. On the diagnostic Preview, health/readiness followed by
Chef on the same instance took **10.510 s / 6.839 s** respectively (Chef was the
first authenticated dashboard, not a new process), then **4.061 / 3.821 / 3.813 s**.

Conclusion: process startup plus first-route/live-validation/connection work
accounts for substantial first-request delay. Cookie decoding alone does not.
JIT/model/TLS/pool/server-execution subcosts are not independently isolated, so
the entire 15 seconds cannot honestly be attributed to one of them or to Supabase.
No claim that the high first-request latency has been eliminated.

### Warm API and bounded repeated-read results

All times below are curl HTTP response time, excluding Vercel CLI setup time;
NOT browser render or Samsung data-ready time. Nearest-rank percentiles use all
successful responses, failures remain in the denominator. Small sample p95 is
the maximum, not a production SLO. Staff and customer reads used two bounded
concurrent lanes, not a load test; instance switching is retained, not discarded.

| Final candidate route | n | p50 seconds | p95 seconds | Errors |
|---|---:|---:|---:|---:|
| Chef dashboard, cold/first-route/warm mixed | 4 | 3.487836 | 13.826579 | 0/4 |
| Cashier dashboard, cold/first-route/warm mixed | 4 | 3.575665 | 16.275006 | 0/4 |
| Admin dashboard, cold/first-route/warm mixed | 4 | 1.472445 | 12.101410 | 0/4 |
| Order history, 23 orders | 4 | 2.964849 | 3.219259 | 0/4 |
| Invoice history, 13 invoices | 8 | 0.849133 | 1.089491 | 0/8 |

Every final sample, in execution order (seconds):

- Chef: 6.075112, 13.826579, 3.487836, 3.198296. First two were first
  dashboard use on distinct processes; remaining two revisit those processes.
- Cashier: 3.591445, 16.275006, 3.575665, 3.521925. Second switches to a new
  process; final two are same-process warm. First is first Cashier route on an
  already-running process.
- Admin: 12.101410, 2.259919, 1.472445, 1.341766. First is a new process;
  second is first Admin use on a different, already-running process.
- Orders: 3.219259, 3.054972, 2.964849, 2.916433 (two instances).
- Invoices: 1.089491, 0.874667, 0.847601, 0.811400, 0.849133, 0.835027,
  0.854877, 0.925530. First seven share one instance; last switches to another.
  Every response contained 13 invoices; no retry was used.

Warm Chef still makes **27 commands**, SQL sum ~5.6 s (parallel), app ~2.8–3.1 s.
Warm Cashier makes **18 commands**, SQL sum ~3.9 s, app ~3.1 s. Live staff
validation ~210–215 ms, cookie authentication ~1–3 ms. Individual SQL duration
includes database/network round-trip, not PostgreSQL CPU alone. Runtime iad1 and
database Sydney make distance a plausible contributor, not an independently
measured SQL-engine diagnosis. Dashboard query chains remain a bottleneck.

### Invoice optimization and historical failure

The diagnostic Preview proved three sequential Billing reads: invoices, quotes,
payments. One customer-scoped paged read with left joins now replaces them,
preserving ordering, optional legacy metadata and response mapping. No cross-DB
read or auth relaxation was added. Runtime proves query count **3 → 1**.

| Invoice API | n | p50 | p95 | Failures |
|---|---:|---:|---:|---:|
| Diagnostic Preview, before join | 6 | 1.025494 s | 1.422177 s | 0/6 |
| Final Preview, after join | 8 | 0.849133 s | 1.089491 s | 0/8 |

p50 improves ~17.2%, p95 ~23.4% in these bounded samples. Before samples:
1.422177, 0.994708, 1.085620, 1.025494, 1.118117, 0.980214 s. App duration
falls from ~630–676 ms warm to ~442–450 ms, SQL sum ~612 ms to ~216 ms.
Public Chef history also skips an unused branch-name lookup; internal history
retains it. This does not resolve the whole dashboard latency.

The earlier failed Invoice History request has no retained HTTP/curl status or
exception in the old helper. A bounded invocation-log lookup returned no records.
It was NOT reproduced in 8 earlier baseline + 6 diagnostic + 8 final reads.
This is **22 successful bounded reads**, not proof the historical error is fixed.
Root cause remains **UNVERIFIED**; neither a Supabase failure nor a network timeout
is asserted. The new verifier retains HTTP/curl code, timing, timestamp and
instance/request identifiers for the next failure instead of hiding evidence.

### Actual UI, security and acceptance boundaries

- User logged into the diagnostic Chef browser; real dashboard showed Bình Thạnh,
  one pending Bàn 2 order and the existing item. No business button was clicked.
  Later API re-login of the same QA account caused the old browser's live session
  to be rejected; its cached card plus login-required message is NOT a fresh UI
  acceptance. Final candidate authenticated Chef/Cashier/Admin APIs returned 200.
  Final authenticated Cashier/Admin browser and browser render timings remain
  UNVERIFIED. User-facing Chef browser login needs renewal after QA API login.
- Final wrong-role reads (Cashier → Chef, Chef → Admin) and unauthenticated
  invoice list returned **401**. These are read-route negative checks, NOT full
  branch-mutation acceptance. Five initial probes using an old Chef cookie were
  401; excluded from authenticated dashboard latency and explicitly retained as
  credential rejection, not mislabeled server 5xx.
- Final health **200**, readiness **200**, branches **200**, branch 3 menu **200**.
  A Catalog image-path probe returned **302**, zero response-body bytes; final
  Supabase image-byte delivery was not verified by that probe, so Storage is not
  marked PASS from the redirect alone.
  Recovery without authorization **401**; authorized global cleanup was not run
  because it can expire the protected user session. Durable recovery unit
  regression is separate from new cloud runtime acceptance.
- Authenticated WSS/reconnect on this final candidate and populated Samsung
  History/restore acceptance remain **UNVERIFIED**. Android source/APK unchanged;
  Samsung still uses the unchanged stable alias. No user order/session/payment
  mutation, app-data clear, automatic recovery scheduler or credential reset.
- Consequently stable alias is NOT promoted despite successful API benchmarks.

### Tests and release decision

- Newly executed CloudHost suite: **71/71 PASS**, including linked timing/context
  isolation, parallel counts, customer invoice paging/left joins and Chef history.
- MobileHistoryReadModelTests rerun after invoice change: **4/4 PASS**.
- Benchmark metadata/failure/percentile Node tests: **4/4 PASS**.
- Preview remote build/publish: PASS (CloudHost and three staff frontends).
- Android unchanged: previous LOCAL/CLOUD_PREVIEW builds, 54 unit and 3 emulator
  tests reused; no new Samsung populated-history acceptance is claimed.
- Secret scan and diff check run before commit.
  Secret scan result: **17 candidate text files / 12 DPAPI values PASS**; no JWT
  match. Node benchmark tests were rerun **4/4 PASS** after final measurements.
  `.idea` and the pre-existing
  routing/Gateway changes excluded from this performance commit. No push.

**VIETTABLE REAL-WORLD PERFORMANCE: PARTIAL** — invoice read cost reduced and
diagnostics tested, but first requests remain high, historical failure cause and
final WSS/Recovery/Samsung/browser gates are not fully established. Vercel skills
guided explicit Preview targeting and withholding alias promotion until acceptance.

## L. First authenticated request and per-read investigation — 2026-10-12

Base: `59f1f9a`. Stable alias is unchanged. No user order/payment/session mutation,
app-data clear, recovery scheduler, Production deployment, push or paid scaling.

### Verified causes, not additive guesses

Baseline Chef request `d9e22e4302204c0d9b0648601513e4e0`, instance
`9288b1ac4cf341e0b0ed2e461c08886d`, sequence 1:

- HTTP 14.348261 s; process age at entry 7,329 ms; app preheaders 5,975.2 ms.
- Cookie decode 11.7 ms; live staff validation 1,657.0 ms; total auth 1,673.5 ms.
- SQL count 31, SQL sum 6,683.6 ms, connection sum 3,905.7 ms.
- MVC result sum 81.9 ms (formatting/buffering, NOT isolated serialization).

Diagnostic Preview `dpl_FJq2SsG65RoA8hWKwif898XPYTfz`, target Preview:
`https://viettable-9f8mi4209-hienkhanhvu981-7145.vercel.app`.
Its first Chef request `baef0e35d64f4be0ac35655ad5fac208`, instance
`f8ee846b3cc64dc085792bbf40fe83ba`, sequence 1:

- HTTP 12.238744 s; process age 6,713 ms; app 4,263.6 ms.
- Cookie 10.6 ms; live validation 349.8 ms; auth total 365.1 ms.
- SQL count 30, SQL sum 6,250.1 ms, connection sum 2,561.0 ms.
- Chef history 3,083.9 ms / 11 commands; active orders 3,617.9 ms / 11 commands.
- Result sum 88.3 ms. Context initialization count 0 at request scope.

These are individual cold observations, not equal-size cold cohorts or a cold
latency percentile. The full HTTP time includes startup/edge/network outside the
request scope. Nested action/SQL/connection durations overlap; do NOT add them.
Npgsql connection acquisition includes pool/network/TLS; their individual costs
and PostgreSQL CPU are still not independently measured.

Health on instance `0593c9c5f5bf441f948ea29d00b4ba2b` reports listener ready
7,266.6 ms; five parallel schema-validation durations sum to 21,679.8 ms.
Each owner's bootstrap calls validation before listening. Source inspection did
not find a dashboard calling it again. Mandatory schema checks remain intact;
there is no justified duplicate-validation removal or auth TTL cache.

### Safe changes

- Identity-owned live validation now projects role/branch/version and an `EXISTS`
  revocation check in one statement. Active flag, ordinal role equality, branch,
  expiration and identical auth-version hashing remain enforced every request.
  No credential/session cache was added. Login/logout paths retain their checks.
- Preview telemetry adds fixed-name per-read elapsed/SQL/connection/count totals
  for Chef orders/history, Cashier Orders/Billing/report and invoice history.
  No SQL text, parameters, credentials, cookies or payloads are logged.
- Chef history projects the paged Orders and associated OrderItems in one owner
  query. Branch filtering is before the order page; Chef item filtering still
  filters eligible orders AFTER that page. Empty orders and response fields remain.

Warm diagnostic Chef: app 2,900.8 ms; history 2,650.5 ms, 9 commands,
SQL 1,844.5 ms; active orders 2,309.0 ms, 9 commands, SQL 1,873.7 ms.
Warm Cashier: app 3,004.2 ms; Billing queue 2,705.7 ms, 13 nested commands,
SQL 2,652.7 ms; Orders subread 1,873.1 ms / 9 commands; report 209.7 ms / 1 command.
Billing totals INCLUDE the nested Orders read, not additional independent work.
This locates the warm bottleneck in chained owner reads, not JSON rendering.
Starting Billing's queue API concurrently with its Bills query is unsafe as-is:
OrdersApiClient also upserts snapshots with the same scoped Billing DbContext.
No unsafe parallel EF access or speculative index was introduced.

### Stability protocol and honest failures

All API timings are curl response times, not CLI setup, browser render or Samsung
data-ready time. Retain instance, request ID, HTTP/curl status and UTC timestamps.
Percentiles are nearest-rank successful responses, errors counted separately.

A REPL verifier binding lost its bearer before one cohort: 80 history probes
returned 401 before business SQL (not a reproduced Invoice History exception).
That cohort is retained as VERIFIER ERROR, not included in authenticated latency.
The corrected verifier holds deployment-specific credentials in an object in RAM.
No token was printed, saved or embedded in arguments. Separate authenticated
cohorts are run rather than retrying these responses and hiding the failures.

Baseline populated history: 20 invoices reads, 20 successes, p50 0.857743 s,
p95 1.092180 s; 20 order reads, 20 successes, p50 3.017972 s, p95 3.510497 s.
Earlier this turn, another baseline completed 20/20 each: invoice p50 0.920336 s,
p95 1.200598 s; orders p50 3.020882 s, p95 3.981680 s. That earlier cohort's RAM
samples were lost in a tool-kernel interruption; do not use it for paired inference.
The old historical invoice failure remains UNVERIFIED, not declared fixed.

### Runtime boundaries

- Diagnostic health/readiness 200. Wrong-role Chef/Admin reads and unauthenticated
  invoice/recovery reads reject with 401. No authorized global cleanup was run.
- Supabase Storage actual Catalog image: 302 to Supabase, then 200 image/jpeg,
  123,302 bytes. Redirect alone is not the acceptance.
- Authenticated customer WSS handshake then fresh reconnect succeed on instances
  `1f3d48e101554ad99fe1e7a261ad560f` and `12279f093f5b430da88a728fbe7defb9`.
  No mutation/event was generated: event delivery/fan-out is NOT newly accepted.
  Two earlier missing-bearer attempts were 401, recorded as verifier auth errors.
- User's browser tab remains on older `kud4i8fl9` Preview. Dashboard and populated
  Chef History visibly loaded there; new `9f8mi4209` tab remains Login. This is not
  final-candidate UI/performance acceptance. API QA re-login can invalidate older
  QA cookies: Identity StaffLogin updates UpdatedAt, included in auth-version.
- ADB lists emulator only; Samsung restore/populated histories are BLOCKED by
  device connection. APK/data unchanged, Samsung stable alias unchanged.

### Tests newly run

CloudHost 74/74 PASS, including live version/revocation/active/role/branch tests,
per-read nested timing counts, empty history orders, multiple items and Chef-item
filtering. MobileHistoryReadModelTests rerun: 4/4 PASS. Benchmark metadata/timeout/
percentile suite 4/4 PASS. Secret scan: 13 candidate text files / 12 DPAPI values
PASS; git diff --check PASS. Android unchanged:
previous builds/unit/emulator results are reused, not new Samsung acceptance.

### Final Preview and paired measurements

Final deployment `dpl_rJisdME9DaUh9LJ294uHb8PfxjV1`, READY, verified
`target=preview`:
`https://viettable-g2k4k721z-hienkhanhvu981-7145.vercel.app`.
CLI executed from filtered source context:
`vercel deploy . --target preview --scope hienkhanhvu981-7145 --yes`.
Remote build/publish of CloudHost and all three staff frontends succeeded.

| Dashboard API | Before n | Before p50 / p95 (s) | Final n | Final p50 / p95 (s) | Errors before / after |
|---|---:|---:|---:|---:|---:|
| Chef | 5 | 3.639759 / 14.348261 | 5 | 3.453763 / 5.400702 | 0/5 / 0/5 |
| Cashier | 5 | 3.506332 / 3.764970 | 5 | 3.513057 / 12.955851 | 0/5 / 0/5 |
| Admin | 5 | 1.346270 / 2.207049 | 5 | 1.312245 / 3.270796 | 0/5 / 0/5 |

These equal-size cohorts INCLUDE first-route/process switches. Before Chef had
a new process; final Chef was first route AFTER login startup on the same process.
Final Cashier sample 4 switches to a new process. Thus p95 improvement/regression
is NOT a controlled cold-start treatment effect. Cashier warm has not materially
improved; Chef p50 improvement is modest (~5.1%), not proof a smooth UI.

Final Chef instance `a0e94f8d34f0484a9dd4c58aef11f58f`, all five responses:
5.400702, 3.480455, 3.403510, 3.360982, 3.453763 s. Last four repeat the process:
history count 8 (previously 9), overall count 25 (previously 27), history SQL
1,653–1,679 ms, app 2,775–2,858 ms. Extra connection waits 1,806–1,830 ms summed
over overlapping work remain; the separate pool/TLS wait decomposition is unknown.

Final Cashier: 3.946215, 3.423627, 3.411718, 12.955851, 3.513057 s.
Sample 4: instance `15157a4d48f34508a9a623c6ddd55c6d`, sequence 1, age 7,354 ms,
app 4,404.2 ms; auth 373.7 ms (cookie 10.4 / live 359.0), SQL sum 4,159.9 ms,
connections sum 1,338.7 ms, result sum 109.6 ms. Warm still has 17 commands,
app ~2,939–3,091 ms, Billing queue ~2,723–2,844 ms. No claim cold is solved.

| History API | Before n | Before p50 / p95 (s) | Diagnostic-after n | After p50 / p95 (s) | Errors |
|---|---:|---:|---:|---:|---:|
| Invoices | 20 | 0.857743 / 1.092180 | 20 | 0.871283 / 2.113984 | 0/20 each |
| Orders | 20 | 3.017972 / 3.510497 | 20 | 3.122405 / 3.317888 | 0/20 each |

History-after uses `9f8mi4209`, before uses `8wtuspd9f`. Customer history source
is unchanged by the final Chef-history projection; final-candidate history smoke
is separately recorded, not relabeled as a final 20-read cohort. Before instance
`447d1148e451498f8f129f203d8dda0e`; all after reads instance
`cbae1f15b0674bbba9e75fe7500b6f72`. After first invoice took 9.109301 s on sequence
1 / process age 7,182 ms: app 892.3 ms and SQL 205.9 ms / one query. The 2.113984 s
outlier at sequence 19 had app 405.7 ms, SQL 199.1 ms, no connection wait; delay
was outside the measured application interval, not a slow invoice SQL statement.
All 80 authenticated history responses across these two cohorts were successful;
the original historical failure remains unproven/unfixed, not concealed by retries.

Every history response time (seconds), preserving order, with the instance mapping
above; failures zero and no automatic retry:

- Before invoices: 1.244432, .845481, .845541, .903778, 1.092180, .858417,
  .925810, .857714, .847185, .857743, 1.003353, .851219, .959138, .847954,
  .835538, .917176, .847527, .967446, .839424, .873238.
- Before orders: 3.510497, 3.263037, 3.025523, 3.017972, 4.192194, 3.110938,
  2.954092, 2.947329, 2.957381, 3.083220, 3.056951, 2.941338, 2.955244,
  2.993596, 3.125580, 3.006875, 3.058964, 3.000449, 3.051850, 2.969369.
- After invoices: 9.109301, .853937, .856525, .846411, .868009, .838489,
  .947384, .876614, .974576, 2.113984, .860601, .882333, 1.521275, .977679,
  .839020, .946237, .847818, .925818, .814237, .871283.
- After orders: 3.647001, 3.154683, 3.090401, 3.123925, 3.085312, 3.131188,
  3.071873, 3.248634, 3.119453, 3.110409, 3.242929, 3.053699, 3.317888,
  3.081252, 3.165952, 3.122405, 3.063604, 3.078795, 3.206538, 3.163104.

Final health 200 / readiness 200, listener ready 6,997.4 ms on instance
`586fe4a26959443b9fca20d33f9fed2d`. Final authenticated Chef/Cashier/Admin APIs
5/5 each; wrong-role reads 401. Final invoice/order/menu smoke 200
(1.407233 / 3.187488 / 1.538686 s), invoice payload 13 items. Final WSS handshake
and reconnect both succeed on `a0e94f8d34f0484a9dd4c58aef11f58f`.
This does not assert new event delivery or recovery-cleanup acceptance.

One intermediate Chef benchmark cohort was 5/5 rejected after the user logged
into that QA account on the older Preview. StaffLogin's UpdatedAt/version change
explains invalidation; those 401s are not treated as fast dashboard reads. Final
authenticated cohort was rerun with a fresh legitimate QA cookie before UI handoff.

**VIETTABLE REAL-WORLD PERFORMANCE: PARTIAL**. Auth first-use and Chef history
reads were reduced safely, but first-process and warm dashboard latency remain
material, historical invoice failure is unreproduced, and final staff browser,
Samsung and non-mutating recovery/event-delivery gates are not all accepted.
Stable alias is deliberately NOT changed. No claim of a fixed platform limit:
network/TLS/pool subcomponents need deeper evidence before further changes.
Read-only alias inspection confirms it still resolves to Preview
`dpl_4CiTfSuo4LhKb2fjFJSX4zaWKTgH` (`viettable-o9udbp2p9`), not either new candidate.
Final browser tab was opened on `g2k4k721z` and is still at Chef Login at handoff.

## M. Focused stabilization — 2026-10-12 (acceptance incomplete)

Base: `4d4abf6`. Existing routing/Gateway/test edits and both `.idea` edits
are preserved and are not part of this change. No stable alias promotion.

### Measured critical path on g2k4k721z

Chef process `9073b60f13fc4b9c8b3181d5a22731a9`, request sequence 1:
HTTP 14.090390 s, process age at request 6,942 ms, app-preheaders 5,670.5 ms,
cookie 10.2 ms, live validation 353.7 ms, staff auth 367.7 ms.
Chef history 5,002.0 ms / 15 nested commands / SQL sum 3,183.1 ms /
connection sum 1,300.7 ms; active queue 4,484.8 ms / 16 nested commands /
SQL sum 3,420.2 ms / connection sum 263.4 ms. Whole-request count 39.
Parallel/nested SQL durations are not additive wall time. The residual outside
app-preheaders is not proven to be exclusively application startup or TLS.

The next Chef read on the same instance was 3.380230 s: app 2,873.8 ms,
history 2,620.5 ms / 8 commands, active queue 2,628.1 ms / 9 commands,
overall count 25. It is one warm sample, not a sufficient improvement cohort.

Cashier warm example: HTTP 3.480005 s, app 3,025.8 ms, Billing queue 2,780.3 ms
including Orders 1,929.6 ms. Billing nested count 13 includes Orders count 9;
do not add these counts. Report runs independently (228.1 ms / one command).
Billing first reads active billed IDs, then Orders live queue plus its Billing
snapshot persistence, then Customers batch. Orders reads branch table scope,
orders, items, Catalog live dishes and persists Orders snapshots. Commands in
warm traces average roughly one remote DB roundtrip, not a measured PostgreSQL
engine-only time. Some repeated Cashier reads have connection duration spikes
around 1.3 s; the existing connection measurement cannot distinguish pool wait
from TLS/network. Parallelizing Billing SQL with its OrdersApiClient is unsafe:
that client also writes through the same scoped BillingDbContext.

### Controlled reads, no retry

All samples below target g2k4k721z, not the new candidate.
`groupByProcess` keeps sequence-1 requests, first observed route, repeated
same-instance route, instance switches and failures in separate cohorts.
Missing instance metadata is unclassified. Failed authentication cannot prime
a warm route. An instance switch is not claimed to be a new-process cold start.

| API / cohort | n | p50 s | p95 s | Failed |
|---|---:|---:|---:|---:|
| Chef new-process first | 1 | 14.090390 | 14.090390 | 0 |
| Chef same-process repeated | 1 | 3.380230 | 3.380230 | 0 |
| Cashier same-process repeated | 6 | 3.540547 | 5.135390 | 0 |
| Admin same-process repeated | 6 | 1.334878 | 1.530844 | 0 |
| Invoice history same-process repeated | 15 | 0.914574 | 1.686554 | 0 |
| Invoice history new-process first | 2 | 8.586282 | 8.892026 | 0 |
| Invoice history instance switches | 2 | 0.817648 | 0.962059 | 0 |
| Order history same-process repeated | 11 | 3.087103 | 4.423759 | 0 |
| Order history new-process first | 1 | 11.561667 | 11.561667 | 0 |
| Order history instance switches | 7 | 3.055731 | 3.340781 | 0 |

Invoice 20/20 and Order 20/20 populated reads succeeded. Each also has one
first-observed-route sample, excluded from the warm rows. Instances observed:
`9073b60f13fc4b9c8b3181d5a22731a9`, `ce7e8731d3384b57ae25699bbc322b74`,
`4896118dbe3643339affa66616d17a39`, `a80cbd3c964242e08ed4fe2e2a700c25`,
and Orders additionally `ef0c33bae476447da10a7bc69241a92a`.
The earlier Invoice failure is not reproduced or claimed fixed.
Five Chef probes returned 401 after the user's legitimate Chef login changed
the live auth version; retained as auth failures, never counted as fast reads.

### Minimal change and verification

Chef active queue and Orders' Cashier queue project each order's items in the
same AsSingleQuery owner read, removing one sequential order-items read from
each route. Branch/status predicates, empty orders, item ordering, item status,
prices, session grouping and live Catalog authority are preserved. No private
cache, schema-check removal, auth bypass or shared-DbContext parallel work.
Fixture regression verifies empty orders, branch exclusion, item order/status,
Cashier amounts/quantities. CloudHost 75/75 PASS, history contract 4/4 PASS,
benchmark helper 6/6 PASS, git diff --check PASS. InMemory fixture tests alone
do not prove Npgsql translation or faster deployed requests.

### Runtime gates

- Chef UI login/dashboard/History: observed on g2k4k721z, Bình Thạnh,
  populated History and item summaries. Read-only navigation, no business action.
- Cashier/Admin browser UI: not yet observed in this round; API is not UI proof.
- Samsung: ADB lists emulator-5554 only, blocked by physical-device connection.
  App data, account and URL unchanged; no reverse tunnel created.
- Correct-route WSS handshake/reconnect: 2/2 on
  `a80cbd3c964242e08ed4fe2e2a700c25`, 1,263 / 1,073 ms. Event delivery and
  multi-instance fan-out remain UNVERIFIED. Two initial verifier probes used
  the wrong hub path and got 404; not a server regression.
- QA customer has no active session (404). Reads of protected session #20 and
  order #38 with that different customer token return 403. Neither was mutated.
- Recovery endpoint always invokes includeIdleCleanup=true, without a fixture
  scope. It was not invoked: global cleanup could affect the protected session.
- Storage: new candidate menu 200, image redirect 302 to Supabase, public asset
  200 image/jpeg, 123,302 bytes. No secret or temporary asset persisted.

Preview candidate `dpl_7MxW7oBh8H1DCYmPSefcYX7Hf8fE`,
`https://viettable-k8j32aegw-hienkhanhvu981-7145.vercel.app`, explicitly inspected
as target=preview and subsequently READY. CLI returned exit 1 while the server
was still BUILDING; later server inspection and actual runtime requests confirm
deployment success. Health/readiness both 200. Stable alias was not promoted.

### Deployed projection and comparable warm cohorts

Npgsql runtime translation succeeded (both dashboard APIs 200). Chef active
queue commands decreased 9 to 8, overall Dashboard 25 to 24. Cashier Orders
commands decreased 9 to 8, nested Billing 13 to 12, overall 17 to 16 on ordinary
warm reads. Occasional snapshot refresh still adds two commands. Nested counts
must not be added together. No business transition was performed.

Controlled read-only baseline recheck and candidate, six successful repeated
same-process reads each, no retries; first-observed-route reads excluded:

| API | Before n | Before p50 / p95 s | After n | After p50 / p95 s | Failures |
|---|---:|---:|---:|---:|---:|
| Chef | 6 | 3.455059 / 3.531444 | 6 | 3.461527 / 3.801705 | 0 / 0 |
| Cashier | 6 | 3.457076 / 3.539001 | 6 | 3.276659 / 3.920328 | 0 / 0 |
| Admin (unchanged route) | 6 | 1.334878 / 1.530844 | 6 | 1.363326 / 1.603643 | 0 / 0 |

Chef/Cashier recheck baseline instance:
`4f3dac0f5db5476dbaba7b9a83c308b1`; candidate instance:
`dfb0ce919fa94e68acaf2cdbd360affb`. These sequential small cohorts use the
same QA identities/data but different processes; not a randomized causal study.
Cashier p50 improves about 0.180 s (5.2%), not p95. Chef median is unchanged:
History remains approximately 2.54–2.76 s, SQL sum 1.65–1.68 s and connection
sum 0.67–0.85 s. Active queue is approximately 2.35–2.56 s. Warm Cashier
critical Billing chain is approximately 2.54–2.56 s including Orders 1.69 s,
with SQL sum approximately 2.51–2.52 s. Remote sequential roundtrips remain.

Candidate sequence-1 Cashier request: 13.698746 s HTTP, app-preheaders 5.4156 s,
cookie 10.1 ms, live validation 367.2 ms, staff auth 381.3 ms; SQL count 18,
sum 3.8593 s, connection sum 1.2702 s. Listener-ready telemetry 6.6129 s.
Five module bootstrap checks run concurrently; their durations cannot be summed
as wall time. Migration-check I/O sum 20.6505 s across modules, schema validation
sum 20.7023 s. Catalog provider initialization 832 ms is before listener ready.
The remaining HTTP interval is not fully attributed. This is Cashier, whereas
the earlier sequence-1 sample is Chef: not a paired cold improvement claim.
Mandatory checks remain; first-request latency is not solved.

New candidate WSS connects and reconnects 2/2 (2,869 / 9,554 ms) on instances
`dfb0ce919fa94e68acaf2cdbd360affb` and
`be244387e177492dbab31a96fdb86ca9`. Distinct reconnect instances are not proof
of event fan-out. Staff hub accepts Cashier QA branch 3 subscription and rejects
branch 2 with Forbidden branch subscription. Chef-to-Admin, Chef-to-Cashier,
Cashier-to-Chef protected API reads and unauthenticated recovery each return 401.
Event delivery and isolated authenticated recovery remain UNVERIFIED.

Cashier/Admin browser acceptance remains pending safe credential handoff; the
user does not have Cashier credentials. Legitimate QA API login succeeded, but
is not UI proof. Samsung is still disconnected. No user account/session data
was cleared; protected session #20/order #38 were not changed.

Secret scan of these five changed files: zero QA-secret matches and zero JWT
patterns. CloudHost 75/75, history contracts 4/4, benchmark helper 6/6 are new
test results. Android builds/unit results are baseline reused, not Samsung proof.

**VIETTABLE REAL-WORLD PERFORMANCE: PARTIAL**. Deployed projection and grouped
measurements are verified; substantial first/warm latency, final Cashier/Admin
UI, Samsung, actual WSS events and isolated recovery acceptance remain open.
Stable alias remains unchanged. No Production deployment or Git push.

## N. Final validation on existing Preview — 2026-10-12

Base `15799ed`; no application source change or deployment in this round.
Candidate remains `dpl_7MxW7oBh8H1DCYmPSefcYX7Hf8fE`,
`https://viettable-k8j32aegw-hienkhanhvu981-7145.vercel.app`.
Stable comparison uses `https://viettable-staging-hienkhanhvu981.vercel.app`.
Health 200 (8.752099 s), readiness 200 (2.383921 s), menu 200 (2.097715 s).

### Actual browser acceptance, not shell/API substitution

Candidate shared staff login is visibly rendered. Chef on the older g2k4k721z
tab initially retains populated History but displays an authentication error
after the earlier legitimate QA API login changed its auth version. Reload
removes that retained view and redirects to Login. This is not a fresh UI PASS.
Candidate Chef/Cashier/Admin login and usable-dashboard acceptance: BLOCKED
on safe credential handoff. Current browser tools cannot directly read the
DPAPI vault and fill the form without placing plaintext in automation input.
No plaintext credential was embedded in browser automation code, written to a
file or handed through a clipboard/chat. API credentials remain in RAM/stdin.
Minimum prerequisite: user signs in manually on the candidate using the
existing authorized QA credential through their local secure mechanism.
The user chose manual sign-in, not clipboard handoff; at the final observation
the candidate is still on Login. No further API staff re-login is performed
after handoff, to avoid invalidating the user's new browser cookie.

Browser read-only evaluation does not expose Navigation/Resource Timing.
Time to meaningful content, usable dashboard, rendering and navigation latency
therefore remain UNVERIFIED; HTTP shell/asset timings below are not UI timings.
Source inspection finds History uses the full dashboard read and its 4-second
controlled polling interval, including unrelated active queue/menu/ingredients.
The controlled reader prevents overlap, coalesces events and pauses while hidden.
No unsupported claim of zero duplicate browser requests or smoother UI is made.

### Stable versus candidate: bounded reads, no retry

Thirty valid dashboard reads succeeded, 15 per target, five per role. The
stable deployment lacks request-sequence metadata, so those samples remain
UNCLASSIFIED for cold/warm, even when the instance identifier repeats. Do not
interpret the mixed stable p95 as warm p95 or compute a causal improvement.

| Target / role / cohort | n | p50 s | p95 s | Failed |
|---|---:|---:|---:|---:|
| Stable Chef / unclassified | 5 | 7.897283 | 35.981400 | 0 |
| Stable Cashier / unclassified | 5 | 4.623462 | 6.203472 | 0 |
| Stable Admin / unclassified | 5 | 1.626533 | 26.642089 | 0 |
| Candidate Chef / sequence 1 | 1 | 13.011102 | 13.011102 | 0 |
| Candidate Chef / warm same-process repeats | 3 | 3.587792 | 3.613534 | 0 |
| Candidate Chef / instance switch | 1 | 4.883765 | 4.883765 | 0 |
| Candidate Cashier / first observed route | 1 | 3.543556 | 3.543556 | 0 |
| Candidate Cashier / warm same-process repeats | 4 | 3.334457 | 3.626921 | 0 |
| Candidate Admin / sequence 1 | 1 | 10.127820 | 10.127820 | 0 |
| Candidate Admin / warm same-process repeats | 4 | 1.400182 | 2.761509 | 0 |

Stable instances: Chef/Cashier `8afd05392a4741cd8e523786ee35dda4`,
Admin `3d005821e9f249089154799d985b566e`.
Candidate Chef `597b85d21af14b2c969e6e5345114d93` then
`5678d09512c4406b8e9626b2b33df9f3`; Cashier the latter;
Admin `7dd6194dc4a54515a2721d6c921b300a`.
Small descriptive cohorts, not sufficient to establish stable tail latency.
The initial verifier used a nonexistent staff/admin route (ten 404s across
targets); corrected to `/api/gateway/admin/dashboard` and retained separately
as verifier errors, not discarded runtime failures. Two table-discovery probes
also used unsupported routes before the verified Admin table endpoint.

### Real WSS events and reconnect — isolated fixture

Existing QA customer, branch 3, Catalog-available Table 1 (ID 36), new Session
23, Order 41, ServiceRequest 23. Protected Session 20/Order 38 are not used.
Two initial customer sockets receive CONFIRMED/OPEN once each. Socket B is
closed deliberately and reconnected before staff transitions.

Socket A: `5678d09512c4406b8e9626b2b33df9f3`.
Reconnected B and staff mutation publisher:
`9baa2add3a4a45fb8a07a4c74f8a3930`.
Both receive orderUpdated PREPARING/READY and serviceRequestUpdated
ACKNOWLEDGED/COMPLETED exactly once per transition. REST order detail confirms
READY before customer confirms receipt. Distinct simultaneous receiver/publisher
instances prove cross-instance delivery for this bounded fixture, not load or
long-duration stability. Service UpdatedAtUtc-to-local-receipt durations:
OPEN 618 ms; ACKNOWLEDGED A/B 598/595 ms; COMPLETED A/B 533/532 ms.
These include clocks and DB-to-publish time, not pure broker latency.
Bounded invocation-log queries did not yield usable event-ID traces; event-ID
correlation and publish-to-receive timing remain UNVERIFIED. Socket A later
closed after successful event delivery; close cause was not captured by this
verifier. All remaining sockets are explicitly closed; no continuous stability
or automatic reconnect policy claim is made from this short native-client test.

Customer JWT staff mutation: 401. Existing branch-2 Cashier list excludes the
fixture; direct completion by ID returns 403. Canonical request stays COMPLETED.
Different-customer reads of protected Session 20/Order 38 return 403. A separate
different-customer WSS observer was not run, so WSS customer isolation is not
newly accepted solely from those REST checks.

Fixture closed through real customer checkout and QA Cashier ONSITE confirmation:
5,000 VND, Payment 22 SUCCEEDED, Invoice 22 total 5,000 VND / Session 23,
Session 23 CLOSED, Table 36 AVAILABLE. No fake payment state or SQL mutation.
This is API/WSS acceptance, not staff UI or Samsung acceptance.
Final customer-scoped history reads initially returned 401 using an access token
that expired at 02:19:02 UTC (09:19:02 Vietnam); the check was at 02:22:03 UTC.
Normal refresh-token API returned 200. Afterwards Orders history returned 200
in 6.017182 s, 24 rows including Order 41; invoice list returned 200 in 1.006840 s.
Its actual `{items,nextCursor}` envelope contains 14 invoices and Invoice 22
exactly once. Expired-token attempts are retained as auth failures, not a claimed
Invoice transient failure or hidden retry. These single reads are not p50/p95.

Chef-start request sequence 2: 11.983196 s, preheaders 11.5324 s, SQL sum
8.3478 s / 39 commands, connection sum 1.4 ms. Chef-ready sequence 3:
17.980589 s, TTFB 14.038407 s, preheaders 13.5370 s, SQL sum 10.9827 s /
52 commands, connection sum 1.7 ms. These are authenticated mutations, not
Dashboard samples. Source includes a full active-queue branch check, two order
detail reads in ready broadcasts and awaited non-idle recovery after ready.
The ready response completes about 3.942 s after TTFB. These serial reads and
recovery are investigation targets; their individual contribution is not yet
isolated, and recovery was not removed or moved to unsafe fire-and-forget work.

### Routing, cache, Storage and recovery

Candidate preparation copies match both existing uncommitted runtime files:
staff-preview/vercel.json and GatewayModule.cs. Test files are excluded from
deployment input. These three existing edits remain untouched and uncommitted.
Public JS returns immutable max-age=31536000. Repeat Chef JS request is 200,
0.493300 s, x-vercel-cache HIT, x-viettable-static staff, no app instance header:
direct CDN evidence. Public HTML is no-store/no-cache; authenticated API is
no-store/no-cache and protected ownership failures are cache BYPASS. HTML first
probe: Chef 1.716136 s, Cashier 0.408055 s, Admin 0.476638 s; JS first probe:
1.204684/0.829220/0.923717 s respectively. Single HTTP probes, not browser render.
Supabase image redirect 302 then 200 image/jpeg, 123,302 bytes, new test.

Standalone recovery/cleanup remains BLOCKED: the public endpoint always runs
includeIdleCleanup=true with no fixture scope. It is not invoked. The normal
successful QA ready/payment paths run existing non-idle recovery; this is not
an isolated recovery fault-injection or global cleanup acceptance. No unattended
scheduler enabled. Stable alias is NOT promoted.

### Android and tests

Samsung absent; ADB lists emulator-5554 only. Emulator installed CLOUD_PREVIEW
CloudPreviewConnectivityTest: 2/2 PASS (54.06 s suite elapsed), public native
HTTPS and actual ApiClient reads, anonymous auth rejection. No reinstall, app
data clear or reverse tunnel. This does not substitute for Samsung launch,
active-session restore or populated history UI acceptance.
New targeted routing/realtime diagnostics/private-backplane tests: 17/17 PASS.
Previous 75/75 CloudHost, 4/4 history contracts, 6/6 benchmark helper, Android
build/unit and populated history reads are explicitly reused baseline results.

**VIETTABLE REAL-WORLD PERFORMANCE: PARTIAL**. Bounded real event/reconnect and
cross-instance delivery, role/branch protection, CDN and fixture closure are
verified. Real authenticated candidate Staff UI, Samsung, scoped recovery,
event-ID logs and stable tail latency remain incomplete. No source optimization,
redeploy, alias promotion, reset, Production deployment or Git push this round.

## 2026-10-12 — final Q1 validation, no speculative optimization

Evidence: [Q1_CLOUD_E2E_ACCEPTANCE.md](Q1_CLOUD_E2E_ACCEPTANCE.md), final-validation
section. Candidate `k8j32aegw`, READY/preview; stable alias unchanged.

| Real observation | n | p50 | nearest-rank p95 | Failures / limits |
|---|---:|---:|---:|---|
| Cashier candidate reload -> tables usable (browser) | 3 | 12.177s | 12.836s | 0/3; process class unmeasured |
| Samsung stable cold launch -> cached real Home categories/dishes | 3 | 5.094s | 5.208s | 0/3; ADB observation overhead included |
| Samsung stable Activity launch only | 3 | 1.421s | 1.442s | Not whole-screen readiness |
| Samsung stable active card from cold launch | 3 | Unavailable | Unavailable | Not observed by 17.184–17.678s; later card appeared |
| Candidate native connectivity on emulator | 2 tests | N/A | N/A | 2/2 FAIL: Vercel Authentication 401 |
| Stable invoice API, same-instance repeats | 2 | 1.408024s | 1.476473s | 0/2; sequence absent |
| Stable Order History API, same-instance repeats | 2 | 3.388319s | 3.455104s | 0/2; sequence absent |
| Candidate Order History API, warm same-process | 1 | 3.646636s | 3.646636s | 0/1; insufficient percentile evidence |
| Candidate Order History API, new-process sequence 1 | 1 | 14.239192s | 14.239192s | 0/1; no cold distribution claim |

Three invoice and three Order History reads on each origin succeeded, exact QA
invoice 23/10,000 VND and orders 42/43 preserved: 12/12, error rate 0 in this small
sample. Candidate invoice samples span different instances, 1.231292/1.728532/
13.119496s; do not pool as warm. Last sample sequence 5: SQL 210.2ms, application
preheaders 333.6ms, HTTP total 13.119496s. The unmeasured residual is substantial,
but platform/network attribution is **not proven**. Stable first observed QA login
27.573458s is an API observation, not browser Login-to-Dashboard timing.

Old Chef/Admin browser measurements remain historical baseline; no matched
stable/candidate authenticated UI benchmark or defensible improvement percentage.
Cashier 3.522/3.636/3.489s observations still showing loading were excluded from
usable timings. Request duplication and frame-accurate meaningful paint unmeasured.
No SQL/cache/index/timeout/business-runtime change was made to chase these samples.

Native candidate blocker is established before application auth: ordinary system
TLS health expected 200 but got 401; actual ApiClient reports Protected by Vercel
Authentication. No bypass credential shipped, no Deployment Protection change,
and no Samsung install/logout/data clear. Emulator APK origin was overridden only
by an uncommitted temporary Gradle init artifact. QA native login/history cannot
be accepted on this protected candidate as currently configured.

Read-only Supabase inspection also resolves the protected-state discrepancy:
Session 20 PAYMENT_REQUESTED since 2026-10-10 19:51:18 Vietnam; Order/item 38 READY,
latest READY audit 2026-10-12 00:50:30 Vietnam; 35,000 VND, table 37 OCCUPIED,
no payment/invoice. Android READY -> aggregate IN_PROGRESS explains ĐANG XỬ LÝ.
These are real historical changes, not corrected or overwritten in this round.

Recovery inspection QA24 settled=true, invoiceCount=1, pendingReady=0. Global
manual/fault recovery still BLOCKED because no narrow session scope; no invocation
or unattended scheduler. Negative WSS subscription controls PASS 17/17 including
reconnect; negative cross-customer event delivery still UNVERIFIED. Positive
cross-instance delivery/reconnect and Storage evidence reused from prior round.

New executions: CloudPreparation build, 6 realtime diagnostics tests, 6 benchmark
tests, PowerShell/Node syntax; candidate/test APK build. Native tests 0/2 PASS;
one earlier startup instrumentation crash retained as harness failure, cause
unproven. Existing frontend/Android unit suites reused explicitly.
**FINAL Q1 ACCEPTANCE / PERFORMANCE: PARTIAL. Stable alias not promoted. Push NO.**

## 2026-10-12 — UI critical-path follow-up (candidate protection bypassed only locally)

Cashier candidate, same authenticated browser tab, three reload observations
(from reload to actual Quận 1 table data / payment row):

| Sample | Shell | Dashboard loading | Tables usable | Payment row visible |
|---|---:|---:|---:|---:|
| 1 | 0.520s | 0.874s | 5.378s | 6.278s |
| 2 | 0.372s | 0.756s | 2.006s | 2.951s |
| 3 | 0.415s | 0.865s | 2.100s | 2.946s |

Dashboard p50 2.100s/p95 5.378s; payment p50 2.951s/p95 6.278s,
0/3 observed failures. Earlier candidate p50 12.177s/p95 12.836s was a
different warm/process cohort, **not a controlled before/after**. Source shows
`getSession` gates the Dashboard route, then Dashboard fetch runs; payment and
service panels load independently. Browser `PerformanceResourceTiming`/network
waterfall was not available through the permitted read-only UI surface, so these
milestones cannot apportion all backend versus JS time. No Cashier source was
changed; do not claim a Cashier optimization or stable-alias readiness.

Android Home on the isolated Emulator, existing cached 10 categories/52 dishes:
instrumented `setupViews()` was called 9 times during one cold-launch window,
totalling about **355 ms** of main-thread work (18–88 ms per call) before the
targeted change. The three food adapters and category chips were rebuilt even
for session/profile notifications. The same fixture after preserving adapters
and rebuilding only when Catalog objects change made 10 calls totalling about
**40 ms** (0–16 ms per call). This is evidence of reduced main-thread jank,
not a comparable whole-app-load benchmark: process, network and serverless
conditions differed. One cold Activity launch was 4.478s before and 2.882s
after, but is **not** attributed to this fix without matched samples. Native
adapter-stability instrumentation 1/1 PASS; debug and CLOUD_PREVIEW builds plus
54 unit tests PASS. No source change to server-side session retrieval or table
metadata. Safe debug telemetry now separates active-session HTTP response,
immediate card state, and order completion, but Samsung measurements could not
be collected because Windows/ADB did not enumerate the phone.

Q1 Emulator authenticated Catalog/Identity and populated completed history
passed through the local test-only bridge (details in
[Q1_CLOUD_E2E_ACCEPTANCE.md](Q1_CLOUD_E2E_ACCEPTANCE.md)). There is no new
Samsung p50, populated active-session restore, candidate/stable paired browser
waterfall, negative cross-customer event-delivery, or scoped recovery result.
The stable alias remains unchanged; performance/release status remains PARTIAL.
