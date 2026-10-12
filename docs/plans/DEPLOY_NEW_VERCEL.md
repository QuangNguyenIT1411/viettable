# Kế hoạch deploy lên tài khoản Vercel MỚI (Production)

> **Trạng thái (12/10/2026): đã làm tới bước 2.16** (deploy Production + APK `cloudProd`); việc còn lại ở mục 2b.
> Phần dưới là kế hoạch gốc, giữ nguyên.
>
> **Kế hoạch gốc:** Lập 12/10/2026, chỉ đọc. Nhãn: 👤 chủ dự án tự làm · 🔒 cần duyệt từng lần ·
> 🤖 Claude làm (không ghi lên Cloud khi chưa duyệt).
>
> - **Tài khoản CŨ** `hienkhanhvu981-7145` / project `viettable-api`: **không deploy, không xóa, không đổi gì** (dự phòng).
> - **Tài khoản MỚI**: team "VietTable", gói Hobby, chưa có project.
> - **Database**: giữ nguyên Supabase hiện tại (Sydney). **Code**: branch `qa/vt-ho-01`, commit `06c3130` (có `regions: ["syd1"]`).

## 0. ⚠ Rủi ro điều khoản — chủ dự án cần đọc trước

Fair Use Guidelines của Vercel ghi: *"Circumventing or otherwise misusing Vercel's limits or usage guidelines is a violation of our fair
use guidelines."* Tạo tài khoản Hobby thứ hai **để có thêm hạn mức** sau khi tài khoản cũ vượt Active CPU có thể bị xem là lách hạn mức.
Hậu quả có thể là tạm dừng deployment hoặc tài khoản. Ngoài ra, Hobby chỉ cho dùng **phi thương mại**: *"Any method of requesting or
processing payment from visitors"* là thương mại. Hiện VietTable chỉ có dữ liệu thử, nhưng khi có khách thật thì cần Pro.
Quyết định là của chủ dự án; kế hoạch này không đánh giá pháp lý. Nguồn: https://vercel.com/docs/limits/fair-use-guidelines

## 1a. Biến môi trường của project cũ (`vercel env ls`, chỉ tên)

Cả 20 biến hiện **chỉ có ở môi trường Preview**.

| Loại | Tên | Ghi chú |
|---|---|---|
| Secret (Vercel ẩn, **không đọc lại được**) | `ConnectionStrings__CatalogDb`, `…__OrdersDb`, `…__CustomersDb`, `…__IdentityDb`, `…__BillingDb`, `MobileJwt__SigningKey`, `CloudHost__DataProtectionCertificate`, `MediaStorage__Supabase__ServiceKey`, `CRON_SECRET` | Phải lấy lại từ kho DPAPI trên máy |
| Config (xem được trên dashboard) | `Database__Provider`, `Database__ApplyMigrations`, `Database__SeedProfile`, `CloudHost__EnvironmentNamespace`, `CloudHost__ExposeStaffWeb`, `MediaStorage__Provider`, `MediaStorage__Supabase__ProjectUrl`, `MediaStorage__Supabase__Bucket`, `MobileJwt__Issuer`, `MobileJwt__Audience`, `Realtime__Provider` | Chủ dự án tự copy |

## 1b. Cách chuyển biến môi trường an toàn nhất (giá trị không bao giờ hiện trong output của Claude)

**Config (11 biến) — 👤 copy tay trên dashboard:**
1. Tài khoản cũ → project `viettable-api` → **Settings → Environment Variables** → bấm từng biến Config → copy giá trị. **Chỉ xem, không sửa.**
2. Tài khoản mới → project mới → **Settings → Environment Variables → Add**: Key giống hệt, Value vừa copy,
   Environments chỉ tick **Production**, để **không Sensitive**.
3. **Bắt buộc kiểm tra:** `Database__ApplyMigrations` = `false` và `Database__SeedProfile` = `None`. Nếu sai, app có thể sửa schema
   hoặc chèn dữ liệu mẫu vào DB thật.

