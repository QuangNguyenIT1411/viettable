# CLAUDE.md — VietTable

Luôn trả lời chủ dự án bằng tiếng Việt, dễ hiểu.

> File này được Claude Code đọc tự động mỗi phiên. Giữ ngắn gọn; chi tiết nằm trong `docs/`.
> Các mục ghi `TODO:` là thông tin chủ dự án cần điền. Nếu gặp `TODO:` liên quan tới việc đang làm, **hỏi chủ dự án, không tự đoán**.

## 1. Dự án là gì

VietTable là hệ thống gọi món / quản lý bàn cho nhà hàng nhiều chi nhánh (ví dụ: Quận 1, Bình Thạnh).

| Thành phần | Người dùng | Vai trò |
|---|---|---|
| Android Customer | Khách tại bàn | Mở phiên bàn, gọi món, xem lịch sử đơn/hóa đơn |
| Chef Web | Bếp | Nhận và cập nhật trạng thái món |
| Cashier Web | Thu ngân | Quản lý bàn, thanh toán, lịch sử |
| Admin Web | Quản trị | Quản lý chi nhánh, menu, tài khoản |
| API Gateway + services | — | Identity, Catalog, Orders, Customers, Billing/CloudHost |
| Realtime | — | SignalR/WebSocket đẩy sự kiện đơn/bàn |

Các giai đoạn nghiệp vụ B0–B7 (nguồn: `SelfRestaurant-master/docs/B*_STATUS.md`, đọc thêm `B*_TEST_CHECKLIST.md`):

| Giai đoạn | Nội dung |
|---|---|
| B0 | Nền tảng hợp đồng API `/api/mobile/v1`: quy ước, chủ sở hữu từng endpoint, state machine, `MobileApiError`; truy vết 72 màn hình Android + 27 quy tắc nghiệp vụ |
| B1 | Xác thực khách: đăng ký, đăng nhập, Google, refresh token xoay vòng, logout, quên/đặt lại/đổi mật khẩu |
| B2 | Hồ sơ cá nhân, điểm/hạng thành viên (MEMBER/SILVER/GOLD/DIAMOND), cài đặt và chi nhánh mặc định |
| B3 | Tìm chi nhánh, xem bàn trống, đặt bàn (tạo/sửa/hủy), đặt món trước |
| B4 | Quét QR, check-in theo đặt bàn hoặc khách vãng lai → mở **Dining Session** (1 phiên/bàn) |
| B5 | Gọi món nhiều đợt trong phiên, gọi phục vụ (nước, dụng cụ…), theo dõi trạng thái món qua SignalR |
| B6 | Báo giá, voucher/điểm, yêu cầu thanh toán (khóa phiên), ONSITE/VNPAY, chia hóa đơn, xuất hóa đơn, đóng phiên |
| B7 | Món yêu thích, hộp thông báo, đánh giá (chỉ khi đã có hóa đơn), FAQ, gửi hỗ trợ |

Luồng chính của khách: B1 đăng nhập → B3 chọn chi nhánh/đặt bàn → B4 quét QR mở phiên → B5 gọi món (Bếp xử lý) → B6 thanh toán (Thu ngân xác nhận) → hóa đơn → B7 đánh giá.

