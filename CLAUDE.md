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

### 6a. Trạng thái hiện tại (cập nhật 26/10/2026 cuối phiên)

| Mục | Trạng thái |
|---|---|
| Deploy đang chạy | Production `dpl_22JVoQfqphyBqmTQcixT5f4uQfrj` (`https://viettable-gtvgkhc43-viet-table.vercel.app`, tạo 26/10/2026 14:30), `https://viettable-beta.vercel.app`, `[syd1]`, Ready (`vercel inspect`). Code = `d917668` (đặt bàn mục 5 chọn bàn trên sơ đồ, trên nền `ef15511`), deploy từ đúng thư mục đã build image Production (`54d0a2175d84`), **cờ `Payments__SePay__Enabled=true`**. Không migration. Đã xoá `.env.local` trước deploy. Gọi thử 14:34: `/healthz` 200 1,4 s, `/` 200 1,3 s, `/app/cashier/` 200 0,9 s, `/app/chef/` 200 1,1 s, `/app/admin/` 200 0,9 s, `/api/mobile/v1/branches` 200 1,0 s; `availability` chi nhánh 1 ngày kia 12:00 2 khách: `tables` 20 bàn (AVAILABLE 20), `availableTables` 20 |
| Bản quay lui | `dpl_33SosH2FEXVeHqEWd5ktN66CqNHL` (`https://viettable-fizoow43o-viet-table.vercel.app`, code `ef15511`, cờ SePay bật). Quay lui (cần chủ dự án duyệt, chạy ngoài thư mục `SelfRestaurant-master`): `npx vercel@62.4.0 rollback https://viettable-fizoow43o-viet-table.vercel.app --scope viet-table` |
| APK đã cài | **Bản thật** `cloudProd` code `a2ee699` (Trang chủ "Phố đêm" + sửa chip/tiêu đề ở cỡ chữ 130%, "Chào Vinh,"), SHA-256 bắt đầu `19e0ed53b407c5e2`, cài đè (`adb install -r`) lên Samsung SM-A528B lúc 20:26:41 26/10/2026 (`dumpsys` lastUpdateTime; chủ dự án duyệt). Trước đó: `34e82e6b` lúc 20:15:21. Kiểm trên máy: dữ liệu thật hiện đúng, "Món của bếp" = Bún Chả Hà Nội, 9 ảnh món khớp tên. Cỡ chữ máy 130% |
| Code chờ deploy | Không có: backend `d917668` đang chạy trên Production, app `d04342c` đã cài. Test tay 26/10 (chủ dự án báo ĐẠT): đặt bàn mục 1, thông báo (7 bước), đặt bàn mục 2 thu ngân xác nhận/từ chối (7 bước), nhắc lịch bản thử 3/1 phút (#15 nhắc 3 và 1 phút, huỷ / bị từ chối không nhắc, bấm thông báo mở khoá rồi vào đúng chi tiết), sơ đồ chọn bàn mục 5 (6 bước: sơ đồ, không đủ ghế, đã được đặt, tranh bàn, đổi đặt bàn, thẻ "Đặt bàn sắp tới"). Đặt bàn thử của tài khoản A (#15, #18) và B (#21) đã huỷ |
| Git | `qa/vt-ho-01` push lên `origin` tới commit trạng thái cuối phiên 26/10 (chủ dự án duyệt, không force), gồm `SelfRestaurant-master/docs/DESIGN_PHO_DEM.md`. `design/staff-redesign` đã push lên `origin` tới `4d7d645` (sao lưu) |
| Việc đang làm (app Android, hạn code 01/11) | **Còn dở:** test tay nhắc lịch khi đổi giờ: chưa làm, làm lại sau với bản thật (#18 không test được vì chủ dự án đổi tài khoản trước giờ nhắc; lịch nhắc tài khoản cũ bị xoá đúng thiết kế). **Thứ tự việc tiếp theo (chủ dự án chốt 26/10):** 1) làm lại UI app khách theo `SelfRestaurant-master/docs/DESIGN_PHO_DEM.md` ("Phố đêm", 6 nhóm, chỉ đổi giao diện, giữ đủ chức năng); 2) đặt cọc (đặt bàn mục 4: 50.000đ/khách từ 6 khách, hoàn khi hủy ≥ 2 giờ trước hoặc nhà hàng từ chối, cờ riêng mặc định tắt); 3) khuyến mãi (tab "Ưu đãi"); 4) chuẩn bị demo |
| Việc app đang làm (chủ dự án giao 26/10 tối) | **Giao diện:** Trang chủ mẫu "Phố đêm" ĐÃ DUYỆT (làm chuẩn cho các màn khác), nhưng **chưa làm lại màn khác cho tới khi chủ dự án duyệt ảnh Trang chủ thật trên Samsung**. Ảnh "đang ở bàn": chủ dự án tự quét QR rồi báo chụp, **không tự mở phiên**. Tên khách: lưu cục bộ (đã viết hoa) sau lần tải đầu, mở app hiện ngay, tải xong cập nhật; đăng xuất/đổi tài khoản thì xoá. **Việc 4 (commit riêng, tách giao diện), nguyên tắc: không hiện số giả/số cứng; không có dữ liệu thật thì ẩn hoặc hiện đang tải:** a) Thanh toán thành công: "+X điểm" = điểm thật cộng từ hoá đơn đó (API/payment response), không có thì ẩn; b) tiêu đề "Ngon miệng nhé, <tên>!" (cùng tên đã viết hoa); c) thanh tiến độ hạng (Tôi/thành viên) dùng điểm thật + ngưỡng hạng kế tiếp thật; API không có ngưỡng thì báo chủ dự án, tạm ẩn thanh; d) màn dùng điểm khi thanh toán: bỏ mặc định 2.000/15.000, hiện số dư thật; e) grep mọi điểm/hạng/số tiền/tên cứng khác, **gửi danh sách trước khi sửa** chỗ ngoài a–d. **Việc 5 (sau việc 4, mỗi việc commit riêng):** ẩn "Online VNPAY" sau cờ cấu hình mặc định tắt; màn VietQR nút sao chép số TK, số tiền, nội dung CK; màn Tiến độ món thẻ "x/y món đã xong" **Trạng thái cuối 26/10:** tên khách lưu cục bộ xong (`4f9f75d`); Việc 4 a–d xong (`da5faee`); Việc 4e: danh sách đã gửi, **chờ chủ dự án chọn**; Việc 5 xong: cờ VNPAY (`43a32cc`), sao chép VietQR (`f59a503`), thẻ x/y món (`2ea3752`). Code `4f9f75d`→`2ea3752` **chưa cài lên Samsung** (APK build cuối SHA `51162427cd80fd9e`). Chờ: chủ dự án quét QR để chụp ảnh "đang ở bàn" thật; duyệt ảnh Trang chủ thật trước khi làm màn khác |
| Việc còn mở | `REMAINING_ISSUES.md` G15 (`/healthz` ~7 s), G16 (hủy bàn cho phiên từ đặt bàn: khách chưa hủy được phiên mở từ đặt bàn, E2E dùng dọn dẹp chung), G17 (thanh bên Admin trên điện thoại) — làm sau demo. Chưa rõ request nào trả lỗi rỗng gây "null" (log Vercel Hobby chỉ giữ ~1 giờ). Web nhân viên redesign xong 14/10/2026 (chỉ sửa lỗi giao diện); app khách làm lại giao diện là việc 1 ở trên. E2E: Identity giới hạn 30 lần đăng ký/phút, test dùng `registerGuest` (đợi rồi thử lại). Demo 01–10/11 |

