# Kế hoạch deploy code mới lên stable — 10/2026

> **Trạng thái: KẾ HOẠCH, CHƯA THỰC THI.** Lập 12/10/2026. Mọi bước 🔒 phải được chủ dự án duyệt **từng lần**
> (CLAUDE.md Mục 6). Bước 👤 là chủ dự án tự làm. Bước 🤖 là Claude làm, không ghi lên Cloud.

## 1. Hiện trạng đã xác minh

| Mục | Giá trị | Cách xác minh |
|---|---|---|
| Stable alias | `viettable-staging-hienkhanhvu981.vercel.app` → `dpl_4CiTfSuo4LhKb2fjFJSX4zaWKTgH` (`viettable-o9udbp2p9-…`), Preview, tạo 10/10/2026 19:30 | `vercel inspect` (chỉ đọc) |
| Code của stable | **Đúng commit `38f6762`** | Thư mục upload `%TEMP%\VietTable-staff-preview-9b70125b…` (tạo 10/10 19:30) khớp 391/391 file nguồn với `38f6762` (so bằng hash git) |
| Code sẽ deploy | Branch `qa/vt-ho-01` (gồm thay đổi staff-preview routing + sửa dispose) | `git log` |
| Khác biệt `38f6762` → branch | 56 file backend/web; Android thêm `981b6cd`, `103bebb` | `git diff --stat` |

## 2. Migration / schema database (yêu cầu 2c)

**Kết luận: KHÔNG có migration hay thay đổi schema nào cần chạy.**

- Không có file nào trong thư mục `Migrations/` thay đổi; không có `CREATE/ALTER/DROP`, `HasIndex`, `ToTable` mới;
  `sql/` không đổi.
- Các file `*DbBootstrapper.cs` và `PostgresBootstrap.cs` có thay đổi, nhưng chỉ thêm đo thời gian và chạy kiểm tra
  schema song song. Kiểm tra "còn migration chưa chạy thì dừng khởi động" **vẫn giữ nguyên**: nếu schema thiếu thì app
  báo lỗi ngay, không tự sửa DB.
- Không cần biến môi trường mới: các key đọc thêm (`VERCEL_ENV`, `Database:ApplyMigrations`, `Database:Provider`,
  `Database:SeedProfile`) đều đã có sẵn hoặc do Vercel cung cấp.
- Bằng chứng thực tế: candidate (code ≈ `4d4abf6`, gần giống branch) đang chạy **Ready** trên **cùng DB**, health 200.
- Vì vậy rollback chỉ cần trỏ lại alias, không phải khôi phục DB (trừ khi code mới ghi dữ liệu sai).

## 3. Sao lưu DB trước khi deploy (yêu cầu 2d) — 🔒 chủ dự án tự chạy hoặc duyệt

Gói Supabase Free không nên coi là có sẵn bản sao lưu tải về. `pg_dump` **chỉ đọc** DB và ghi ra một file trên máy.
Dùng Docker nên không phải cài thêm gì. File sao lưu nằm **ngoài repo**, không commit.

Thông số kết nối (không bí mật, lấy từ `scripts/bootstrap-staging-q1-admin.ps1`): host `aws-0-ap-southeast-2.pooler.supabase.com`,
port `5432` (session pooler), user `postgres.waxzqkuwuwubdkrzxbqu`, db `postgres`. TLS kiểm tra bằng CA Supabase đã có trên máy.
5 schema của app: `catalog`, `orders`, `customers`, `identity`, `billing`.