**Secret (8 biến) — 👤 chạy script trên máy** (script đọc kho DPAPI và chuyển thẳng vào `vercel env add … --sensitive` qua stdin;
chỉ in tên biến và OK/FAILED; giá trị không lên dòng lệnh, không ghi file, không hiện trên màn hình):

| Biến | Lấy từ (kho `%LOCALAPPDATA%\VietTable\staging-secrets`) |
|---|---|
| `ConnectionStrings__<Owner>Db` (5 biến) | `RuntimeRole-<owner>.dpapi` + thông số cố định: host pooler `aws-0-ap-southeast-2.pooler.supabase.com`, port 6543, user `viettable_<owner>.waxzqkuwuwubdkrzxbqu`, TLS VerifyFull với `/staging-ca/supabase-ca.crt`. Định dạng giống `tools/SelfRestaurant.CloudPreparation/CloudWorkflow.cs` `RuntimeConnection` |
| `MobileJwt__SigningKey` | `CloudHostSigningKey.dpapi` *(suy ra từ tên; nếu không khớp với key cũ thì app chỉ phải đăng nhập lại)* |
| `CloudHost__DataProtectionCertificate` | `CloudHostDataProtectionCertificate.dpapi` *(suy ra từ tên; xem rủi ro mục 1c)* |
| `MediaStorage__Supabase__ServiceKey` | `StorageServiceKey.dpapi` |
| `CRON_SECRET` | **Không chuyển.** Thiếu biến này thì endpoint recovery (có dọn dẹp toàn cục) trả 503, tức là bị **tắt**. An toàn hơn |

Script (🤖 viết và đưa anh duyệt trước khi chạy; **không** commit kèm giá trị):

```powershell
# Chạy trong THƯ MỤC UPLOAD đã link với project MỚI (bước 2.6). Tự dừng nếu thư mục đang link project cũ.
param([Parameter(Mandatory)][string]$Scope)
$ErrorActionPreference = 'Stop'
$link = Get-Content .vercel\project.json -Raw | ConvertFrom-Json
if ($link.orgId -eq 'team_0Zou1LjMbkg5R7iSVHybcmz2' -or $link.projectName -eq 'viettable-api') { throw 'STOP: thư mục đang link tài khoản/project CŨ.' }
$dir = "$env:LOCALAPPDATA\VietTable\staging-secrets"
function Read-Dpapi([string]$n) {
  $s = ConvertTo-SecureString (Get-Content (Join-Path $dir "$n.dpapi") -Raw)
  $p = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
  try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($p) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($p); $s.Dispose() }
}
function Add-Secret([string]$name, [string]$value) {
  $psi = [Diagnostics.ProcessStartInfo]::new('cmd.exe', "/c npx.cmd --yes vercel@62.4.0 env add $name production --sensitive --scope $Scope")
  $psi.UseShellExecute = $false; $psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
  $proc = [Diagnostics.Process]::Start($psi); $proc.StandardInput.Write($value); $proc.StandardInput.Close()
  $null = $proc.StandardOutput.ReadToEnd(); $null = $proc.StandardError.ReadToEnd(); $proc.WaitForExit()
  if ($proc.ExitCode -ne 0) { throw "FAILED: $name (exit $($proc.ExitCode))" }; "OK: $name"
}
foreach ($o in 'catalog','orders','customers','identity','billing') {
  $b = [System.Data.Common.DbConnectionStringBuilder]::new()
  $b['Host']='aws-0-ap-southeast-2.pooler.supabase.com'; $b['Port']='6543'; $b['Database']='postgres'
  $b['Username']="viettable_$o.waxzqkuwuwubdkrzxbqu"; $b['Password']=(Read-Dpapi "RuntimeRole-$o")
  $b['SSL Mode']='VerifyFull'; $b['Root Certificate']='/staging-ca/supabase-ca.crt'; $b['Pooling']='true'
  $b['No Reset On Close']='true'; $b['Multiplexing']='false'; $b['Maximum Pool Size']='2'; $b['Minimum Pool Size']='0'
  $b['Max Auto Prepare']='0'; $b['Include Error Detail']='false'; $b['Timeout']='15'; $b['Command Timeout']='30'
  $b['Application Name']="VietTable-Prod-$o"
  Add-Secret ("ConnectionStrings__" + (Get-Culture).TextInfo.ToTitleCase($o) + "Db") $b.ConnectionString
  $b.Clear()
}
Add-Secret 'MobileJwt__SigningKey' (Read-Dpapi 'CloudHostSigningKey')
Add-Secret 'CloudHost__DataProtectionCertificate' (Read-Dpapi 'CloudHostDataProtectionCertificate')
Add-Secret 'MediaStorage__Supabase__ServiceKey' (Read-Dpapi 'StorageServiceKey')
```

