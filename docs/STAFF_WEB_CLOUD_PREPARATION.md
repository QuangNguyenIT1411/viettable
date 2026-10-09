# Staff Web — chuẩn bị Preview cùng-origin (10/10/2026)

## Trạng thái và phạm vi

**STAFF WEB DEPLOYMENT: READY — source/config/build preparation, NOT remote UI acceptance.**

Backend đã chốt trong `8de8b67` (11 file), từ base `8b6c078`; đối chiếu deployment
`dpl_2n6LXbAyfocDMgG5DdTid2u6N5wK`. Backend trước đó đã nghiệm thu WSS nhiều
instance và recovery thủ công; không diễn giải thành automatic scheduling/broker-outage PASS.

Lượt này không deploy, tạo project, đổi environment/alias/protection, kích hoạt lịch,
push, sửa Android URL, deploy Customer Web hoặc thay local Docker.
Hai thay đổi `.idea` vẫn ngoài commit.

## Source thực tế

| Web | Framework/lock sau chuẩn bị | Build | API/auth | Realtime hiện có |
|---|---|---|---|---|
| Chef | React 19.2.5, TS 5.9.3, Vite 7.3.2, React Router 7.18.4 | `npm ci` → `npm run build` | `/api/gateway/staff/...`; cookie `credentials: include` | Dashboard polling 4s, cleanup khi unmount; không SignalR client |
| Cashier | Cùng stack; SignalR 10.0.0 | Cùng command; thêm 6 Node lifecycle tests | `/api/gateway/staff/...`, reservation `/api/cashier/...`; cookie | `/hubs/customer-notifications`, `cashierDashboardChanged`; ONSITE/Service Request polling 5s có guard |
| Admin | Cùng React/TS/Vite/Router | `npm ci` → `npm run build` | `/api/gateway/admin/...` và auth staff; cookie, upload FormData | REST; không thêm hub/poller |

Ba project **không phải ASP.NET MVC**; không có `.csproj`, server session hoặc DB
connection phía frontend. MVC-like URL là đường dẫn cũ giữ tương thích. Gateway/CloudHost
host SPA static bằng `MapSpa`, và API/auth nằm trên ASP.NET Core **.NET 8**.

`BrowserRouter`/Vite base: `/app/chef/`, `/app/cashier/`, `/app/admin/`.
`VITE_GATEWAY_URL` chỉ cấu hình proxy **Vite dev**, fallback `http://localhost:5100`;
client dùng URL tương đối. Không cần nhúng generated Vercel URL vào JS.
Vite dev chưa proxy `/hubs`; local full-flow dùng Gateway host bundle, không đổi dev/local config.
Không có browser signing key, JWT credential store hoặc direct database trong ba SPA.
Các URL `*.module.invalid` chỉ là in-process backend dispatch, không phải browser/network target.

Local không thay đổi:

| Web | Vite dev (`npm run dev`) | Local Gateway bundle |
|---|---|---|
| Chef | `http://localhost:5174/app/chef/` | `http://localhost:5100/app/chef/Staff/Account/Login` |
| Cashier | `http://localhost:5175/app/cashier/` | `http://localhost:5100/app/cashier/Staff/Cashier/Index` |
| Admin | `http://localhost:5176/app/admin/` | `http://localhost:5100/app/admin/Admin/Dashboard/Index` |

## Chọn một project, không tạo ba project

**1 project hiện có: `viettable-api`; 0 project mới; 0 Production bootstrap mới.**

`Dockerfile.staff-preview` build ba SPA bằng Node 24 + lockfile, publish CloudHost .NET 8,
copy đúng `wwwroot/{chef,cashier,admin}-app`. Final image chỉ ASP.NET runtime, không Node/npm,
node_modules, build tool hoặc Customer Web. Gateway đã có SPA deep-link/cross-role redirect.

Đường dẫn trên **cùng Preview origin**:

```text
https://<staff-preview-origin>/app/chef/Staff/Account/Login
https://<staff-preview-origin>/app/cashier/Staff/Cashier/Index
https://<staff-preview-origin>/app/admin/Admin/Dashboard/Index
https://<staff-preview-origin>/api/gateway/...
wss://<staff-preview-origin>/hubs/customer-notifications
```

Không dùng CORS cross-origin/third-party cookie. Không split ba domain rồi bypass Gateway.
Một alias staging **Preview-only** có thể được chọn và assign sau phê duyệt; chưa tạo/kiểm tra
availability/assign alias nào. Không dùng hoặc di chuyển Production alias `viettable-api.vercel.app`.
Frontend vẫn hoạt động bằng relative paths kể cả chưa có alias; cookie host-only không theo sang origin mới.

## Cấu hình đã chuẩn bị

- `SelfRestaurant-master/Dockerfile.staff-preview` + Dockerfile-specific `.dockerignore`.
- `SelfRestaurant-master/deploy/staff-preview/{vercel.json,.vercelignore}`: một container
  service `api`, catch-all ingress, allowlist backend + đúng ba SPA.