```powershell
# Chạy trong PowerShell. Mật khẩu KHÔNG xuất hiện trên dòng lệnh hay trong log.
$dir = 'D:\Quang\VietTable-backups'; New-Item -ItemType Directory -Force $dir | Out-Null
$ca  = "$env:LOCALAPPDATA\VietTable\staging-secrets\supabase-ca.crt"
$conn = 'host=aws-0-ap-southeast-2.pooler.supabase.com port=5432 dbname=postgres user=postgres.waxzqkuwuwubdkrzxbqu sslmode=verify-full sslrootcert=/ca.crt'

# Cách A (đơn giản): pg_dump tự hỏi mật khẩu, anh gõ tay (cần cờ -it).
# Cách B (không phải gõ): lấy mật khẩu từ file DPAPI đã có, chỉ giữ trong biến môi trường của cửa sổ này:
#   $s = ConvertTo-SecureString (Get-Content "$env:LOCALAPPDATA\VietTable\staging-secrets\DatabasePassword.dpapi" -Raw)
#   $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s))
#   (khi đó thêm  -e PGPASSWORD  vào các lệnh docker bên dưới, và cuối cùng chạy: Remove-Item Env:PGPASSWORD)

# 1) Kiểm tra phiên bản server (chỉ đọc). Nếu server mới hơn 17 thì đổi image postgres:17 cho khớp.
docker run --rm -it -v "${ca}:/ca.crt:ro" postgres:17 psql $conn -c "select version();"

# 2) Sao lưu 5 schema (định dạng custom, nén)
$ts = Get-Date -Format 'yyyyMMdd-HHmm'
docker run --rm -it -v "${ca}:/ca.crt:ro" -v "${dir}:/backup" postgres:17 `
  pg_dump $conn --format=custom --schema=catalog --schema=orders --schema=customers --schema=identity --schema=billing `
  --file "/backup/viettable-staging-$ts.dump"

# 3) Kiểm tra file sao lưu đọc được (chỉ đọc file trên máy)
docker run --rm -v "${dir}:/backup" postgres:17 pg_restore --list "/backup/viettable-staging-$ts.dump" | Select-Object -First 15
Get-Item "$dir\viettable-staging-$ts.dump" | Select-Object Name, Length
```

- Nếu không biết mật khẩu DB: **đừng bấm "Reset password" trên Supabase** khi chưa hỏi, vì có thể làm hỏng kết nối của các script/tool.
- Ảnh món (Supabase Storage) không bị deploy đụng tới, nên không cần sao lưu cho lần này.
- **Khôi phục** (`pg_restore --clean`) là thao tác **ghi đè DB**: chỉ dùng khi thật sự cần và phải duyệt riêng. Không nằm trong kế hoạch mặc định.

## 4. Các bước deploy (yêu cầu 2e)

| # | Bước | Ai | Lệnh / cách làm |
|---|---|---|---|
| 0 | Kiểm tra trước | 🤖 | `git status --short -- SelfRestaurant-master/src SelfRestaurant-master/deploy SelfRestaurant-master/sql SelfRestaurant-master/certs SelfRestaurant-master/Dockerfile.staff-preview*` phải **rỗng**. Lý do: script prepare copy cả file chưa commit trong các thư mục này |
| 0b | Ghi lại Vercel Usage (%) | 👤 | vercel.com → Usage. Chỉ số nào ≥ 70% thì hoãn |
| 1 | Sao lưu DB (mục 3) | 🔒/👤 | |
| 2 | Chuẩn bị thư mục upload (chỉ trên máy) | 🤖 | `powershell -NoProfile -File scripts\prepare-staff-preview.ps1` → in ra `PREPARED ONLY: <thư mục>` |
| 2b | (Khuyến nghị) build thử image trên máy | 🤖 | `cd <thư mục>; docker build -f Dockerfile -t viettable-staff-preview:prepared .` |
| 3 | **Deploy Preview** (chưa đổi stable) | 🔒 | `cd <thư mục>; npx.cmd --yes vercel@62.4.0 deploy . --target preview --yes --scope hienkhanhvu981-7145 --project viettable-api`. **Không** dùng `--prod` |
| 4 | Kiểm tra bản mới | 🤖 | `npx.cmd --yes vercel@62.4.0 inspect <URL-mới>`: name `viettable-api`, target `preview`, status `Ready` |
| 4b | (Tùy chọn) gọi thử health của bản mới khi vẫn còn bảo vệ | 🔒 | `npx.cmd --yes vercel@62.4.0 curl /healthz --deployment <URL-mới>`. Lệnh `curl` không nằm trong danh sách chỉ đọc đã duyệt |
| 5 | **Trỏ stable alias sang bản mới** | 🔒 | `npx.cmd --yes vercel@62.4.0 alias set <URL-mới> viettable-staging-hienkhanhvu981.vercel.app --scope hienkhanhvu981-7145` |
| 6 | Kiểm tra sau deploy | 🤖 | (a) `curl.exe -s -o NUL -w "%{http_code} %{time_total}\n" https://viettable-staging-hienkhanhvu981.vercel.app/healthz` → 200, tương tự `/readyz`, `/api/mobile/v1/branches` (200, 3 chi nhánh); (b) `vercel inspect` stable alias → `dpl` mới; (c) candidate không đăng nhập vẫn 401 (không bị đụng) |
| 6b | Đăng nhập web | 👤 | `…/app/admin/login`, `…/app/chef/Staff/Account/Login` (Chef, Cashier). Mỗi vai trò xong thì đăng xuất (dùng chung cookie) |
| 7 | Build APK mới | 🤖 | mục 6 |
| 8 | Cài APK lên Samsung | 🔒 | mục 6 |
| 9 | Test app | 👤 | Mở app → Home có menu → đăng nhập → chọn chi nhánh → lịch sử đơn/hóa đơn → (tùy chọn) gọi món thử |
| 10 | Ghi evidence mới | 🤖 | `docs/acceptance/evidence/RUN_<ngày>_DEPLOY_STABLE.md` |

