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