- `scripts/prepare-staff-preview.ps1`: chỉ tạo context mới trong Temp từ source allowlist,
  không copy `.vercel`, `.env`, `.idea`, secret store, bin/obj/node_modules/dist; không link/deploy.
- API-only `Dockerfile.vercel`, `vercel.json`, `.vercelignore` và local Docker giữ nguyên.

Commands đã chạy và có thể chạy lại để **prepare/build**, không deploy:

```powershell
cd D:\Quang\VietTable
powershell -ExecutionPolicy Bypass -File .\scripts\prepare-staff-preview.ps1
# cd vào context mới được in, không dùng root workspace làm upload context
docker build -f Dockerfile.staff-preview -t viettable-staff-preview:prepared .
```

Trong mỗi frontend, command build vẫn là `npm ci` rồi `npm run build`.
Node 24 cần cho regression helper:

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master\src\Frontend\selfrestaurant-cashier-web
node --experimental-strip-types --test tests/staffRealtime.test.mjs
```

### Preview Config gate trước deploy tiếp theo

Preview hiện có **`CloudHost__ExposeStaffWeb=false`** để giữ API-only. Cần đổi **riêng Preview**
sang `true` khi được duyệt triển khai Staff. Dockerfile đặt default true nhưng environment Vercel
override default; không coi bundle có trong image là đủ để SPA xuất hiện. Không đổi Production.

Giữ `Database__ApplyMigrations=false`, `Database__SeedProfile=None`, provider Postgres,
namespace/certificate và Supabase Storage/private realtime hiện tại. Không startup bootstrap/migrate.

Không cần secret mới phía client. Reuse 9 **Preview** secret names, không kéo giá trị ra file:

```text
ConnectionStrings__CatalogDb
ConnectionStrings__OrdersDb
ConnectionStrings__CustomersDb
ConnectionStrings__IdentityDb
ConnectionStrings__BillingDb
MediaStorage__Supabase__ServiceKey
MobileJwt__SigningKey
CloudHost__DataProtectionCertificate
CRON_SECRET
```

Sau khi được phê duyệt deploy Staff Preview, từ context riêng mới link **project hiện có**, review
upload selection, rồi deploy explicit target Preview; các lệnh dưới đây **chưa chạy**:

```powershell
npx.cmd --yes vercel@62.4.0 link --yes --project viettable-api --scope hienkhanhvu981-7145
npx.cmd --yes vercel@62.4.0 deploy --yes --target preview --project viettable-api
```

Inspect deployment phải `target=preview`, runtime container, READY trước UI acceptance.
Không thêm `--prod`, promote hoặc Production bootstrap. Nếu CLI đòi tạo project/upgrade, dừng.
Không upload thư mục bootstrap/verifier cũ hoặc Temp chứa secret/test outputs.

## Authentication, HTTPS và cold start

Shared login `/api/gateway/staff/auth/login` → cookie `VietTable.CloudStaff`:
HttpOnly, SameSite=Lax, Secure=Always trong Production, host-only, hạn 8 giờ, không sliding.
Role/branch/version được revalidate bằng Identity boundary; backend authorize, không chỉ hide button.
Cloud mode không dùng ISession RAM: session façade từ encrypted cookie claims, Data Protection XML
persist trong Identity-owned schema, protected bằng certificate/namespace ổn định.
Đổi namespace/certificate sẽ làm cookie cũ mất khả năng giải mã; không rotate tùy tiện khi deploy.

**Bug được reproduce trên Preview cũ:** POST login `{}` có HTTPS Origin đúng host trả **403**;
cùng POST không Origin trả **400** model validation. CloudStaff middleware so Origin.scheme=https
với container Request.scheme=http sau TLS edge, nên browser login bị từ chối trước validation.

**Fix source:** Cloud-only Origin policy yêu cầu đúng authority + HTTPS trong non-Development;
Development vẫn match request scheme. Không trust X-Forwarded-Host/Proto tùy ý, không nới CORS,
không tắt CSRF. Reject foreign host/port, cross-site, null/malformed/multiple Origins.
Server/CLI không Origin vẫn chịu cookie/role authorization. Đây là source fix đã test, **chưa redeploy**;
deployment cũ vẫn có bug. `/api/cashier` legacy ngoài Origin guard `/api/gateway` vẫn cần targeted
CSRF audit về sau; không tự tuyên bố toàn bộ API có anti-forgery token.

Cashier WSS skip negotiation, tránh connection token negotiate ở instance khác. Reconnect đăng ký
lại branch rồi REST resync; cleanup stop + disposed guards chặn callback sau unmount. Không thêm
polling vô hạn. Client không tự join nhánh khác; backend subscription check giữ nguyên.
Initial network failure hiển thị lỗi và các panel REST fallback vẫn tồn tại; không claim automatic
initial-start retry ngoài SignalR reconnect sau khi từng connected.

## Build/runtime constraints và security audit

Container Runtime hiện là beta trên mọi plan; HTTP/WSS đi chung service. Preview idle scale-down
30 giây, nên cold-start phải được nghiệm thu; không giữ dashboard mở cả ngày chỉ để warm instance.
Hobby 100 MB source upload và 45 phút build; context/image đã build local, không đồng nghĩa remote
quota/cost PASS. Polling Chef 4s và panel Cashier 5s + open WSS tạo usage thực khi có người dùng.
Không đảm bảo miễn phí tuyệt đối; kiểm tra Usage Dashboard trước rehearsal, không nâng plan.
[Container runtime](https://vercel.com/docs/functions/runtimes/container),
[Vercel limits](https://vercel.com/docs/limits).

Lock React Router nâng **đúng một dependency family**, 7.14.0 → 7.18.4, cùng major;
không `npm audit fix --force`. Server Framework/RSC advisories không phải architecture SPA này,
nhưng client navigation advisory đã được vá.
[Maintainer advisory](https://github.com/remix-run/react-router/security/advisories/GHSA-wrjc-x8rr-h8h6).
Sau vá, Chef/Admin `npm audit --omit=dev` không còn advisory; Cashier còn `ws` transitive Node adapter
của SignalR. Nó không phải browser WebSocket và không nằm trong final .NET image; không gọi audit
toàn workspace sạch. Build-tool advisories còn lại (8/8/9 tổng theo role) chưa auto-upgrade và
không expose Vite dev server trên cloud. Future Node staff hosting cần xử lý `ws` trước.

Admin QR web target còn `/Menu/FromQr` (Customer Web không deploy), nên route đó 404 có chủ đích;
không dùng làm nghiệm thu Customer Web. QR image renderer dùng `api.qrserver.com`; Cashier QR
chuyển khoản dùng `img.vietqr.io` + cấu hình tài khoản bank demo có sẵn, không provider verification.
Cloud Staff nghiệm thu **ONSITE confirm thật**, không tuyên bố external bank payment PASS.

## Test evidence và remote acceptance còn lại

- Backend review baseline chạy lại: CloudHost **21/21**, backend **141 PASS / 1 existing skip**.
- Sau Origin/static-route regression: CloudHost **33/33** (bao gồm Origin matrix và Gateway deep links).
- Backend contract rerun sau source fix: **141 PASS / 1 existing skip**, không failure.
- Gateway routing test dùng đúng ApplicationName/real controller discovery + CloudStaff middleware:
  POST `{}` Origin HTTPS đúng host qua HTTP container đến model validation **400**, foreign/HTTP Origin **403**;
  không gọi Identity DB. Ba SPA deep links 200/no-cache, Customer route 404. Đây là pipeline test,
  không thay cho browser cloud login thật sau redeploy.
- Cashier bind lifecycle/reconnect **6/6**, promises không flaky sleep; chạy host và Docker build.
- Ba SPA build PASS qua `npm ci` lock mới + `npm run build` trong Docker Node 24;
  CloudHost Release publish PASS. Final image gồm ba SPA, public CA, không secret/local config/Customer Web.
- Secret scan mở rộng TS/TSX/MJS/Docker/config; `git diff --check` phải PASS trước commit.
- Vercel CLI 62.4.0 `deploy <prepared-context> --dry --json --target preview --project <existing-project-id>`
  đã chạy: **610 files / 21.414.746 bytes**, framework Other, đúng container service config;
  không upload hoặc tạo deployment. Context không có env/private-key/Customer/cache artifacts.
- Chưa deploy Staff, chưa cloud browser login/logout/role-flow/WSS/cold-start acceptance của bundle mới.

Checklist sau approval deploy (không đánh dấu trước runtime):

- [ ] Verify Preview READY, `/healthz` + `/readyz` 200, ba deep links/assets 200, Customer Web vẫn absent.
- [ ] Browser HTTPS login Chef/Cashier/Admin, đúng role/branch; logout clear cookie, cross-role redirect đúng.
- [ ] Browser POST có Origin đúng host không 403; Origin sai host/scheme + non-role mutations bị chặn.
- [ ] Chef chỉ thấy nhánh của mình, order mới/transition; Cashier Service Request/ONSITE đúng branch.
- [ ] Cashier WSS khác backend instance, reconnect SubscribeBranch + REST resync; wrong branch denied.
- [ ] Admin read/mutation an toàn; readonly browser refresh/hydration đúng.
- [ ] Idle cold-start rồi cookie/session REST vẫn đúng, không login giả, không secret vào bundle/log.
- [ ] Không Production alias thay đổi; không Customer Web/Android URL đổi; không push.

Automatic recovery kế hoạch riêng: [AUTOMATIC_RECOVERY_PLAN.md](AUTOMATIC_RECOVERY_PLAN.md).
