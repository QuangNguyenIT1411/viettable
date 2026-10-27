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

### 6a. Trạng thái hiện tại (cập nhật 27/10/2026 tối)

| Mục | Trạng thái |
|---|---|
| Deploy đang chạy | Production `dpl_Hzojg5bVk4NaLQUFACe5TX4VmuQe` (`https://viettable-r8czjkbr2-viet-table.vercel.app`, tạo 27/10/2026 10:12), `https://viettable-beta.vercel.app`, `[syd1]`, Ready (`vercel inspect`). Code = `0637818` (quy tắc điểm mới mục 6b + giới hạn 10%; so với bản trước chỉ khác phần điểm), deploy từ thư mục đã build image Production (`b537a388ee91`), không migration, đã xoá `.env.local` trước deploy (chủ dự án duyệt 27/10). Gọi thử 1 lần: `/healthz` 200 1,6 s, `/` 200 1,5 s, `/app/cashier/` 200 1,0 s, `/app/chef/` 200 0,6 s, `/app/admin/` 200 0,7 s, `/api/mobile/v1/branches` 200 1,2 s; `/api/mobile/v1/payment-options` 401 khi ẩn danh (cần đăng nhập) nên **chưa xem trực tiếp** `pointValueVnd/pointsStep/maxPointsDiscountPercent` |
| Bản quay lui | `dpl_22JVoQfqphyBqmTQcixT5f4uQfrj` (`https://viettable-gtvgkhc43-viet-table.vercel.app`, code `d917668`, cờ SePay bật). Quay lui (cần chủ dự án duyệt, chạy ngoài thư mục `SelfRestaurant-master`): `npx vercel@62.4.0 rollback https://viettable-gtvgkhc43-viet-table.vercel.app --scope viet-table` |
| APK đã cài | **MỚI NHẤT (chủ dự án duyệt trước 27/10):** `cloudProd` code `4e84384` (= giao diện nhóm 4 + sửa ngày đặt bàn + trạng thái lỗi + 3 màn Quét QR/Phiên bàn, Đơn hiện tại, Chi tiết đơn), SHA-256 bắt đầu `c32b0cfd863a001c`, `adb install -r` lên Samsung SM-A528B, `dumpsys` lastUpdateTime 19:07:19 27/10/2026 (đồng hồ máy). Chưa test trên máy. Bản trước đó: `cloudProd` code `a3f87db` (= `cb84272` + giao diện nhóm 3 + sửa dùng điểm), SHA-256 bắt đầu `4ad60bc514f7d824`, `adb install -r` lên Samsung SM-A528B, `dumpsys` lastUpdateTime 12:03:55 (đồng hồ máy). Chủ dự án xác nhận không Back từ VietQR trước khi dùng điểm, nên nguyên nhân #43 chưa chốt: cần test lại trên bản này. Dòng dưới là bản cũ trước đó: **Bản thật** `cloudProd` code `0637818` (Trang chủ Phố đêm, tên khách lưu cục bộ, Việc 4–5, quy tắc điểm mới: bước/tỉ giá/giới hạn 10% đọc từ server), SHA-256 bắt đầu `d5f217cacea85597`, cài đè (`adb install -r`) lên Samsung SM-A528B lúc 10:18:47 27/10/2026 (`dumpsys` lastUpdateTime; chủ dự án duyệt). Chưa test trên máy: chủ dự án sẽ test quét QR → gọi món → tiến độ → chuyển khoản SePay test → thành công → thẻ thành viên |
| Code chờ deploy | Backend: không có (`0637818` đang chạy). **App nhóm 4 (27/10, ĐÃ cài cùng APK c32b0cfd):** chủ dự án nhận 3 chỗ tự quyết (ẩn thanh điều hướng ở Thực đơn/bước 2,3,5 đặt bàn; "Chuyển khoản VietQR"; nhãn trạng thái đặt bàn theo dữ liệu thật). Bổ sung sau duyệt, mỗi việc 1 commit: ngày đặt bàn lưu ISO yyyy-MM-dd, chỉ format khi hiển thị (`a4fe332`, sửa "Chưa rõ ngày" từ bước 2); lỗi tải hiện trạng thái lỗi riêng thay danh sách (`ListState`, 9 màn, `def9083`); làm lại Quét QR + Phiên bàn (`170d6cf`), Đơn hiện tại (`1e31062`), Chi tiết đơn (`4e84384`); 161/161 unit test, 3 bản build OK; ảnh `screenshots
hom4\emulator_23…27_*` + `SO_SANH_nhom4_bosung.png`. Trước đó: làm lại theo `DESIGN_PHO_DEM.md` các màn còn lại, mỗi màn 1 commit (`83e5a7d`…`02b477a`, 24 commit): Chào mừng/Đăng nhập/Đăng ký; Thực đơn + Thực đơn tại bàn + Giỏ (lưới đĩa tròn, thanh giỏ nổi; lưới danh mục/đặt trước/tìm kiếm/yêu thích cũng dùng thẻ đĩa); Chọn phương thức thanh toán, Hoá đơn, Dùng điểm (logic a3f87db giữ nguyên, chỉ đổi nút quay lại), Voucher; Đặt bàn 5 bước (thanh 5 đoạn, chip ngày, lưới giờ 3 cột, sơ đồ bàn theo chú thích thiết kế), Đặt bàn thành công, Lịch sử + Chi tiết đặt bàn; Đơn hàng, Thông báo; Tôi, Thẻ thành viên. Build debug/cloudPreview/cloudProd OK, 157/157 unit test (15:02 27/10). APK cloudProd `02b477a` SHA-256 bắt đầu `2fda522184974937` (chưa cài). Ảnh máy ảo 100%/130%: `screenshots
hom4\` + ảnh tổng `SO_SANH_nhom4.png`; test xem trước `Group4DesignPreviewTest` (chỉ chạy tay, dữ liệu mẫu). Chưa làm lại (vẫn theme tối chung): quên/đặt lại mật khẩu, khoá vân tay, sửa hồ sơ, cài đặt, hỗ trợ, voucher của tôi, đánh giá, chi tiết hoá đơn lịch sử, chia hoá đơn, các màn thanh toán phụ (địa điểm/tại quầy/online/đang xử lý/thất bại), đặt trước, chi nhánh, check-in/gọi phục vụ, tìm kiếm (Quét QR, Phiên bàn, Đơn hiện tại, Chi tiết đơn đã làm 27/10). **App nhóm 3 (28/10, ĐÃ cài cùng APK 4ad60bc5, ảnh `screenshots\nhom3\SO_SANH_nhom3_P03-P06.png`):** 4 màn viết lại theo P03–P06, mỗi màn 1 commit: Chi tiết món `7b2c5f5`, Tiến độ món `73a69f8`, chờ thanh toán/VietQR `0993b17`, Thanh toán thành công `a1f8751`. Build debug/cloudPreview/cloudProd OK, 151/151 unit test. **Sửa lỗi dùng điểm (28/10, đã cài cùng APK 4ad60bc5):** bấm xác nhận giờ gọi server và chờ; chỉ `pointsDiscount > 0` mới báo "Đã áp dụng −X đ", lỗi/trả 0 hiện dialog và ở lại màn chọn điểm; hoá đơn khoá "Chọn Voucher"/"Dùng Điểm" + dòng giải thích khi báo giá không còn OPEN; mở lại hoá đơn (Back từ VietQR) tải `active-payment` để lấy báo giá thật. Nguyên nhân nghi ngờ (chưa kiểm chứng bằng dữ liệu): đổi điểm sau khi đã yêu cầu thanh toán → server từ chối `state_conflict`, app vẫn báo thành công cục bộ. 157/157 unit test. Không có nút "Giục bếp" (app không có chức năng này) và không có "dự kiến xong"/"12 phút" (API không có). **App: `cb84272` (thẻ đang ở bàn 2 hàng) đã nằm trong APK 4ad60bc5 đã cài.** Test 27/10: 150/150 unit test, 3 bản build OK |
| Git | `qa/vt-ho-01` push lên `origin` tới commit trạng thái cuối phiên 26/10 (chủ dự án duyệt, không force), gồm `SelfRestaurant-master/docs/DESIGN_PHO_DEM.md`. `design/staff-redesign` đã push lên `origin` tới `4d7d645` (sao lưu) |
| Việc đang làm (app Android, hạn code 01/11) | **Còn dở:** test tay nhắc lịch khi đổi giờ: chưa làm, làm lại sau với bản thật (#18 không test được vì chủ dự án đổi tài khoản trước giờ nhắc; lịch nhắc tài khoản cũ bị xoá đúng thiết kế). **Thứ tự việc tiếp theo (chủ dự án chốt 26/10):** 1) làm lại UI app khách theo `SelfRestaurant-master/docs/DESIGN_PHO_DEM.md` ("Phố đêm", 6 nhóm, chỉ đổi giao diện, giữ đủ chức năng); 2) đặt cọc (đặt bàn mục 4: 50.000đ/khách từ 6 khách, hoàn khi hủy ≥ 2 giờ trước hoặc nhà hàng từ chối, cờ riêng mặc định tắt); 3) khuyến mãi (tab "Ưu đãi"); 4) chuẩn bị demo |
| Việc app đang làm (chủ dự án giao 26/10 tối) | **Giao diện:** Trang chủ mẫu "Phố đêm" ĐÃ DUYỆT (làm chuẩn cho các màn khác), nhưng **chưa làm lại màn khác cho tới khi chủ dự án duyệt ảnh Trang chủ thật trên Samsung**. Ảnh "đang ở bàn": chủ dự án tự quét QR rồi báo chụp, **không tự mở phiên**. Tên khách: lưu cục bộ (đã viết hoa) sau lần tải đầu, mở app hiện ngay, tải xong cập nhật; đăng xuất/đổi tài khoản thì xoá. **Việc 4 (commit riêng, tách giao diện), nguyên tắc: không hiện số giả/số cứng; không có dữ liệu thật thì ẩn hoặc hiện đang tải:** a) Thanh toán thành công: "+X điểm" = điểm thật cộng từ hoá đơn đó (API/payment response), không có thì ẩn; b) tiêu đề "Ngon miệng nhé, <tên>!" (cùng tên đã viết hoa); c) thanh tiến độ hạng (Tôi/thành viên) dùng điểm thật + ngưỡng hạng kế tiếp thật; API không có ngưỡng thì báo chủ dự án, tạm ẩn thanh; d) màn dùng điểm khi thanh toán: bỏ mặc định 2.000/15.000, hiện số dư thật; e) grep mọi điểm/hạng/số tiền/tên cứng khác, **gửi danh sách trước khi sửa** chỗ ngoài a–d. **Việc 5 (sau việc 4, mỗi việc commit riêng):** ẩn "Online VNPAY" sau cờ cấu hình mặc định tắt; màn VietQR nút sao chép số TK, số tiền, nội dung CK; màn Tiến độ món thẻ "x/y món đã xong" **Trạng thái cuối 26/10:** tên khách lưu cục bộ xong (`4f9f75d`); Việc 4 a–d xong (`da5faee`); Việc 4e xong theo danh sách chủ dự án chốt (`c866b75`; mục "sửa sau" ở 6b); Việc 5 xong: cờ VNPAY (`43a32cc`), sao chép VietQR (`f59a503`), thẻ x/y món (`2ea3752`). Tất cả đã cài (APK `c1e17d09e6b3400d`). Chờ: chủ dự án test một lượt rồi báo để chụp ảnh "đang ở bàn" thật; duyệt ảnh Trang chủ thật trước khi làm màn khác |
| Việc còn mở | `REMAINING_ISSUES.md` G15 (`/healthz` ~7 s), G16 (hủy bàn cho phiên từ đặt bàn: khách chưa hủy được phiên mở từ đặt bàn, E2E dùng dọn dẹp chung), G17 (thanh bên Admin trên điện thoại) — làm sau demo. Chưa rõ request nào trả lỗi rỗng gây "null" (log Vercel Hobby chỉ giữ ~1 giờ). Web nhân viên redesign xong 14/10/2026 (chỉ sửa lỗi giao diện); app khách làm lại giao diện là việc 1 ở trên. E2E: Identity giới hạn 30 lần đăng ký/phút, test dùng `registerGuest` (đợi rồi thử lại). Demo 01–10/11 |

### 6b. Điểm thưởng và việc sửa sau

**Điểm thưởng: quy tắc chủ dự án CHỐT 26–27/10/2026 (đã deploy 27/10, `dpl_Hzojg5bV…`):**
- Tích: **1 điểm / 1.000đ đã trả**, làm tròn xuống (245.000đ → 245 điểm).
- Đổi: **1 điểm = 10đ**, bước **1.000 điểm** (= 10.000đ), app và thu ngân cùng bước; không đổi quá số dư; **giảm bằng điểm tối đa 10% hoá đơn** (`MaxDiscountPercent`, chung app + thu ngân; `/payment-options` và thẻ đơn thu ngân trả `maxPointsDiscountPercent`).
- Một nguồn duy nhất: `SelfRestaurant-master/src/Shared/Loyalty/LoyaltyRules.cs` (liên kết vào Identity, Billing, Gateway). App đọc `pointValueVnd` + `pointsStep` từ `GET /api/mobile/v1/payment-options`; web thu ngân đọc từ thẻ đơn trong dashboard (`pointValueVnd`, `pointsStep`). Không có thì không cho đổi điểm (không đoán tỉ giá).
- Mô tả quyền lợi hạng: "Tích 1 điểm cho mỗi 1.000đ thanh toán" (bỏ "tích 5%").
- Quy tắc cũ trước khi sửa: tích 1%; app đổi 1.000đ/điểm; thu ngân 1đ/điểm và tối đa 10% hoá đơn; app cho bước 500 nhưng quyết toán chỉ trừ bội số 1.000.
- Điểm đã tích/đổi sai trên tài khoản thử: **chưa sửa dữ liệu**; liệt kê bằng SQL chỉ đọc `SelfRestaurant-master/docs/LOYALTY_RECHECK_READONLY_20261026.sql` (chạy trong Supabase SQL Editor; Claude không đọc được chuỗi kết nối).
- Backend chưa trả số điểm cộng trong hoá đơn mobile: app tạm đọc từ thông báo `loyalty.points-earned.v1`.

**Backend, gộp vào lần deploy đặt cọc (chưa deploy riêng, chủ dự án chốt 28/10):** `MobileCheckoutController.ApplyLoyalty`/`ApplyVoucher` đang trả chung `state_conflict` ("Trạng thái hiện tại không cho phép thao tác này.") khi báo giá không còn OPEN → đổi thành mã/lời riêng (ví dụ `quote_locked`: "Đã yêu cầu thanh toán, không đổi được voucher/điểm"); thêm `GET checkout/quotes/{id}` để app tải lại báo giá thật khi Back (hiện chỉ có `active-payment`, chỉ trả báo giá khi có giao dịch đang chờ). App đã xử lý mã `state_conflict` hiện tại (`LoyaltyApplyRules`).

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