*Lưu ý:* connection string trên dựng lại theo định dạng của tool, **không phải bản sao** giá trị trong project cũ (giá trị cũ không
đọc lại được). Sau deploy, `/readyz` = 200 là bằng chứng kết nối được.

## 1c. Biến phụ thuộc tài khoản / URL cũ

Đã đọc code (`CloudHost/Program.cs`, `StaffCookieState.cs`, `GatewayModule.cs`):

- **Không có biến nào chứa tên miền cũ.** Kiểm tra Origin chỉ so với chính host của request, nên domain mới tự hoạt động.
  Không có cấu hình CORS, cookie domain hay URL callback gắn với domain. `ReverseProxy`/`AllowedHosts` không được đặt.
- **`VERCEL_*` và OIDC:** do Vercel tự cấp cho từng project, không chuyển. ⚠ `VERCEL_ENV` sẽ là `production` thay vì `preview`.
  Code rẽ nhánh theo giá trị này: (1) **khởi động 5 module tuần tự** thay vì song song, nên khởi động lạnh chậm hơn;
  (2) tắt header đo hiệu năng `Server-Timing`. Không có tác vụ nền mới (`Runtime:LegacyPersistentWorkers=false` luôn bật).
  - Đề xuất tùy chọn (🔒, sửa code nhỏ + test): cho `ModuleStartup.CanInitializeInParallel` chấp nhận cả `production`.
- **`CloudHost__EnvironmentNamespace`:** dùng làm tên khóa DataProtection và kênh realtime. **Giữ nguyên giá trị cũ** để dùng chung
  khóa trong DB. Rủi ro: nếu `CloudHost__DataProtectionCertificate` mới **khác** chứng chỉ cũ, app có thể không đọc được khóa cũ
  (lỗi đăng nhập staff hoặc lỗi khởi động). Cách xử lý dự phòng (🔒): đặt namespace mới cho project mới (app tạo khóa mới trong DB).
- **`MobileJwt__Issuer` / `__Audience` / `__SigningKey`:** giữ nguyên để token đang lưu trong app vẫn hợp lệ (cùng DB). Nếu khác,
  người dùng chỉ phải đăng nhập lại.
- **Realtime dùng chung:** stable cũ và Production mới cùng namespace và cùng DB, nên sự kiện realtime có thể đi qua lại giữa hai bản.
  Chấp nhận được với dữ liệu thử.

## 1d. Cấu hình project mới

| Mục | Giá trị |
|---|---|
| Tên project | `viettable` (domain Production dự kiến `viettable.vercel.app`; nếu đã có người dùng tên này, Vercel cấp dạng `viettable-<team>.vercel.app`. Biết chính xác sau bước 2.5) |
| Framework / root | Không dùng Git import. Deploy bằng CLI từ **thư mục upload** do `scripts/prepare-staff-preview.ps1` tạo. Framework do `vercel.json` (services) quyết định |
| Dockerfile | `Dockerfile.staff-preview`, được script đổi tên thành `Dockerfile` trong thư mục upload (API + staff SPA), cùng service `staff` tĩnh trong `vercel.json` |
| Region | `syd1` (trong `vercel.json`). Sau deploy xác nhận bằng `vercel inspect` (phần Builds phải hiện `[syd1]`). Nếu không phải `syd1` thì **dừng, không đổi region khác** |
| Deployment Protection | Để mặc định (Standard Protection + Vercel Authentication). Theo tài liệu: *"protects all deployments except production domains"*, nên **domain Production công khai**, còn URL riêng của từng deployment vẫn bị khóa. Không cần ngoại lệ, không đổi cài đặt |
| Biến môi trường | Chỉ đặt cho **Production** |