**Lưu ý:**
- Ngoại lệ protection của stable gắn với **alias**, không gắn với deployment. Lần chuyển alias trước (sang `dpl_4CiTfSuo…`)
  vẫn giữ được truy cập công khai. Nếu sau bước 5 mà stable trả 401 thì **quay lại ngay** (mục 5), không tự thêm lại ngoại lệ.
- `vercel.json` mới có thêm service `staff` (web tĩnh). Đây là thay đổi cấu trúc so với stable. Candidate 12/10 đã build
  bằng đúng file này và đăng nhập staff hoạt động, nhưng vẫn phải kiểm tra kỹ ở bước 6b.
- **Không xóa** deployment `dpl_4CiTfSuo…` (bản 10/10): đây là đường quay lại.

## 5. Quay lại bản 10/10 nếu hỏng — 🔒 (khẩn cấp: duyệt nhanh bằng một câu)

```powershell
npx.cmd --yes vercel@62.4.0 alias set https://viettable-o9udbp2p9-hienkhanhvu981-7145.vercel.app viettable-staging-hienkhanhvu981.vercel.app --scope hienkhanhvu981-7145
```

Có hiệu lực gần như ngay. Kiểm tra lại: `vercel inspect` stable → `dpl_4CiTfSuo4LhKb2fjFJSX4zaWKTgH`, `/healthz` 200.
DB không cần khôi phục vì không có migration. Chỉ khôi phục từ file sao lưu nếu code mới đã ghi dữ liệu sai, và phải duyệt riêng.
APK mới vẫn chạy được với backend 10/10 (thay đổi Android chỉ là tối ưu phía client), nhưng cần kiểm tra lại sau khi quay lại.

## 6. APK

**Cần build lại.** APK trên Samsung cài ngày 11/10 19:47 và thiếu 2 commit Android: `981b6cd` (gộp request, ưu tiên khôi phục
phiên) và `103bebb` (giảm vẽ lại Home). Build type `cloudPreview` đã trỏ sẵn vào stable alias, không cần đổi URL.

```powershell
cd D:\Quang\VietTable
.\gradlew.bat :app:assembleCloudPreview
# File: app\build\outputs\apk\cloudPreview\app-cloudPreview.apk
# Cài đè, GIỮ dữ liệu và đăng nhập (🔒 hỏi trước):
& D:\Quang\Android\platform-tools\adb.exe -s R5CRC14LFYD install -r .\app\build\outputs\apk\cloudPreview\app-cloudPreview.apk
```

Nếu báo `INSTALL_FAILED_UPDATE_INCOMPATIBLE` (khác chữ ký) thì **dừng và hỏi**: muốn cài phải gỡ app, và sẽ mất đăng nhập/phiên trên máy.

## 7. Đánh giá độ "mượt" (yêu cầu 2f) — chỉ đánh giá, chưa làm

