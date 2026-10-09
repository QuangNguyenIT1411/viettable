# Staff Web cloud acceptance — 10/10/2026

## Phạm vi và deployment

Branch `cloud/postgres-migration`, base `41f7958`. Chỉ project hiện hữu `viettable-api`,
target Preview. Không Production, Customer Web, Android URL, Railway, paid upgrade,
reset DB, push hoặc automatic recovery schedule.

- Deployment cuối: `dpl_7dbGYm8vsm6HTDahRT66mdN9fX6q`.
- [Preview](https://viettable-q6sf4c7h9-hienkhanhvu981-7145.vercel.app).
- `vercel inspect --json`: **target preview, readyState READY**; container tại iad1.
- `/healthz`: **200**; `/readyz`: **200**, Supabase databases ready.
- Chỉ thêm Preview Config `CloudHost__ExposeStaffWeb=true`; giữ chín Preview Secrets.
- Production placeholder `dpl_HtbM5wEHbj4HcfQRW4RJCya6JPV5` không đổi.

Lệnh thực thi trong context được tạo bởi `scripts/prepare-staff-preview.ps1`:

```powershell
npx.cmd --yes vercel@62.4.0 link --yes --project viettable-api --scope hienkhanhvu981-7145
npx.cmd --yes vercel@62.4.0 deploy --yes --target preview --project viettable-api
npx.cmd --yes vercel@62.4.0 inspect https://viettable-q6sf4c7h9-hienkhanhvu981-7145.vercel.app --json
```

Context chỉ copy source allowlist. Local env/DPAPI/private keys/cache không upload.
CLI link tạo OIDC tạm trong context riêng; `.vercelignore` loại nó khỏi upload.
Vercel Protection giữ nguyên. Browser verifier dùng bypass cookie ngắn hạn được CLI
đã đăng nhập cấp, chỉ giữ RAM; cookie này không thay Customer JWT/staff authentication.

## URLs và browser thật

| Role | Route cùng Preview origin | Acceptance |
| --- | --- | --- |
| CHEF | `/app/chef/Staff/Account/Login` | HTTPS login200, dashboard branch3, CSS/JS/render PASS |
| CASHIER | `/app/cashier/Staff/Cashier/Index` | Anonymous redirect login; login200, dashboard branch3 PASS |
| ADMIN | `/app/admin/Admin/Dashboard/Index` | Login200, dashboard/menu/tables/deep-link PASS |

Chromium headless thật bằng Playwright, viewport1365×900. Các actor QA có sẵn,
credential DPAPI chỉ đọc vào RAM. Không dùng Admin bypass để làm Chef/Cashier.
Không ghi passwords/JWT/cookies vào source, docs hoặc artifacts.

Nghiệp vụ và CRUD dưới đây chạy trên deployment
`dpl_C6ms1HbJdokdimzAVWBRsqMEkAca`, URL
`https://viettable-k2s0zsjrq-hienkhanhvu981-7145.vercel.app`.
Sau fix ảnh nghiệm thu trên `dpl_82i94YPdWEujakX43mfWDUfMki9W` và deployment cuối;
ba login200/dashboard/routes và production login-help absent được chạy lại trên URL cuối.
Không gọi API staff thay cho UI trong main flow.

## Cross-App E2E

Customer API client chỉ gọi HTTPS CloudHost thật, không localhost/Docker/ADB.
Branch3, table36 (UI Bàn1), session18:

| Step | Bằng chứng thực tế | Result |
| --- | --- | --- |
| Login / QR walk-in | Customer JWT/profile; bảng Catalog thật và bàn trống | PASS |
| Order36 | `ORD-20261009181100-AFD5C1B54FA9485`; 1×Thêm đồ chua, 5.000VND | PASS |
| Chef nhận order | Card đúng bàn/branch, không chỉnh DB | PASS |
| Chef START | UI BẮT ĐẦU NẤU → HTTP200 → PREPARING | PASS |
| Chef READY | UI Hoàn thành → HTTP200 → item READY | PASS |
| Customer realtime | WSS `orderUpdated` PREPARING/READY, REST canonical khớp | PASS |
| Customer nhận món | Own item confirm204 → SERVING | PASS |
| ServiceRequest19 WATER | Cashier card tự xuất hiện đúng bàn | PASS |
| Staff acknowledge | UI Tiếp nhận →200→ACKNOWLEDGED | PASS |
| Staff complete | UI Hoàn tất →200→COMPLETED | PASS |
| Request realtime | Customer WSS OPEN/ACKNOWLEDGED/COMPLETED đúng một lần mỗi state | PASS |
| ONSITE payment18 | `PAY-20261009181324-DBACA080F4E84C4AAE528`, PENDING, 5.000VND | PASS |
| Cashier confirm | UI Xác nhận đã thu tiền → HTTP200 | PASS |
| Canonical payment | SUCCEEDED | PASS |
| Invoice18 | Tổng5.000VND, ID giữ nguyên sau hai recovery passes | PASS |
| Session/table | Session18 CLOSED, table36 physicallyAvailable=true | PASS |

## Admin CRUD và security

Temporary category20 `QA TEMP Staff Preview 8740ad7d`:
blank name bị UI validation; CREATE200; READ đúng;
UPDATE200 description persist sau reload deep-link Edit/20;
Vô hiệu200 rồi DELETE200 qua dialog UI; row biến mất.
Cleanup hoàn tất; canonical 10categories/52dishes không sửa/xóa.

| Check | Runtime |
| --- | --- |
| Cashier branch2 list | Không có request19/payment18 branch3 |
| Cashier branch2 acknowledge / payment confirm | 403 / 403 |
| Customer JWT staff acknowledge | 401 |
| Chef Admin create category | 401 |
| Cashier Chef dashboard | 401 |
| Admin foreign Origin / HTTP Origin mutation | 403 / 403; không tạo entity |
| CloudStaff cookie | Secure, HttpOnly, SameSite=Lax |
| Browser exceptions | Zero uncaught pageerror trong lượt acceptance |

Staff cookies đã được xác minh authenticated trên hai backend instance khác nhau.
Cookie từ deployment nghiệp vụ được chuyển nguyên ciphertext sang hostname deployment
ảnh để kiểm tra stable DataProtection/session: authenticated=true. Đây là kiểm tra
cryptographic redeploy continuity, không phải tự động mang host-only cookie qua domain.
Lượt kiểm tra lại muộn hơn cùng cookie cũ trả authenticated=false; không dùng cookie
cũ đó để khẳng định continuity vô hạn hoặc bỏ qua session expiry.

## Realtime, reconnect và recovery

Cashier browser WSS thật `/hubs/customer-notifications`:
SubscribeBranch[3] invocation completion không lỗi.
Fault test chỉ đóng transport với code1012, không mock frame/server:
socket mới được tạo, SubscribeBranch[3] lại thành công và dashboard REST resync
count7→8. Offline toggle không đóng socket nên không được tính làm reconnect evidence.
Customer business events quan sát đúng một lần/state, không duplicate ở lượt flow.

**Fresh staff deployment multi-instance fan-out: PASS** trên Preview cuối.
Không suy luận từ hai sockets hoặc HTTP requests: từng WSS upgrade trả opaque
`x-viettable-instance`, đối chiếu trace backplane và frame nhận thật. Một session QA19,
order37/item37, request20; các staff mutations đều qua Chromium UI thật.

| Mutation | Instance A | Instance B | Publisher | Số frame A/B/Customer khác |
| --- | --- | --- | --- | --- |
| Chef BẮT ĐẦU NẤU → PREPARING | `c6c450783b764676ba311bac1014a46d` | `4b6ace2ee7d04d759bceb71d89d6dae8` | `68fb441110a848aa8bc536c2cfab675d` | 1/1/0 |
| Chef Hoàn thành → READY | `3f4f4365c6264883a54fb241cecf20d9` | `caa9c8dc73544adda0070aca205e76e5` | `890a184434914961b95200c0a1b90601` | 1/1/0 |
| Cashier Tiếp nhận → ACKNOWLEDGED | `3f4f4365c6264883a54fb241cecf20d9` | `caa9c8dc73544adda0070aca205e76e5` | `549d5de0240c4f2aab96fd371c0397e5` | 1/1/0 |
| Cashier Hoàn tất → COMPLETED, sau reconnect A | `eca87395a23d4c088ce71171d391c7ad` | `caa9c8dc73544adda0070aca205e76e5` | `3bfcdea6297c417982942b9916288f64` | 1/1/0 |

Trace an toàn (UTC, ngày09/10; giờ Việt Nam là ngày10/10):

| Event/state | Backplane eventId | Mutation correlation ID | Publish / broker accepted | Client receive |
| --- | --- | --- | --- | --- |
| orderUpdated PREPARING | `4d8d34bf3bd148a18fa49ae6815b684f` | `3d7d51c64b5a4538a8e1de4017277d66` | 19:12:28 / 19:12:29 | đúng một frame mỗi socket |
| orderUpdated READY | `854d4408b46948a8beddda9057790e5f` | `cc40e566f96b49c8a6fa231cf72f7ce1` | bridge dispatch19:18:18 | A19:18:18.976, B19:18:18.970 |
| serviceRequestUpdated ACKNOWLEDGED | `c732709018e94160a7ae4ed6a9fd6403` | `720818909ea04497b770d16c56915ddd` | 19:19:26 / 19:19:27 | B19:19:27.463, A cũng1 frame |
| serviceRequestUpdated COMPLETED | `559e9ab5830d4a30a3ad1b0be712e4f4` | `020e9127ae214a14a1a56bb3aeb4b735` | 19:20:20 / 19:20:21 | A/B19:20:21.359 |

EventId là private backplane envelope ID, không tự thêm ID vào public payload.
Vercel logs có thể lặp cùng dòng trong nhiều request records; đó không phải client
duplicate. Đếm frame theo entity/status trên socket thật cho kết quả1/1/0.
Customer khác không nhận sự kiện dù một lượt cùng backend instance với socket B.
Cashier branch2 mutate request20 branch3 trả403; request vẫn COMPLETED.
Customer khác đọc order37 trả403, Customer gọi staff complete trả401.
Staff branch2 SubscribeBranch[3] invocation bị reject.

Reconnect: đóng A rồi mở lại, handshake và dispatch barrier hoàn tất; REST resync
order37 READY/request20 ACKNOWLEDGED đúng canonical, sự kiện COMPLETED mới nhận1 lần.
Mobile barrier dùng unknown-method invocation completion sau OnConnected, không
coi lỗi method đó là subscription thành công và không thêm endpoint QA vào backend.
Subscription thực tế được chứng minh bằng trace group customer + delivery.
Các sockets bổ sung bị giới hạn; không forced scaling, load test hoặc paid upgrade.

Nguyên nhân timeout/reset đã reproduce: verifier tìm Logout như button trong khi
menu tài khoản là link; và waitForResponse rejection chưa catch trong lúc locator
auto-wait gây kernel reset. Một acknowledge vượt budget25s của harness nhưng UI
cuối cùng nhận200, REST ACKNOWLEDGED và cả hai sockets nhận event; không click lại.
Payment waiter còn dùng sai `/payments/19/` thay vì `/onsite-payments/19/confirm`,
nên timeout dù thao tác UI đã thành công. Sau reset đọc canonical, không mutation lặp.
Một cặp WSS kết thúc1006 sau lượt nhận event; không có close reason để kết luận
do Vercel lifecycle. Cold-start/network latency là khả năng, chưa chứng minh là
broker fault. Không rewrite backplane; reconnect/event test ở trên đã PASS.

Cashier browser transport reconnect và REST resync ở đầu mục này dùng baseline
UI thực tế của lượt trước; lượt bổ sung mới chứng minh Customer WSS reconnect.

Recovery thủ công POST `/api/internal/recovery` với secret chỉ trong RAM:
hai passes200/200, invoice18 giữ nguyên, không schedule/cron mới.
Fault/redeploy durable recovery sâu dùng baseline8de8b67, không chạy lại injection.

## Storage fix và production bundle

Vấn đề runtime: Catalog giữ portable `/images/...` nhưng Staff container không chứa
canonical assets, Admin menu trả404. Không sửa image URLs trong DB hoặc dựng ảnh giả.

Đã publish55 assets reviewed (52dishes+3branches, tổng9.317.031bytes), không overwrite,
tới public bucket `viettable-staging-media`, namespace `canonical-v1/images/...`.
Public bytes SHA-256 match source **55/55**. CloudHost chỉ GET/HEAD safe image paths
redirect tới HTTPS Supabase public object. Không đưa service key xuống browser.
Admin menu10/10 ảnh visible complete/naturalWidth>0 trên deployment ảnh và deployment cuối.
Canonical assets này là dependency runtime, không xóa như temporary QA entity.

Login help demo được giới hạn `import.meta.env.DEV`; Vite production loại khỏi JS.
Không đổi auth logic. Ba `vite-env.d.ts` khai báo type Vite để tsc build đúng.
Production bundle tests3/3 kiểm tra không có demo login help/backend key pattern;
ba màn login production đều không có `.login-help`.

Vercel từ chối entrypoint basename `Dockerfile.staff-preview`; prep script chỉ đổi tên
trong isolated context sang `Dockerfile`/`Dockerfile.dockerignore`, giữ Dockerfile API gốc.
Một deploy kiểm tra thất bại TS2339 trước khi thêm Vite types; deploy cuối đã build PASS.

## Tests và limitations

- CloudHost **48/48 PASS** chạy lượt này (thêm staff logout replay regression).
- Backend contracts **141 PASS,1existing skip,0failure**, chạy lượt này.
- Deploy cuối: Chef/Cashier/Admin `npm ci` + `npm run build` PASS;
  Cashier reconnect6/6 và production bundle3/3 PASS; .NET Release publish PASS.
- Secret safety: DPAPI11 giá trị checked, không JWT/key match; chạy lại trước handoff.
- `git diff --check` phải PASS trước handoff. `.idea` hash không đổi.
- Dependency advisory baseline8/9/8 tổng không được tự upgrade hàng loạt;
  xem scope browser/build-tool trong preparation document.
- Customer Web không deploy. Admin QR `/Menu/FromQr` không được nghiệm thu và vẫn
  known limitation; QR renderer external và bank QR không phải external payment acceptance.
- Vercel generated Preview có Deployment Protection; user cần đăng nhập Vercel hợp lệ.
- Không Android/Samsung acceptance trong task staff-cloud này.
- Expired staff revocation records chưa có automatic cleanup job; deny check chỉ
  áp dụng ticket chưa hết hạn. Không thêm schedule trong task này.
- Logout chặn các request mới bằng cookie đã revoke. Không nghiệm thu forced
  termination của WSS đã mở trước logout hoặc logout-all-devices.

## Chef logout — bug thật, fix và browser acceptance

UI: `Lịch sử & Tài khoản` (link) → button `Đăng xuất`.
Trước fix, logout200 xóa cookie nhưng replay ticket cũ vẫn dashboard200 và
authenticated=true: stateless cookie không có server-side revocation.

Fix tối thiểu: hash SHA256 toàn ticket (CookieManager tái dựng chunked cookie),
Identity persist hash/expiry vào `identity.CloudStaffRevocations`; kiểm tra trước
password/role/branch-version hiện có. Logout persist trước SignOut/success.
Không lưu raw cookie, đổi auth scheme/lifetime, revoke các phiên độc lập hoặc đọc
database Identity từ domain khác. Additive migration, không reset.

Supabase migration PASS (identity5). Runtime đầu tiên trả SQLSTATE42501 vì table
mới chưa được cấp quyền runtime; chạy helper `test-staging-database.ps1 -Action Roles`
hiện có. Năm service-role owner SELECT PASS, foreign SELECT denied4/4 mỗi role.
Không rotate passwords hoặc mở cross-schema grants.

Trên Preview cuối: login200/dashboard branch3; Logout UI200; CloudStaff cookie0;
replay pre-logout cookie dashboard401 và staff/session authenticated=false trên
instance `cb6b6cdaf98b4ddaa51f856ee17e3c4f`. Browser Back → Login; mở/reload dashboard
cũ → Login; login lại200/dashboard hợp lệ. **Chef logout: PASS**.
Cashier và Admin login200/dashboard cũng được đọc lại, zero pageerror.

## Final targeted regression và cleanup

Session19/table36 branch3; order37,1×Thêm đồ chua5.000VND.
Chef PREPARING/READY và Service Request20 ACKNOWLEDGED/COMPLETED qua UI.
Customer confirm own item204 → SERVING, quote25 tổng5.000.
Cashier row đúng `PAY-20261009192805-651E09D1F05D400EB2793`, button
`Xác nhận đã thu tiền` thực tế; verifier response waiter timeout được đối chiếu
canonical, không click lần hai: payment19 SUCCEEDED, session19 CLOSED.
Invoice19 `INV-4394b67271444d51bad1d70facfdb094`, tổng5.000, đúng1 item37.
Issued19:28:24.179691Z, payment completed19:28:23.958024Z.
Không tạo session/order mới để retry; không chỉnh dữ liệu canonical demo.

## Final status

**STAFF WEB CLOUD: PASS — Chef logout và fresh multi-instance fan-out đã nghiệm thu.**
Ba giao diện, regression mới và main cross-role cloud flow đã chạy thật.
Commit tested source/docs, không push và không đưa `.idea` vào commit.