**Hiện trạng dữ liệu (chủ dự án xác nhận 12/10/2026):** VietTable **chưa có người dùng thật**. Toàn bộ dữ liệu trên
stable/Supabase (kể cả Session #20, Order #38, Bàn 37) là **dữ liệu thử** do chủ dự án tạo.

**Mục tiêu hiện tại:** đưa code mới nhất lên **stable** trên Vercel, chạy mượt, để chủ dự án test app Android + web trên stable.
Môi trường QA riêng **tạm hoãn** (kế hoạch giữ tại `docs/plans/VT-HO-01_QA_ENV.md`). Tiêu chí nghiệm thu toàn phần cũ:
`docs/acceptance/RELEASE_READINESS.md`.

## 2. Công nghệ

- Android: **Java** (module `:app`, package `com.restaurant.viettable`), AGP 9.3.2, Gradle 9.5.0,
  minSdk 24 / targetSdk + compileSdk 37, Java 11 + desugaring, ViewBinding, Navigation, CameraX + ML Kit (QR),
  SignalR Java 8.0.30, Credential Manager (Google ID).
  Build types (không phải flavor): `debug` (mặc định `http://10.0.2.2:5100`), `cloudPreview`
  (initWith debug, base URL **cố định = stable alias**, timeout 60 s), `release` (bắt buộc `-PVIETTABLE_API_BASE_URL=https://…`).
- Backend: **.NET 8 / ASP.NET Core**, EF Core 8.0.11; Gateway + 5 service (Identity, Catalog, Orders,
  Customers, Billing); `VietTable.CloudHost` gộp tất cả vào 1 container chạy trên Vercel.
- Web frontends (4 app): **React + Vite + TypeScript**, **npm** (`package-lock.json`); Customer/Cashier
  dùng `@microsoft/signalr`, Chef/Customer dùng TanStack Query.
- Database: **SQL Server 2022** (local, docker-compose) + **PostgreSQL Supabase** (staging/cloud, Npgsql);
  RabbitMQ (local). Supabase Storage cho ảnh.
- Hạ tầng: Vercel project `viettable-api` (scope `hienkhanhvu981-7145`): **stable alias và candidate cùng
  project, dùng chung biến môi trường Preview → chung DB Supabase staging**. Stable alias có ngoại lệ
  protection riêng (`alias-protection-override`); Production chỉ là trang placeholder.
  Docker/SDK container cho CloudHost tests.

## 3. Cấu trúc thư mục

```
/app/                                   # Android Customer (Gradle root ở thư mục gốc repo)
/SelfRestaurant-master/                 # Backend + web (solution SelfRestaurant.Microservices.sln)
  src/Gateway/SelfRestaurant.Gateway.Api/
  src/Services/SelfRestaurant.{Identity,Catalog,Orders,Customers,Billing}.Api/
  src/Backend/VietTable.CloudHost/      # host 1 container cho Vercel
  src/Shared/{CloudCompatibility,SelfRestaurant.Database}/
  src/Frontend/selfrestaurant-{customer,chef,cashier,admin}-web/  + shared/
  tests/{VietTable.CloudHost.Tests,SelfRestaurant.Backend.ContractTests,
         SelfRestaurant.Postgres.Tests,SelfRestaurant.SqlServer.CompatibilityTests}/
  tools/SelfRestaurant.CloudPreparation/  # ⚠ công cụ staging, kết nối thẳng DB, có action ghi DB
  vercel.json + Dockerfile.vercel        # deploy API-only (Vercel project link: viettable-api)
  deploy/staff-preview/ + Dockerfile.staff-preview  # deploy API + staff web cùng origin
  docs/B0_STATUS.md … B7_STATUS.md
/scripts/                               # PowerShell/Node QA scripts (nhiều script gọi Cloud thật)
/docs/                                  # tài liệu kỹ thuật, Cloud, hiệu năng
/docs/acceptance/                       # biên bản nghiệm thu, ma trận test, evidence
/docs/plans/                            # kế hoạch xử lý tồn đọng (chưa thực thi)
```

## 4. Lệnh build & test

Kết quả tham chiếu tại lần nghiệm thu 12/10/2026 ghi trong ngoặc.

```bash
# Android build Debug / CloudPreview + unit tests (54/54 PASS) — tại thư mục gốc repo, Windows JBR
gradlew.bat :app:assembleDebug :app:assembleCloudPreview :app:testDebugUnitTest

# Host dotnet.exe KHÔNG có SDK → chạy trong container `viettable-cloudhost-sdk` (dotnet/sdk:8.0),
# container mount thư mục SelfRestaurant-master tại /workspace (đã xác minh 12/10/2026).
# Chạy từ PowerShell. Git Bash tự đổi "/workspace" thành đường dẫn Windows → lỗi "Cwd must be an absolute path"
# (nếu buộc dùng Git Bash: đặt MSYS_NO_PATHCONV=1).
# CloudHost tests (75/75 PASS; nếu thiếu EF InMemory: dotnet restore đúng project trước)
docker exec -w /workspace viettable-cloudhost-sdk dotnet test tests/VietTable.CloudHost.Tests/VietTable.CloudHost.Tests.csproj --no-restore
# Backend contract tests (143 PASS, 1 skipped)
docker exec -w /workspace viettable-cloudhost-sdk dotnet test tests/SelfRestaurant.Backend.ContractTests/SelfRestaurant.Backend.ContractTests.csproj --no-restore

# Build 4 web frontends (4/4 PASS) — trong từng SelfRestaurant-master/src/Frontend/selfrestaurant-*-web
npm.cmd ci
npm.cmd run build

# Build image Production (bắt buộc khi "chạy toàn bộ test"; image này chạy thêm test web dùng chung + test bundle).
# Chỉ build trên máy, không deploy. Chạy trong thư mục do scripts/prepare-staff-preview.ps1 tạo ra:
docker build -f Dockerfile -t viettable-prod:prepared .
```

**Quy tắc (chủ dự án yêu cầu 15/10/2026):** "chạy toàn bộ test" luôn phải gồm cả **build image Production** từ
`SelfRestaurant-master/Dockerfile.staff-preview` (qua `scripts/prepare-staff-preview.ps1`). Docker local (`docker compose build gateway`)
dùng Dockerfile khác nên không thay thế được: ngày 15/10 image Production hỏng vì đường dẫn test cũ dù Playwright vẫn qua 24/24.

⚠ KHÔNG chạy nếu chưa hỏi: `scripts/*staging*`, `scripts/q1-*`, `scripts/*bootstrap*`, `scripts/provision-*`,
`tools/SelfRestaurant.CloudPreparation`, `docker compose up` (có db-init), mọi lệnh `vercel`/`npx vercel` ghi lên Cloud.
Vercel CLI không nằm trong PATH; dùng `npx vercel@62.4.0` (đã đăng nhập sẵn). Lệnh Vercel **chỉ đọc** được phép:
`whoami`, `ls`, `inspect`, `project ls`, `domains ls`.

Chỉ chạy các lệnh **build, unit test, contract test** khi được phép ở Mục 6. Mọi lệnh có thể ghi vào DB, gọi Cloud thật hay deploy đều phải hỏi trước.

## 5. Môi trường

| Môi trường | Ghi chú |
|---|---|
| Local | TODO: cách chạy backend + DB local |
| QA cô lập | Chưa có; tạm hoãn (kế hoạch `docs/plans/VT-HO-01_QA_ENV.md`) |
| Candidate Vercel | `dpl_7MxW7oBh8H1DCYmPSefcYX7Hf8fE` — Ready/Preview, **chưa đủ điều kiện release** |
| Stable | Alias **không được đổi** nếu chưa có phê duyệt |
| **Production mới** (đang dùng) | Tài khoản Vercel mới, team `viet-table`, project `viettable`, `https://viettable-beta.vercel.app`, region `syd1`. Chi tiết và trạng thái: mục 6a |

**Bảng mới trên Supabase (bài học 25/10/2026):** migration chạy bằng tài khoản quản trị; tài khoản runtime `viettable_<owner>` KHÔNG tự có quyền trên bảng mới → lỗi `42501 permission denied`. Mỗi migration tạo bảng phải kèm SQL `GRANT` cho runtime role (mẫu: `SelfRestaurant-master/docs/SEPAY_GRANTS_20261025.sql`) và kiểm tra `has_table_privilege` trước khi bật tính năng.

Secrets nằm trong `.env` / biến môi trường, **không bao giờ** commit, in ra log, hay nhúng vào APK.

## 6. Phạm vi được phép hiện tại

> Chủ dự án cập nhật mục này mỗi khi mở/đóng việc. Mặc định là **CHỈ ĐỌC**.

- Trạng thái: **MỞ VIỆC** (từ 12/10/2026)
- Việc đang mở: **Deploy code mới lên stable**
- Branch làm việc: `qa/vt-ho-01` (commit trên máy, **không push** khi chưa hỏi)
- Được phép không cần hỏi: sửa code, chạy build/unit test/contract test, đọc Vercel (lệnh chỉ đọc ở Mục 4) và đọc Supabase
- **Phải hỏi trước TỪNG việc:** deploy, migration DB, đổi stable alias, cài app lên Samsung, sao lưu/khôi phục DB, push

### 6a. Trạng thái hiện tại (cập nhật 25/10/2026 09:10)

| Mục | Trạng thái |
|---|---|
| Deploy đang chạy | Production `dpl_2EP1YtFdBASso8oQ5scbbnjnXbVs` (`https://viettable-ochhs9ypt-viet-table.vercel.app`, tạo 25/10/2026 09:03), `https://viettable-beta.vercel.app`, `[syd1]`, Ready. Code = `0badc17`, **cờ `Payments__SePay__Enabled=true`** (chủ dự án đổi trên Vercel 25/10, duyệt bật). Trước đó cùng ngày: `dpl_GpJkcqo…` (08:26, cờ tắt) và `dpl_4FajqtsF…` (08:43, nạp secret mới). SePay chế độ Test: webhook mô phỏng 08:50:19 → "signature valid; feature disabled" (chữ ký đúng). Image Production cùng nguồn build lại trên máy 25/10: thành công. Gọi thử 09:06: `/` 200 1,4 s, `/app/cashier/` 200 1,2 s, `/app/chef/` 200 2,6 s, `/api/mobile/v1/branches` 200 2,8 s, `/healthz` 200 0,7 s |
| Bản quay lui | `dpl_DPLgUGvTN99zvnrjkd1PZd9N3Utg` (`https://viettable-ezm902nl4-viet-table.vercel.app`, commit `436ee23`). Quay lui (cần chủ dự án duyệt, chạy ngoài thư mục `SelfRestaurant-master`): `npx vercel@62.4.0 rollback https://viettable-ezm902nl4-viet-table.vercel.app --scope viet-table`. Migration SePay chỉ thêm bảng nên bản cũ vẫn chạy được |
| APK đã cài | **Bản THỬ** `cloudProd` khoá app sau **30 giây** (`-PVIETTABLE_APP_LOCK_SECONDS=30`, file `artifacts/apk-test/app-cloudProd-KHOA-30-GIAY-chi-de-thu.apk`, SHA-256 bắt đầu `F4583C92`) build 19/10/2026 19:40 từ commit `88da383`, unit test 105/105, cài đè (không gỡ) lên Samsung SM-A528B lúc 19:43:02 19/10/2026; mở thử chạy được, không crash. Sau khi chủ dự án test xong phải **cài đè bản thật 5 phút** (`app/build/outputs/apk/cloudProd/app-cloudProd.apk`, `E7E6F34E`, cùng commit) — hỏi trước khi cài |
| Code chờ deploy | Không có (Production = `0badc17`). Migration `20261019134340_SePayBankTransferEvents` đã có trên Supabase (chủ dự án chạy 25/10, xác nhận bằng SELECT: 17 cột + 1 dòng lịch sử 8.0.11). Biến Production có tên: `Payments__SePay__WebhookSecret`, `Payments__SePay__AccountNumbers` (= tài khoản thử SePay 0000000001), `Payments__SePay__Enabled` (= false) |
| Git | `qa/vt-ho-01` push lên `origin` tới `e1ca3d8` (25/10, chủ dự án duyệt). `design/staff-redesign` đã push lên `origin` tới `4d7d645` (sao lưu) |
| Việc đang làm (app Android, hạn code 01/11) | TN1 vân tay + khoá app (`88da383`), chi nhánh theo tài khoản, không tự gán (`d80b940`). Chủ dự án đang test bản 30 giây; APK thật 5 phút từ `d80b940` (SHA-256 bắt đầu `6C42993A`) **chưa cài**. SePay: xong backend + web thu ngân (`1a3030f`). Thứ tự đã duyệt: migration (hỏi) → deploy cờ tắt (hỏi) → kiểm tra SePay gọi tới → bật cờ → test chế độ Test. Không đổi `Payments__BankTransfer__AccountNumber` cho tới khi VA nhận tiền thật. Sau đó TN2–TN4 |
| Việc còn mở | `REMAINING_ISSUES.md` G15 (`/healthz` ~7 s), G16 (hủy bàn cho phiên từ đặt bàn), G17 (thanh bên Admin trên điện thoại) — làm sau demo. Chưa rõ request nào trả lỗi rỗng gây "null" (log Vercel Hobby chỉ giữ ~1 giờ, lúc đọc 23:01 đã hết). Việc giảm tải lại 4–5 s đã làm ở `9afa0bb` (đã deploy 15/10). Redesign xong 14/10/2026; từ nay tới 01/11 chỉ sửa lỗi giao diện. Demo 01–10/11 |

## 7. TUYỆT ĐỐI KHÔNG làm nếu chưa có phê duyệt rõ ràng của chủ dự án

**Git & release**
- Không push, merge, reset, rebase, stash, discard thay đổi; không commit lên `main`.
- Không đụng các thay đổi chưa commit sẵn có (`.idea/*`, `tools/SelfRestaurant.CloudPreparation/Program.cs`,
  `StagingAdminBootstrap.cs`, `scripts/*` chưa track, `docs/STAGING_STAFF_ACCESS.md`) — chúng không phải của bạn.
- Không deploy, không đổi stable alias, không chạy migration DB nếu chưa được duyệt **cho lần đó**.
- Không tắt Vercel Deployment Protection; không nhúng OIDC/bypass token hay bất kỳ secret nào vào APK hoặc code client.

**Dữ liệu (dữ liệu thử, nhưng vẫn là Cloud dùng chung)**
- Session #20 / Order #38 / Bàn 37 là dữ liệu thử: được dùng khi test, nhưng thao tác ghi trên Cloud vẫn hỏi trước.
- Không chạy **recovery endpoint** (có cleanup toàn cục) hay script cleanup/xóa DB hàng loạt.
- Không chạy `tools/SelfRestaurant.CloudPreparation` (dưới mọi action) và các script gọi nó.

**Thiết bị**
- Samsung SM-A528B: được cài lại/cài đè app khi cần, nhưng **phải hỏi trước mỗi lần**.

**File nhạy cảm**
- Không mở, đọc, in hay commit `b64.txt`. Không đọc nội dung `%LOCALAPPDATA%\VietTable\staging-secrets\*`.

**Bằng chứng**
- Không xóa/ghi đè logs, screenshots, `docs/acceptance/**`, ma trận test. Kết quả mới → tạo file evidence mới, không sửa file cũ.
- Ngoại lệ duy nhất: khi chủ dự án yêu cầu, được **THÊM dòng mới** (ở cuối, phần Addendum) vào
  `docs/acceptance/REMAINING_ISSUES.md`. Không sửa hay xóa dòng đã có.

## 8. Tồn đọng (tóm tắt — chi tiết ở `docs/acceptance/REMAINING_ISSUES.md`)

| ID | Ưu tiên | Vấn đề |
|---|---|---|
| VT-HO-01 | P0 | APK CloudPreview bị Vercel Authentication 401 trước Identity |
| VT-HO-02 | P0 | Chưa chứng minh event payload realtime cô lập giữa 2 customer |
| VT-HO-03 | P1 | Recovery endpoint cleanup toàn cục → cần recovery theo phạm vi hẹp |
| VT-HO-04 | P1 | Order/Invoice History có dữ liệu trên Samsung + B0–B7 chưa nghiệm thu lại |
| VT-HO-05 | P1/P2 | Thẻ phiên Samsung chậm (p95 ~29,75 s, 3 mẫu) — cần tracing trước khi kết luận |
| VT-HO-06 | P1/P2 | Chef/Admin/Cashier chủ yếu chỉ có bằng chứng lịch sử |
| VT-HO-07 | Phụ thuộc | Chốt baseline git (HEAD so với commit `103bebb`, file chưa commit) |

## 9. Cách làm việc

1. Đầu phiên: đọc file này, chạy `git status` + `git log -10`, xác nhận Mục 6 trước khi làm gì.
2. Chỉ xử lý **một ID tồn đọng mỗi phiên**. Đề xuất kế hoạch, chờ duyệt rồi mới sửa.
3. Sửa trên branch riêng; diff nhỏ, có test đi kèm.
4. Báo cáo bằng **bằng chứng thực tế** (lệnh, exit code, output). Không ghi PASS nếu chưa chạy. "Build PASS" ≠ "E2E PASS"; "0 FAIL" ≠ "mọi thứ đúng".
5. Khi không chắc một thao tác có ảnh hưởng dữ liệu/Cloud thật hay không → **dừng và hỏi**.
6. Phát hiện thông tin trong file này sai/lỗi thời → đề xuất sửa CLAUDE.md.

### Quy tắc cuối phiên

Trước khi kết thúc mỗi phiên, cập nhật mục 6a (và mục trạng thái đầu `docs/plans/DEPLOY_NEW_VERCEL.md` nếu liên quan):
deploy nào đang chạy (id, commit), APK nào đã cài lên Samsung (build type, giờ cài), branch đã push tới commit nào, việc còn mở.
Ghi theo thông tin đã xác minh (lệnh chỉ đọc) hoặc chủ dự án xác nhận; ghi rõ nguồn. Commit thay đổi này (không push khi chưa hỏi).

## 10. Tài liệu tham chiếu

- `docs/acceptance/VIETTABLE_NGHIEM_THU_BAN_GIAO_2026-10-12.md` — biên bản bàn giao (đọc khi cần bối cảnh đầy đủ)
- `docs/acceptance/FINAL_ACCEPTANCE_REPORT.md`
- `docs/acceptance/FULL_ACCEPTANCE_MATRIX.csv` — 64 ca test và trạng thái
- `docs/acceptance/REMAINING_ISSUES.md`
- `docs/acceptance/RELEASE_READINESS.md`
- `docs/acceptance/evidence/RUN_2026-10-12.md`
- `docs/Q1_CLOUD_E2E_ACCEPTANCE.md`
- `docs/FULL_SYSTEM_PERFORMANCE_AUDIT.md`
