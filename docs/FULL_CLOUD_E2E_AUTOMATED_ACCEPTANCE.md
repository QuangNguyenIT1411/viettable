# Autonomous cloud E2E acceptance — 2026-10-10

Status: **AUTOMATED CLOUD E2E: PARTIAL**.

Base: `16682b8`, branch `cloud/postgres-migration`. API/DB acceptance is deliberately separate from
browser UI and Samsung business acceptance. No privileged account was created, credentials guessed,
authentication bypassed, or user session replaced. Credentials stayed in memory from the existing
DPAPI store. No credentials, cookie/JWT, raw logs, screenshots or APK are included here.

## Environment

- Stable origin: https://viettable-staging-hienkhanhvu981.vercel.app
- Preview deployment: `dpl_7dbGYm8vsm6HTDahRT66mdN9fX6q`, project `viettable-api`.
- Vercel ordinary Preview target metadata is null/non-production, not a literal `preview` value.
  Alias maps to the approved deployment; no alias/protection/deployment mutation in this run.
- `/healthz` and `/readyz`: 200, measured approximately 441 ms and 1,602 ms respectively.
- Supabase PostgreSQL: authenticated read-only verification using the pooler with CA validation.
- Storage: `/images/dishes/banh-mi-op-la.jpg` followed HTTPS redirect to actual Supabase Storage, 200.
- Samsung SM-A528B / Android 14: installed CLOUD_PREVIEW targets HTTPS alias, ADB reverse list empty.
- Docker backend was not used for cloud requests. Docker Desktop was already running; Docker OFF is
  not claimed. Official SDK container was used only to run .NET tests because no local SDK was available.

## Scope and preserved user fixture

User fixture preserved: session **20**, branch **3** Bình Thạnh, table ID **37** / table **2**,
order/item **38**, one Bánh mì ốp la, 35,000 VND. Latest read: session ACTIVE, item CONFIRMED,
no payment for that session. No Chef action or checkout was executed against this fixture.

Isolated QA customer already existed; no new customer or staff accounts were created. A read check
established table ID **38** / table **3** was free before walk-in. Fixture:

| Record | Value |
|---|---|
| Branch / table | 3 / 38 (Bình Thạnh, table 3) |
| QR from actual Catalog | `QR-BT-B03` |
| Dining session | 21 |
| Order / item | 39 / 39 |
| Dish | 29, 1 × Bánh mì ốp la |
| Service request | 21, WATER |
| Checkout quote | 26, 35,000 VND |
| Payment | 20, `PAY-20261010043513-17528971EAC541669F08F` |
| Invoice | 20, `INV-b10b935dad5e451fae485ba8841e039b` |

## Scenario results

Counts cover the following 25 acceptance groups, not individual HTTP calls or unit test cases:
**18 PASS, 0 FAIL, 3 BLOCKED, 4 UNVERIFIED**. The WSS row is the final successful retest, not a
claim that earlier timeouts were absent or transport reliability is established.
Previous runs are not substituted for new evidence.

