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

Secrets nằm trong `.env` / biến môi trường, **không bao giờ** commit, in ra log, hay nhúng vào APK.

## 6. Phạm vi được phép hiện tại

> Chủ dự án cập nhật mục này mỗi khi mở/đóng việc. Mặc định là **CHỈ ĐỌC**.

- Trạng thái: **MỞ VIỆC** (từ 12/10/2026)
- Việc đang mở: **Deploy code mới lên stable**
- Branch làm việc: `qa/vt-ho-01` (commit trên máy, **không push** khi chưa hỏi)
- Được phép không cần hỏi: sửa code, chạy build/unit test/contract test, đọc Vercel (lệnh chỉ đọc ở Mục 4) và đọc Supabase
- **Phải hỏi trước TỪNG việc:** deploy, migration DB, đổi stable alias, cài app lên Samsung, sao lưu/khôi phục DB, push

### 6a. Trạng thái hiện tại (cập nhật 15/10/2026 10:08)

| Mục | Trạng thái |
|---|---|
| Deploy đang chạy | Production `dpl_HvjDLTFJ3WFkWf3b1kbt6JC2Bzcg` (tạo 15/10/2026 09:45), project `viettable` (team `viet-table`), `https://viettable-beta.vercel.app`, `[syd1]`, Ready (`vercel inspect`). Code = `qa/vt-ho-01` `fcd83a9`: giao diện mới + sửa "null" + realtime cho bếp/thu ngân (`9afa0bb`: bếp nhận đơn qua SignalR, hỏi dự phòng 60 s, 10 s khi mất realtime; 2 khung thu ngân và sơ đồ bàn cập nhật theo sự kiện, dự phòng 60 s, 15 s khi mất realtime; không thao tác 15 phút thì dừng hỏi + ngắt realtime, hiện thanh "Tạm dừng cập nhật để tiết kiệm. Bấm để tiếp tục"; máy chủ trả JSON tiếng Việt cho lỗi chưa xử lý ở `/api`). Gọi thử sau deploy: `/` 200 1,4 s, `/app/cashier/` 200 7,8 s, `/app/chef/` 200 0,8 s, `/app/admin/` 200 1,0 s; file JS bếp + thu ngân có thanh tạm dừng. **Realtime bếp/thu ngân đã xác nhận chạy trên Production ngày 15/10/2026** (chủ dự án test: đơn mới hiện ở bếp qua realtime, giao dịch tại quầy hiện ở thu ngân, thanh toán OK, không có khung lỗi). Theo dõi log 09:49–10:04 lúc test: 0 request 500/502/503/504, 0 dòng `ApiError` (nguồn: `vercel logs` chỉ đọc) |
| Code chờ deploy | (**chưa deploy**) `be88d4e` (hộp xác nhận hủy món đã nấu xong) + đợt 1 dọn tính năng `7d8873d` + **thanh toán chỉ Tiền mặt/Chuyển khoản VietQR và doanh thu Admin = tiền thực thu** (commit sau `7d8873d`, xem `docs/plans/FEATURE_GAPS_2026-10-15.md`). **Trước khi deploy phải thêm 4 biến Vercel** `Payments__BankTransfer__BankBin/BankName/AccountNumber/AccountName` (thiếu → chỉ còn Tiền mặt). Test: Playwright 29/29, CloudHost 103/103, contract 184 + 1 bỏ qua, web dùng chung 29/29, Android unit 74/74 + build debug/cloudPreview, build 3 web, build image Production; mã QR giải mã thử bằng zxing-cpp đúng chuỗi. **APK mới chưa build/cài** (phải hỏi; deploy máy chủ trước rồi mới cài APK) |
| Bản quay lui | `dpl_8EwCeSUzAdaAcgyFjbqNbbzU87Mx` (`https://viettable-7o4wc01g7-viet-table.vercel.app`, commit `b63cbcf`, chưa có realtime). Quay lui (cần chủ dự án duyệt, chạy ngoài thư mục `SelfRestaurant-master`): `npx vercel@62.4.0 rollback https://viettable-7o4wc01g7-viet-table.vercel.app --scope viet-table` |
| APK đã cài | `cloudProd` build 13/10/2026 19:04 từ commit `4a11139`, cài đè lên Samsung SM-A528B lúc 19:10:18 13/10/2026 (không đổi) |
| Git | `qa/vt-ho-01` (gồm merge `159141f`, sửa `b63cbcf`, realtime `9afa0bb`, sửa Dockerfile `fcd83a9`, hủy món đã sẵn sàng `be88d4e`, đợt 1 dọn tính năng) push lên `origin` cùng commit cập nhật mục này. `design/staff-redesign` đã push lên `origin` tới `4d7d645` (sao lưu) |
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