## 1e. Android

Thêm build type **`cloudProd`**. Giữ nguyên `cloudPreview` (trỏ stable cũ) làm bản dự phòng.

```groovy
// app/build.gradle — đề xuất, chưa sửa. URL điền sau bước 2.5.
def cloudProdApiUrl = 'https://<production-domain-moi>'   // public origin only, no Vercel credential
cloudProd {
    initWith cloudPreview          // giữ timeout 60 s cho khởi động lạnh, HTTPS-only
    matchingFallbacks = ['debug']
    buildConfigField 'String', 'API_BASE_URL', groovy.json.JsonOutput.toJson(cloudProdApiUrl)
}
```

- Lệnh build: `.\gradlew.bat :app:assembleCloudProd` → `app\build\outputs\apk\cloudProd\app-cloudProd.apk`.
- Không dùng `-PVIETTABLE_API_BASE_URL` với bản debug: bản debug có timeout 20 giây, dễ hết giờ khi gặp khởi động lạnh.
- Cùng applicationId nên cài đè lên app hiện tại (giữ dữ liệu). Muốn quay về thì cài lại APK `cloudPreview`. Nếu muốn **2 app song song**
  thì thêm `applicationIdSuffix '.prod'`, nhưng Google Sign-In sẽ không chạy với package mới.
- Kèm unit test kiểm tra URL `cloudProd` là HTTPS và không phải domain cũ. Chạy lại 54 test.

## 1f. Ước tính tiêu thụ và cách test tiết kiệm (ước tính, chưa đo)

- **1 lần deploy:** build tính vào **Build CPU** (khoảng 4–5 phút × 2 vCPU), **không** tính vào Active CPU. Sau deploy, request đầu tiên
  khởi động lạnh: khoảng **5–15 giây CPU**.
- **1 buổi test 1 giờ** (thao tác liên tục app + web): instance Production chỉ tắt sau **5 phút** không có request. Tổng ước tính
  **1–6 phút CPU** (vài lần khởi động lạnh, cộng vài trăm request × khoảng 0,05–0,3 giây CPU). Với 4 giờ/tháng, đủ cho vài chục buổi.
- **Bộ nhớ:** tính suốt thời gian instance còn sống. App giữ kết nối SignalR thì instance sống theo, nên **thoát app/đóng tab khi xong**.
- **Test tiết kiệm:** gom thay đổi rồi mới deploy một lần; test liên tục, tránh nghỉ quá 5 phút; không chạy script benchmark, không dùng
  vòng lặp gọi `/healthz`; xem **Usage** sau mỗi buổi; xóa bớt deployment cũ không dùng (giảm Images Storage).

## 2. Các bước theo thứ tự