| # | Test | Environment / evidence | Status |
|---|---|---|---|
| 1 | Origin, health/readiness | Approved Preview alias, ordinary HTTPS 200 | PASS |
| 2 | PostgreSQL connectivity | TLS pooler queries, persisted fixture reads | PASS |
| 3 | Storage image | HTTPS Supabase image 200 | PASS |
| 4 | Native Android connectivity | Installed ApiClient and native diagnostic, 2/2 | PASS |
| 5 | Customer authentication API | Login/profile, refresh rotation, logout; revoked refresh 401 | PASS |
| 6 | Catalog / QR API | Branches/menu/layout, real QR; invalid QR 404 `qr_not_found` | PASS |
| 7 | Isolated walk-in API | Free table → session 21, user session 20 unchanged | PASS |
| 8 | Ordering/idempotency | Order 39; same-key replay returns same order | PASS |
| 9 | Chef processing API | CONFIRMED → PREPARING → READY persisted | PASS |
| 10 | Customer received API | Item 39 → SERVING; HTTP 204 | PASS |
| 11 | Service request API | WATER OPEN → ACKNOWLEDGED → COMPLETED; HTTP 200 | PASS |
| 12 | ONSITE payment API | Server quote 35,000; PENDING → correct Cashier confirm → SUCCEEDED | PASS |
| 13 | Invoice/history/close | One invoice, correct item/amount, session CLOSED, table released | PASS |
| 14 | Role and branch enforcement | Forbidden mutations preserve QA payment; results below | PASS |
| 15 | Admin temporary CRUD API | Create/read/update persisted, inactive then delete, absent on reread | PASS |
| 16 | Chef cookie logout API | Login/dashboard 200, logout 200, old cookie 401, relogin 200 | PASS |
| 17 | Manual recovery API | Anonymous 401; authorized POST 200 `processed`, no scheduler enabled | PASS |
| 18 | WSS opening/reconnect | Earlier bounded probes timed out; final fresh negotiated handshake and direct reconnect succeeded | PASS |
| 19 | Chef authenticated browser UI | Login form accessible; no secure UI credential entry completed | BLOCKED |
| 20 | Cashier authenticated browser UI | No authenticated UI payment/service action executed | BLOCKED |
| 21 | Admin authenticated browser UI | API CRUD is not browser CRUD acceptance | BLOCKED |
| 22 | Current SignalR event delivery / fan-out | No captured orderUpdated/serviceRequestUpdated receipt | UNVERIFIED |
| 23 | Full autonomous Samsung business E2E | User's preceding login/QR/order evidence only; no new automated UI flow | UNVERIFIED |
| 24 | Wi-Fi / full cellular business flow | Cellular default snapshot; no Wi-Fi/full business network run | UNVERIFIED |
| 25 | OPEN cancellation / occupied-table / expired-access edge cases | Not executed against a fresh runtime fixture | UNVERIFIED |

## Data consistency and security

Session 21 ended at `2026-10-10T04:36:15.342Z` (11:36:15 Vietnam time). Final payment SUCCEEDED;
invoice count for session 21 = **1**; invoice history contains invoice 20 once. Snapshot contains one
item with Quantity 1, UnitPrice/LineTotal 35,000 and DishName Bánh mì ốp la. Snapshot item field names
are PascalCase; history envelope is `{items,nextCursor}`. No duplicate order/payment from same-key replay.
Order 39 remains in history as COMPLETED; item 39 remains SERVING under current aggregate close rules.
Table 38 layout reports available. Do not incorrectly assert every item becomes COMPLETED.

| Security action | Actual response |
|---|---|
| Customer → kitchen item start | 401 `unauthorized` |
| Customer → staff acknowledge | 401 |
| Customer → confirm ONSITE | 401 `unauthorized` |
| Non-admin → category create | 401 |
| Wrong-branch Cashier → payment confirm | 403 `payment_not_collectible`; payment still PENDING |
| Wrong-branch Cashier → QA request mutation | 403 `forbidden` |
| Other existing QA customer → order 39 detail | 403 `forbidden` |
| Cancel COMPLETED request | 409 `service_request_state_conflict` |
| Anonymous profile / recovery | 401 |

Manual recovery completed in about 27 seconds. A session-scoped outbox query using a lower-camel
payload key returned no rows; that alone is **not** evidence of zero pending work across all payload
formats. Exhaustive pending-work/receipt reconciliation remains unverified. No scheduler was enabled.

## Realtime diagnosis

Negotiation succeeds with real QA JWT. Negotiated upgrade attempts using header and query authentication,
IPv4 and Windows ClientWebSocket timed out before a 101/JSON handshake was captured. No token logged.
One earlier tool-harness deadline also expired while a REST mutation completed on the server; canonical
state was reread before further action, rather than blindly resubmitting.

Subsequent standard **skip-negotiation direct WSS** completed JSON handshake. A fresh reconnect also
completed handshake, with instance `13029dede7f348d5bafa252503a64e44`. The Hub source automatically
joins `mobile-customer:{subject}` on connection; successful handshake does not independently prove group
event receipt. A final fresh negotiate + connection-token WSS retest then succeeded as well (combined
probe approximately 24.7 seconds). Thus negotiated WSS is not permanently broken; intermittent opening
timing remains unresolved. The measured cold responses are relevant, but do not prove the exact cause
of each earlier timeout. No transport change was made to Android or the backplane.