### 6b. Sửa sau (chủ dự án chốt 26/10/2026: chưa sửa web/backend lúc này)

**Điểm thưởng: backend, app và màn thu ngân đang lệch nhau (đã đọc code 26/10, cần chủ dự án chốt quy tắc):**

| Chỗ | Quy tắc hiện tại trong code |
|---|---|
| **Tích điểm** (`Identity` `POST /api/customers/{id}/loyalty/settle`, dùng cho thanh toán qua app và thu ngân) | `điểm cộng = floor(số tiền đã trả × 1%)` → **100đ = 1 điểm** (hoá đơn 245.000đ cộng 2.450 điểm). Lời mô tả hạng Vàng/Kim Cương trong API nói "tích điểm 5%": không khớp 1% |
| **Dùng điểm trên app** (`MobileCheckoutController.ApplyLoyalty`) | **1 điểm = 1.000đ** giảm: `PointsDiscount = PointsApplied × 1000`, tối đa `floor((tạm tính − voucher)/1000)` điểm |
| **Dùng điểm ở thu ngân** (`CashierController`) | **1 điểm = 1đ** giảm, bội số 1.000 điểm |
| **Trừ điểm khi chốt** (`settle`) | chỉ trừ theo **bội số 1.000 điểm** (`pointsUsed/1000*1000`), còn app cho chọn bước 500 điểm và giảm theo từng điểm → dùng 500 điểm được giảm 500.000đ nhưng không bị trừ điểm |
| **Hạng** | Bạc từ 1.000, Vàng từ 5.000, Kim Cương từ 20.000 điểm (`MobileProfileController.GetTier`) |

Hệ quả: tích 1 điểm/100đ mà mỗi điểm đổi 1.000đ thì 1 hoá đơn đủ tiền cho hơn 10 lần giảm hết hoá đơn tiếp theo. Cần chốt tỉ giá một chỗ (backend là nguồn đúng), sửa thu ngân cho khớp, và cho API trả `pointValueVnd` (app đã sẵn sàng đọc trường này từ `GET /payment-options`; chưa có thì app **không hiện** số quy đổi). Backend chưa trả số điểm cộng trong hoá đơn mobile: app tạm đọc từ thông báo `loyalty.points-earned.v1`.

**Lỗi/việc khác cần làm sau demo:** mục 4e chưa sửa vì chủ dự án để nguyên: dòng 6 (đặt cọc: làm chung việc đặt cọc), 48 chữ mẫu trong layout (code luôn ghi đè, dọn khi làm lại từng màn).

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