Số liệu lấy từ `docs/FULL_SYSTEM_PERFORMANCE_AUDIT.md` và `docs/acceptance/evidence/RUN_2026-10-12.md`.

### 7.1 Nguyên nhân chậm chính

1. **Backend và DB ở hai đầu Trái Đất (quan trọng nhất, sửa được, miễn phí).** Function chạy ở `iad1` (Washington, mặc định
   của Vercel), còn DB Supabase ở Sydney (`ap-southeast-2`). Mỗi câu SQL phải đi Mỹ ↔ Úc. Số đo: dashboard Bếp chạy 30 câu SQL,
   tổng **6,25 giây**, tức khoảng 200 ms mỗi câu. Request khi đã "ấm" vẫn mất 1,4–4,7 giây phần lớn vì lý do này.
   Kiểm tra schema lúc khởi động cũng chịu chung độ trễ đó.
2. **Khởi động lạnh (giới hạn của serverless, cộng thêm cách đang dùng Preview).** Stable là một deployment **Preview**:
   theo tài liệu Vercel, container Preview **tắt sau 30 giây** không có request (Production là 5 phút). Vì vậy gần như mỗi lần
   mở app sau một lúc nghỉ đều gặp khởi động lạnh: lần đo gần nhất khoảng 8–15 giây (trước đây 25–30 giây).
3. **Một màn hình gọi nhiều câu SQL** (dashboard Bếp 30 câu, Cashier 18–20 câu). Khi đã sửa mục 1 thì đây là vấn đề phụ.

### 7.2 Các lựa chọn

| Lựa chọn | Sửa gì | Chi phí | Công sức / rủi ro | Hiệu quả dự kiến |
|---|---|---|---|---|
| **A. Chuyển region function sang `syd1` (Sydney)** | Thêm `"regions": ["syd1"]` vào `deploy/staff-preview/vercel.json` | **0đ** (Hobby được chọn 1 region) | Nhỏ; có thể gộp vào lần deploy này; quay lại bằng alias | **Lớn nhất:** mỗi câu SQL từ khoảng 200 ms còn vài ms; request ấm dự kiến dưới 1 giây; khởi động lạnh ngắn hơn. Cần kiểm chứng: container runtime có tuân theo `regions` không (`vercel inspect` sẽ hiện `[syd1]`) |
| B. Giữ ấm **chỉ trong giờ test** | Một vòng lặp PowerShell trên máy gọi `/healthz` mỗi 20 giây trong lúc test | 0đ, nhưng tốn hạn mức Provisioned Memory (360 GB-giờ/tháng dùng chung) | Nhỏ; không chạy 24/7 vì sẽ vượt hạn mức Hobby | Hết khởi động lạnh trong lúc test |
| C. Chuyển stable sang deployment Production (tắt sau 5 phút) | Thêm biến môi trường cho Production; code đang rẽ nhánh theo `VERCEL_ENV=preview` | 0đ | **Lớn**, đổi cách chạy khởi động; không khuyến nghị lúc này | Ít khởi động lạnh hơn |
| D. Tối ưu code | Gộp câu SQL mỗi màn hình; giảm việc lúc khởi động | 0đ | Vừa; cần đo trước/sau | Vừa (sau khi đã làm A) |
| E. Trả phí | Vercel Pro (20 USD/người/tháng: nhiều hạn mức hơn) hoặc chuyển backend sang máy chủ chạy liên tục gần Sydney/Singapore | Có phí (giá máy chủ ngoài chưa kiểm tra) | Pro: ít việc nhưng vẫn có khởi động lạnh. Chuyển máy chủ: việc lớn | Hết khởi động lạnh (nếu chuyển máy chủ) |

**Khuyến nghị:** làm **A** trước (miễn phí, hiệu quả nhất, gộp được vào lần deploy này nếu chủ dự án duyệt), đo lại, rồi dùng **B**
trong giờ test. Chỉ cân nhắc D/E sau khi có số đo mới.

Nguồn: [Vercel regions](https://vercel.com/docs/functions/configuring-functions/region),
[Container Runtime (tắt sau 30 giây/5 phút)](https://vercel.com/docs/functions/runtimes/container),
[Vercel Hobby](https://vercel.com/docs/plans/hobby).