No new event receipt, latency, duplicate detection, rejoin delivery, or Android UI update captured.
**MULTI-INSTANCE: UNVERIFIED**; no two distinct connection instances plus publish correlation proven.
No forced scaling, load test, paid upgrade, redeploy or backplane rewrite.

## Regression and measurement

- Android `assembleDebug`, `assembleCloudPreview`: PASS.
- `testDebugUnitTest --rerun-tasks`: **41/41 PASS** (XML counts checked).
- Samsung `CloudPreviewConnectivityTest`: **2/2 PASS**, 48.777 s. Native health 200 ~769 ms;
  branches 200 ~23.2 s; anonymous profile 401 ~557 ms; empty login 400 ~23.0 s.
  Diagnostic uses an isolated unused token vault; no user authentication replacement.
- CloudHost .NET tests in SDK 8.0 container: **48/48 PASS**.
- Backend contract tests: **141 PASS, 1 SKIP / 142**. Skip is not runtime acceptance.
- Chef/Cashier/Admin `npm run build`: PASS. Cosmetic Cashier Rollup annotation warning not a blocker.
- APK secret scan: PASS against 11 DPAPI values and 2,608 expanded-entry encodings; rerun after docs.
- Warm simple requests ~0.3–6 s; several cloud requests ~23–27 s. WSS event delivery latency unavailable.
- No arbitrary sleep-based test, load test or full user-destructive instrumentation suite added.

Harness corrections are not application bugs: read `serviceRequestId` rather than a guessed `requestId`;
read invoice history envelope and actual PascalCase snapshot keys. Admin DELETE first returned 409
because a category must be inactive; the temporary category was then deactivated and deleted legitimately.
Temporary category 21 is absent on final read. Canonical menu/images unchanged.

## Manual handoff

Authenticated UI acceptance still needs an existing authorized staff login through the normal form;
no password request was sent to the user during this autonomous run. API cookie tests do not establish
browser Logout/Back/Refresh behavior. No secrets or session injection into browser were used.

- Chef: https://viettable-staging-hienkhanhvu981.vercel.app/app/chef/
- Cashier: https://viettable-staging-hienkhanhvu981.vercel.app/app/cashier/
- Admin: https://viettable-staging-hienkhanhvu981.vercel.app/app/admin/
- APK: `D:\Quang\VietTable\app\build\outputs\apk\cloudPreview\app-cloudPreview.apk` (not tracked).
- Existing user session 20: actual QR `QR-BT-B02`, table 2 occupied by that session, not a free-table claim.
- For a new independent test, QR `QR-BT-B03` was real and table 3 released after QA; recheck availability
  immediately before walk-in. QR artifacts remain outside Git.
- Use existing staging Customer/Chef/Cashier/Admin identities; usernames supplied privately in handoff,
  no credentials committed. Local accounts need not exist in staging. Google Preview is not configured.

Manual checklist:

- [ ] Samsung normal password login → branches/menu/images load over Internet without ADB reverse.
- [ ] QR resolves actual branch/table; resume own active session rather than duplicate check-in.
- [ ] Order submit → Chef UI sees correct table/items → BẮT ĐẦU NẤU → Hoàn thành.
- [ ] Without manual refresh Samsung updates READY → Tôi đã nhận món → SERVING.
- [ ] WATER request → Cashier Yêu cầu phục vụ → Tiếp nhận → Hoàn tất; Samsung automatically updates.
- [ ] Customer checkout ONSITE → Cashier confirms correct reference/amount; untouched Samsung shows success.
- [ ] Invoice correct and listed once; session CLOSED/table released; cold launch does not restore closed session.
- [ ] Chef Logout → old dashboard refresh/back cannot perform protected action → normal login works again.
- [ ] Reconnect resync and event delivery proven; fan-out remains unverified until distinct-instance evidence.
- [ ] Repeat Wi-Fi; user may separately choose cellular; do not infer both from one network snapshot.

Final: **AUTOMATED CLOUD E2E: PARTIAL** — actual authenticated staff browser workflows, event delivery,
multi-instance fan-out and full Samsung realtime/payment business acceptance remain incomplete.
Intermittent WSS opening timeout reliability also needs further evidence despite final handshake PASS.