| # | Bước | Ai | Chi tiết |
|---|---|---|---|
| 2.0 | Đọc mục 0 (rủi ro điều khoản), quyết định tiếp tục | 👤 | |
| 2.1 | Sao lưu DB (`pg_dump`, `docs/plans/DEPLOY_STABLE_2026-10.md` mục 3) | 🔒 | Code mới dùng chung DB |
| 2.2 | **Đăng nhập Vercel CLI vào tài khoản mới** | 👤 | Xem hướng dẫn bên dưới |
| 2.3 | Gửi tên **team slug** của team VietTable | 👤 | Từ lệnh `teams ls` ở bước 2.2 |
| 2.4 | Kiểm tra code sạch: `git status --short -- SelfRestaurant-master/src SelfRestaurant-master/deploy SelfRestaurant-master/sql SelfRestaurant-master/certs SelfRestaurant-master/Dockerfile.staff-preview*` phải rỗng; HEAD = `06c3130` | 🤖 | |
| 2.5 | Tạo project rỗng: `npx.cmd --yes vercel@62.4.0 project add viettable --scope <slug-moi>` | 🔒 | Chạy ở thư mục **không** link project cũ (ví dụ `%TEMP%`) |
| 2.6 | Chuẩn bị thư mục upload + link với project mới: `powershell -NoProfile -File scripts\prepare-staff-preview.ps1` → `cd <thư mục>` → `npx.cmd --yes vercel@62.4.0 link --yes --project viettable --scope <slug-moi>` → kiểm tra `.vercel\project.json` có `projectName = viettable` và orgId **khác** `team_0Zou1LjMbkg5R7iSVHybcmz2` | 🤖 (link: 🔒) | **Không bao giờ** chạy lệnh Vercel trong `SelfRestaurant-master` (đang link project cũ) |
| 2.7 | Nhập 11 biến Config (mục 1b) | 👤 | Dashboard tài khoản mới |
| 2.8 | Nhập 8 biến Secret bằng script (mục 1b) | 👤 | Script in ra 8 dòng `OK:` |
| 2.9 | Kiểm tra tên biến: `npx.cmd --yes vercel@62.4.0 env ls --scope <slug-moi>` (chỉ tên) — đủ 19 biến, tất cả Production | 🤖 | |
| 2.10 | (Khuyến nghị) build thử image trên máy: `docker build -f Dockerfile -t viettable-prod:prepared .` | 🤖 | Không tốn hạn mức Vercel |
| 2.11 | **Deploy Production**: trong thư mục upload `npx.cmd --yes vercel@62.4.0 deploy . --prod --yes --scope <slug-moi>` | 🔒 | |
| 2.12 | `vercel inspect <url>`: project `viettable`, target `production`, Ready, **region `[syd1]`** | 🤖 | Không phải syd1 thì dừng, báo |
| 2.13 | Đo 3 lần (`curl.exe`, ẩn danh) trên domain Production: `/healthz`, `/readyz`, `/api/mobile/v1/branches`, `/api/mobile/v1/branches/1/menu`; so với số đo stable cũ | 🤖 | Đọc công khai |
| 2.14 | Mở web staff: `https://<domain>/app/admin/login`, `/app/chef/Staff/Account/Login` | 👤 | Mỗi vai trò xong thì đăng xuất |
| 2.15 | Thêm build type `cloudProd` (mục 1e) + test, commit | 🤖 | |
| 2.16 | Build APK `cloudProd` | 🤖 | |
| 2.17 | Cài APK lên Samsung | 🔒 | `adb install -r`; nếu báo lỗi chữ ký thì dừng |
| 2.18 | Test app + web, rồi xem Usage | 👤 | |
| 2.19 | Ghi evidence mới `docs/acceptance/evidence/RUN_<ngày>_NEW_VERCEL.md` | 🤖 | |

### Hướng dẫn bước 2.2 — đăng nhập CLI vào tài khoản mới (👤)

> Sau khi đăng nhập tài khoản mới, máy này **không dùng được tài khoản cũ** qua CLI nữa (cho tới khi đăng nhập lại). Điều này giúp
> tránh deploy nhầm. Tài khoản cũ trên dashboard web không bị ảnh hưởng.

1. Mở **PowerShell**, gõ: `cd D:\Quang\VietTable`
2. Xem đang đăng nhập ai: `npx.cmd --yes vercel@62.4.0 whoami` (hiện `hienkhanhvu981-7145` là tài khoản cũ).
3. **Nếu team VietTable thuộc một tài khoản khác** (email khác):
   - Gõ: `npx.cmd --yes vercel@62.4.0 logout` → Enter.
   - Gõ: `npx.cmd --yes vercel@62.4.0 login` → Enter. Màn hình hiện một **mã** và mở trình duyệt (nếu không tự mở, copy đường link hiện ra).
   - Trên trình duyệt: đăng nhập **tài khoản Vercel mới** → kiểm tra mã giống trên màn hình → bấm **Confirm/Allow**.
   - Quay lại PowerShell, chờ chữ "Success".