## Remaining Android acceptance follow-up — base ae9cde8

Date: 2026-10-10. **ANDROID CLOUD E2E: PARTIAL**. Earlier API fixture results above are retained
as API evidence, not relabelled Samsung/browser acceptance. Home/Menu evidence from commit ae9cde8
is reused rather than unnecessarily repeating that full run. No new customer, session, order or payment
was created in this follow-up. User authentication, session 20 and order/item 38 were preserved.

### Direct Samsung observations

| Function | Result | Actual evidence / scope |
|---|---|---|
| Home/Menu/categories/images/branch restore | PASS | ae9cde8 device baseline: branch 3, 10 categories/52 dishes; branch change and cold launch accepted |
| Home cold loading | PASS | After install-r launch: "Đang tải danh mục và món ăn…", then branch Bình Thạnh/categories and own active card appeared |
| Search | PASS | Tapped "Nem rán" keyword; one real Nem Rán result, 40,000 VND, branch Bình Thạnh |
| Food detail/image | PASS | Opened Nem Rán: photo, price, description and ingredient list; did not add to cart or change favorite |
| Cart validation / new autonomous order UI | BLOCKED | Avoided changing current user's cart/session; independent QA customer cannot replace current Samsung login |
| Current orders | PASS | Real UI: round 1, order 38, ĐÃ XÁC NHẬN, one Bánh mì ốp la, 35,000 VND, 11:20 Vietnam time |
| Closed-session history empty state | PASS | "Chưa có lịch sử phiên dùng bữa"; this customer has no populated closed-session UI evidence |
| Invoice history empty state | PASS | Reproduced wrong message, fixed layout, install-r, then "Chưa có hóa đơn đã phát hành" in Hóa đơn tab |
| Populated invoice history/detail UI | BLOCKED | Existing user's session unpaid; QA invoice 20 belongs to another customer. Did not switch login or finish session 20 |
| Notification empty state | PASS | Opened real Thông báo: "Bạn chưa có thông báo nào trong mục này."; did not mark read |
| Background/foreground | PASS | Home key and normal activity return preserved notification screen and login; no new crash in current run |
| QR camera | UNVERIFIED | No physical QR camera scan executed in this follow-up; prior QR API success is not camera acceptance |
| Android WSS connection / REST resync | PASS | 12:29:45 connected; 12:30:12 REST resync complete, after several transport-unavailable attempts |
| Android READY / service event delivery | UNVERIFIED | No permitted mutation against user session 20; no matching new orderUpdated/serviceRequestUpdated UI receipt captured |
| Reconnect reliability / duplicates / fan-out | UNVERIFIED | Event delivery not captured; one recovery does not establish transport reliability or distinct-instance fan-out |
| Offline error / retry UI | UNVERIFIED | No network toggle on active cellular service; state tests/layout guard are not offline Samsung acceptance |
| Wi-Fi | UNVERIFIED | Not active during this run |
| Cellular native cloud reads | PASS | Samsung native HTTPS test and UI catalog/detail reads without ADB reverse; this does not accept full cellular business flow |
| Full 4G/5G business E2E | UNVERIFIED | No Samsung checkout/payment flow; network changes left to user |
| Chef / Cashier authenticated browser actions | BLOCKED | No secure normal-form credential entry completed; valid API cookies do not constitute browser acceptance |
| Order/service/payment/invoice API | PASS | Isolated QA session 21 evidence above; not rerun against protected user session |
| Android automatic ONSITE success | UNVERIFIED | Requires an authorized independent UI session or later user-owned UAT; API payment success is insufficient |

### Minimal bug fix and known findings

**Fixed:** Invoice tab was selected but displayed "Chưa có lịch sử phiên dùng bữa". In
`fragment_order_history.xml`, progressHistoryInvoices, ivHistoryEmptyIcon and tvHistoryEmptyMessage
belonged to hidden layoutEmptyOngoing, although OrderHistoryFragment updates them while showing
layoutEmptyPast. Moved those IDs/views into layoutEmptyPast; no API/model/navigation change.
OrderHistoryLayoutTest asserts unique IDs and the correct parent for loading/icon/message/retry.
After install-r Samsung separately rendered correct session-history and invoice empty messages.
Loading/error/retry association has structural regression coverage; actual offline error UI is not claimed.

**MEDIUM, remaining:** Active-session/current-order headings say "Bàn 37" (database TableID), whereas
Catalog's table number is 2. MobileRepository.parseSession builds a display string directly from tableId.
Record identity/data is correct, but table label acceptance is FAIL. This separate display mapping was not
expanded into a backend contract change during the bounded acceptance run.

### Regression evidence and preservation

- Fresh command: `./gradlew.bat :app:assembleDebug :app:assembleCloudPreview :app:testDebugUnitTest`.
  Builds PASS, unit XML totals **46 tests, zero failures/errors** (includes one new structural guard).
- Installed CLOUD_PREVIEW using `adb install -r`, no clear/uninstall/logout/token extraction.
- Fresh Samsung runner: `CloudPreviewConnectivityTest`, **OK (2 tests), 71.760 seconds**.
  It uses an isolated diagnostic vault; does not replace app authentication or create business records.
- Fresh health/readiness HTTP 200: approximately 22.7 / 23.9 seconds. Branches 200, three records.
- Fresh authenticated Cashier/Admin dashboards 200. Previously revoked Chef cookie correctly returned
  401; normal QA API login again returned 200 and dashboard 200. No Chef business action performed.
- Baseline reused, not rerun for XML-only change: CloudHost 48/48; backend 141 PASS + 1 SKIP;
  Chef/Cashier/Admin frontend builds PASS from the prior isolated run above.
- SignalR recovery log timestamps above are device evidence; no token or raw log included. AndroidRuntime
  fatal entries present in the buffer were dated October 8, not a crash from this October 10 run.
- Final read-only PostgreSQL: session 20 ACTIVE, order status CONFIRMED, item 38 CONFIRMED,
  Quantity 1, LineTotal 35,000, PaymentId null. No user fixture mutation.
- No ADB reverse, Docker backend request, redeploy, Production change, scheduler, reset, paid operation or push.
- Final secret scan PASS: four candidate source/docs files, 2,608 APK entry encodings against eleven
  DPAPI values; no secret/JWT match. `git diff --check` PASS. Both pre-existing .idea changes excluded.

### Checklist thủ công còn lại

Không thao tác session 20/order 38 nếu chưa chủ động quyết định tiếp tục UAT của chính mình.
Các bước tạo/gọi món/thanh toán dưới đây dùng một phiên QA riêng khi người dùng cho phép đổi tài khoản.

- [ ] Camera quét QR bàn thật còn trống → đúng chi nhánh/bàn; QR sai → lỗi thật và quét lại được.
- [ ] Thêm 1 món → giỏ đúng giá/số lượng; gửi một lần → Chef UI thấy đúng một order.
- [ ] Chef Web `/app/chef/`: BẮT ĐẦU NẤU → Hoàn thành; Samsung tự READY, không refresh.
- [ ] Samsung "Tôi đã nhận món" → SERVING; món khác không bị đổi trạng thái.
- [ ] Gửi WATER → Cashier Web `/app/cashier/`, Yêu cầu phục vụ → Tiếp nhận → Hoàn tất;
  Samsung tự cập nhật cả hai bước, không refresh.
- [ ] ONSITE → giữ nguyên Samsung ở màn chờ; Cashier xác nhận đúng reference/amount → tự thành công.
- [ ] Hóa đơn đúng món/tổng, chỉ một bản; history/detail mở được; phiên CLOSED và bàn giải phóng.
- [ ] Background/resume hoặc mất mạng/reconnect → REST resync đúng, không duplicate update.
- [ ] Người dùng tự kiểm tra offline → Home có lỗi/Thử lại → phục hồi; chạy riêng Wi-Fi và mạng di động.

Final: **ANDROID CLOUD E2E: PARTIAL** — independent Samsung cart/order/payment UI blocked by
preservation constraint; staff browser actions and event-driven UI not accepted; physical camera,
offline/retry and full network business acceptance unverified; raw table-ID display finding remains.