4. **Nếu team VietTable nằm trong cùng tài khoản cũ:** không cần logout/login, chỉ cần dùng `--scope <slug-moi>` ở mọi lệnh.
5. Gõ: `npx.cmd --yes vercel@62.4.0 whoami` và `npx.cmd --yes vercel@62.4.0 teams ls` → gửi cho Claude **tên team slug** (không có bí mật).

## 2b. Việc sau (ghi 12/10/2026, CHƯA làm)

Production đã deploy: `dpl_7CqU6PiRdt6Q64vLty2BVFFt8mcN`, domain `https://viettable-beta.vercel.app`, region `syd1`.

| # | Việc | Ai | Chi tiết |
|---|---|---|---|
| 3.1 | **Đo khởi động lạnh thật** | 👤 báo giờ → 🤖 đo | Để yên **> 10 phút** không có request (app thoát, tab web đóng). Đo **1 lần** `/healthz`, `/readyz`, `/api/mobile/v1/branches`, `/api/mobile/v1/branches/1/menu` bằng `curl.exe` ẩn danh. Không chạy vòng lặp |
| 3.2 | **Theo dõi `/readyz` chậm ~7 s** | 🤖 | Lần đo sau deploy: `/readyz` 7,14 s và 7,50 s ở 2 lần đầu, 0,37 s ở lần 3; `/healthz` và menu < 1,2 s. Ghi lại trong lần đo 3.1; nếu vẫn chậm thì đọc code `/readyz` (kiểm tra DB của 5 module) và log Vercel trước khi sửa |
| 3.3 | **Trang chủ ở `/`** | 🤖 sửa code + test, gom vào **lần deploy sau** (🔒) | Hiện `/` trả 404. Thêm trang tĩnh đơn giản có 3 nút: **Admin** → `/app/admin/`, **Bếp** → `/app/chef/`, **Thu ngân** → `/app/cashier/`. Không chứa dữ liệu hay bí mật. **Code xong 12/10/2026, chưa deploy:** `GatewayModule` trả trang HTML tĩnh ở `/` khi không có customer web và staff web đang bật; test trong `StaffPreviewRoutingTests` |

### 2c. Lần deploy gộp tiếp theo (đề xuất, 🔒 chưa duyệt)

Gồm: trang chủ `/` (3.3) + biến `Authentication__Google__ClientId` (đã có trên Production, `docs/plans/GOOGLE_LOGIN.md`) +
sửa lỗi sau test demo 12/10/2026 (thông báo thanh toán, nhãn tiếng Việt, mã đơn ngắn, nhãn bếp). Không có migration DB.
Các bước: `scripts\prepare-staff-preview.ps1` (thư mục upload mới) → link `viet-table/viettable` → xóa `.env.local` nếu CLI tự tạo →
`docker build` thử → `deploy --prod` → `inspect` (syd1) → đo `/healthz`, `/readyz`, `/` → build + cài APK `cloudProd` mới (🔒 riêng).

## 3. Quay lại nếu hỏng

- Project mới hỏng: stable cũ (`viettable-staging-hienkhanhvu981.vercel.app`) **vẫn chạy nguyên**. Cài lại APK `cloudPreview` để app
  quay về stable cũ. Không cần thao tác gì trên tài khoản cũ.
- Production mới có bản deploy trước đó: dùng Instant Rollback trên dashboard (Deployments → bản cũ → **Promote/Rollback**), 🔒.
- DB: không có migration. Chỉ khôi phục từ file sao lưu nếu code mới ghi sai dữ liệu (🔒 riêng).
