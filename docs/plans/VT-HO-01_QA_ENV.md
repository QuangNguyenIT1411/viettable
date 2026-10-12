# VT-HO-01 — Kế hoạch hướng A: môi trường QA Cloud riêng

> **Trạng thái: KẾ HOẠCH, CHƯA THỰC THI.** Lập ngày 12/10/2026 trong phiên chỉ đọc. Chưa tạo gì trên Vercel/Supabase,
> chưa sửa code Android. Mỗi bước có nhãn:
> - 👤 **Chủ dự án tự làm** trên dashboard (Claude không có/không được dùng quyền này)
> - 🔒 **Cần chủ dự án duyệt bằng văn bản** trước khi Claude làm
> - 🤖 Claude làm được khi phiên được mở việc (Mục 6 CLAUDE.md), không đụng Cloud

## 1. Bối cảnh (đã xác minh bằng cách đọc tài liệu trong repo)

- Stable alias `viettable-staging-hienkhanhvu981.vercel.app` và candidate `viettable-k8j32aegw-…-7145.vercel.app`
  **nằm cùng Vercel project `viettable-api`** (scope `hienkhanhvu981-7145`). Nguồn: `docs/ANDROID_CLOUD_PREVIEW_ACCEPTANCE.md`
  mục "Approved origin and scope"; `scripts/verify-staging-q1-admin.ps1` kiểm tra candidate thuộc `viettable-api`.
- Cả hai đều là deployment **Preview**, dùng **chung biến môi trường Preview** ⇒ **chung DB Supabase staging**
  (nơi có Session #20 / Order #38 / Bàn 37 thật). Production của project chỉ là trang placeholder.
- Stable truy cập công khai được nhờ một ngoại lệ `alias-protection-override` gắn **riêng alias đó**.
  Candidate không có ngoại lệ ⇒ APK trỏ vào candidate nhận Vercel 401.
- APK `cloudPreview` hiện tại trỏ cố định vào stable alias (`app/build.gradle`), nên bản đang cài trên Samsung không lỗi.
- Candidate **không khớp chính xác commit nào**: ≈ `4d4abf6` + 2 thay đổi chưa commit (`GatewayModule.cs`,
  `deploy/staff-preview/vercel.json`). Chi tiết và mức độ chắc chắn ở mục 8.

## 2. Hướng A khác gì "promote candidate lên stable kèm rollback"?

| | **A — Môi trường QA riêng** | **Promote lên stable + rollback** |
|---|---|---|
| Làm gì | Dựng thêm 1 bản hệ thống thứ hai (Vercel project + DB riêng), cài app QA trỏ vào đó | Trỏ stable alias sang candidate; nếu lỗi thì trỏ lại bản cũ |
| Dữ liệu | DB **riêng, rỗng/dữ liệu giả** → được thoải mái test ghi, thanh toán thử, 2 khách A/B | **Dữ liệu thật** (Session #20…) → vẫn không được test ghi |
| Ảnh hưởng người dùng | Không ảnh hưởng Samsung/stable | Samsung và mọi người dùng stable **nhận code mới ngay** |
| Công sức / chi phí | Nhiều (nhân bản cấu hình, secret, dữ liệu; có thể tốn phí) | Ít (1 thao tác alias), rollback nhanh |
| Giải quyết thêm | Mở đường cho VT-HO-02/03/04/06 (cần QA cô lập) | Chỉ giải quyết VT-HO-01 |
| Rủi ro chính | Môi trường QA lệch cấu hình so với thật | Lỗi của candidate chạm dữ liệu thật trước khi kịp rollback |

**Gợi ý:** nếu chỉ cần "APK chạy được", promote rẻ hơn nhưng bỏ qua bước nghiệm thu trước phát hành. Nếu muốn
nghiệm thu đầy đủ các mục P0/P1 còn lại, A là nền tảng cần có. Quyết định thuộc về chủ dự án.

## 3. Kiến trúc đích

```
Android app "VietTable QA" (build type cloudQa, applicationId com.restaurant.viettable.qa)
        │ HTTPS, không token Vercel
        ▼
Vercel project MỚI  viettable-qa  ──►  Supabase project MỚI  viettable-qa (Postgres + Storage riêng)
(code = đúng commit candidate)
```

Truy cập công khai: khuyến nghị deploy làm **Production của project QA** (mặc định công khai, không cần ngoại lệ
protection). Phương án dự phòng: Preview + `alias-protection-override` giống stable (đã có tiền lệ, phức tạp hơn).
Backend vẫn bắt buộc đăng nhập VietTable cho mọi API cần quyền.

## 4. Các bước

| # | Bước | Ai | Ghi chú |
|---|---|---|---|
| 0 | Chốt **commit nguồn** cho QA: commit hóa thay đổi đang dở hoặc chọn `103bebb`. Không deploy từ working tree bẩn | 👤 quyết định, 🤖 chuẩn bị | Liên quan VT-HO-07 |
| 1 | Kiểm tra gói Vercel và Supabase (mục 7) | 👤 | Dừng nếu cần nâng cấp mà chưa duyệt ngân sách |
| 2 | Tạo Supabase project `viettable-qa` (cùng region với staging); tạo bucket ảnh; tải CA (Database → Settings → SSL) | 👤 | Ghi mật khẩu DB vào trình quản lý mật khẩu, **không gửi qua chat** |
| 3 | Tạo schema + role runtime cho 5 service trên DB QA | 🔒 | Trước đây làm bằng `tools/SelfRestaurant.CloudPreparation` (action `Roles`/`Migrate`) — **đang bị cấm**. Cần duyệt: (a) cho phép chạy tool **chỉ với DB QA**, hoặc (b) Claude soạn SQL để anh chạy trong Supabase SQL Editor |
| 4 | Thử nghiệm local trước: chạy CloudHost với Postgres local, `Database__ApplyMigrations=true`, `Database__SeedProfile=Demo` để xem app tự dựng schema + dữ liệu mẫu | 🤖 (khi mở việc) | **Giả thuyết**, chưa kiểm chứng; có test `SelfRestaurant.Postgres.Tests` làm tham chiếu |
| 5 | Sinh **secret mới** cho QA (không sao chép từ staging) | 👤/🔒 | Để token/cookie staging không dùng được ở QA và ngược lại |
| 6 | Tạo Vercel project `viettable-qa`, khai báo biến môi trường (mục 5) | 👤 | Settings → Environment Variables; secret chọn loại *Sensitive* |
| 7 | Deploy commit đã chốt vào project QA | 🔒 | Lệnh deploy chỉ chạy sau khi có duyệt; kiểm tra lại project = `viettable-qa` trước khi chạy |
| 8 | Lần chạy đầu bật migrate/seed, sau đó đặt `Database__ApplyMigrations=false` rồi redeploy | 🔒 | Theo đúng quy tắc của staging: lúc chạy thường app không được sửa schema |
| 9 | Kiểm tra không cần đăng nhập Vercel: `/healthz`, `/readyz`, `/api/mobile/v1/branches` = 200 | 🤖 | Chỉ đọc |
| 10 | Tạo dữ liệu QA (mục 6) | 🔒 | Chỉ trên DB QA |
| 11 | Android: thêm build type `cloudQa` (mục 6.2) + unit test; build | 🤖 (khi mở việc) | Branch riêng, ví dụ `fix/vt-ho-01-cloudqa` |
| 12 | Cài APK QA lên **emulator**; chạy `CloudPreviewConnectivityTest` + đăng nhập QA thủ công | 🤖 emulator | Mật khẩu nhập tay, không qua ADB/chat |
| 13 | (Tùy chọn) Cài APK QA lên Samsung. Gói khác tên (`.qa`) nên **không ghi đè** app đang giữ phiên | 🔒 | Vẫn cần duyệt vì thiết bị đang được bảo vệ |
| 14 | Ghi evidence mới `docs/acceptance/evidence/RUN_<ngày>_VT-HO-01.md` | 🤖 | Không sửa evidence cũ |

**Điều kiện đóng VT-HO-01 (nếu chủ dự án chấp nhận hướng A):** APK `cloudQa` build từ commit đã chốt, trên thiết bị thật
hoặc emulator đi được: health → đăng nhập → Home → menu → lịch sử, **không có Vercel 401** và không chứa secret nào
(quét secret APK PASS); chủ dự án ký chấp nhận rằng QA là môi trường nghiệm thu đại diện cho candidate.

## 5. Cấu hình/secret cần cho project QA (chỉ tên, không ghi giá trị)

Nguồn danh sách: `docs/VERCEL_API_STAGING.md`, `docs/CLOUDHOST.md`, code `CloudHost`/`CloudCompatibility`.

**Cấu hình thường (không bí mật):**
`Database__Provider`, `Database__ApplyMigrations`, `Database__SeedProfile` (`None`/`Reference`/`Demo`),
`CloudHost__EnvironmentNamespace` (**phải khác staging**, ví dụ chứa `qa`), `CloudHost__ExposeStaffWeb`,
`MediaStorage__Provider`, `MediaStorage__Supabase__ProjectUrl`, `MediaStorage__Supabase__Bucket`,
`MobileJwt__Issuer`, `MobileJwt__Audience`, `Realtime__Provider`.
Cần kiểm tra thêm khi làm: `CloudHost__StatelessStaff`, `Services__TimeoutSeconds` (có trong code, chưa thấy trong tài liệu env).

**Secret (sinh mới cho QA):**
`ConnectionStrings__CatalogDb`, `ConnectionStrings__OrdersDb`, `ConnectionStrings__CustomersDb`,
`ConnectionStrings__IdentityDb`, `ConnectionStrings__BillingDb` (transaction pooler + TLS),
`MediaStorage__Supabase__ServiceKey`, `MobileJwt__SigningKey`, `CloudHost__DataProtectionCertificate`
(PFX Base64), `CRON_SECRET` (endpoint recovery thủ công).

**Không cấu hình ở QA** trừ khi được yêu cầu: Google Sign-In, VNPAY. Không bật Vercel Cron.

**File không bí mật đi kèm image:** CA của Supabase QA (thay cho `certs/supabase-ca.crt` của staging, hoặc
xác nhận cùng CA). Cần kiểm tra Dockerfile có đường dẫn CA cố định không.

## 6. Dữ liệu QA và thay đổi Android

### 6.1 Dữ liệu QA

- Nền: seed `Demo` hoặc `Reference` (chi nhánh, bàn, menu, vai trò). Nếu cần menu giống thật (Quận 1: 10 nhóm / 52 món),
  phải có bước sao chép **chỉ dữ liệu danh mục** từ staging, cần duyệt riêng. Không sao chép khách, đơn, phiên hay hóa đơn.
- Khách: **2 tài khoản QA (A, B)** đăng ký qua API B1 trên QA. Đây cũng là điều kiện để làm VT-HO-02.
- Nhân viên: Chef / Cashier / Admin cho ít nhất 1 chi nhánh, tạo qua API admin của QA.
- Mật khẩu lưu theo cách đang dùng (DPAPI trong `%LOCALAPPDATA%\VietTable\…`), dùng thư mục **riêng cho QA**,
  không ghi vào repo hay tài liệu.

### 6.2 Android — build type `cloudQa` (đề xuất, chưa sửa)

```groovy
// Public QA origin only; no Vercel protection credential.
def cloudQaApiUrl = 'https://<qa-public-host>'   // điền sau bước 9

cloudQa {
    initWith cloudPreview
    matchingFallbacks = ['debug']
    applicationIdSuffix '.qa'          // cài song song, không ghi đè app trên Samsung
    versionNameSuffix '-qa'
    buildConfigField 'String', 'API_BASE_URL', groovy.json.JsonOutput.toJson(cloudQaApiUrl)
}
```

- Tái dùng logic `validateReleaseApiUrl` để chặn http/localhost/candidate URL cho `cloudQa`.
- Nếu có `app_name`, thêm `resValue` để app hiện tên "VietTable QA", tránh nhầm với app thật.
- Test: unit test khẳng định URL của `cloudQa` là HTTPS và không phải host candidate; chạy lại 54 unit test.
- Google Sign-In sẽ không hoạt động với package `.qa` nếu chưa đăng ký. Chấp nhận được với QA.
- Không đổi `ApiClient`, không thêm header hay cookie nào của Vercel.

## 7. Gói dịch vụ

**Cách xem gói Vercel (👤):** đăng nhập vercel.com → góc trên bên trái chọn team/scope `hienkhanhvu981-7145`
→ **Settings → Billing**. Trang này ghi *Hobby* hoặc *Pro*.
**Cách xem gói Supabase (👤):** supabase.com/dashboard → chọn Organization → **Billing** (Free / Pro).

**Hướng A cần gì (cần đối chiếu trang giá chính thức lúc làm, không dựa trên tài liệu này):**
- Project hiện tại đã chạy được backend dạng container trên gói đang có ⇒ project thứ hai *có khả năng* chạy được
  trên cùng gói, nhưng tốn thêm tài nguyên/chi phí. Nếu Vercel yêu cầu nâng cấp hoặc add-on trả phí ⇒ **dừng và hỏi**
  (đúng quy tắc trong `docs/VERCEL_API_STAGING.md`).
- Hướng A **không cần** tính năng "Deployment Protection Exceptions" trả phí nếu deploy làm Production của project QA.
- Lưu ý: gói Hobby của Vercel dành cho mục đích cá nhân, phi thương mại. Nếu VietTable dùng cho nhà hàng thật thì nên
  cân nhắc Pro. Đây là câu hỏi điều khoản, không phải kỹ thuật.
- Supabase Free giới hạn số project đang hoạt động cho mỗi organization, và project Free bị tạm dừng nếu lâu không dùng.
  Kiểm tra còn chỗ cho project thứ hai.

## 8. Nguồn code của candidate và hiện trạng Vercel

### 8.1 Kết quả lệnh Vercel CLI chỉ đọc — 12/10/2026

Lệnh đã chạy (`npx vercel@62.4.0`, đã đăng nhập sẵn, tài khoản `hienkhanhvu981-7145`): `whoami`, `inspect`, `ls`,
`project ls`, `domains ls`. Không chạy lệnh nào khác.

| Mục | Kết quả |
|---|---|
| Project trong tài khoản | `viettable-api`, `portfolio-vu-hien-khanh` (chưa có project QA) |
| Candidate | `dpl_7MxW7oBh8H1DCYmPSefcYX7Hf8fE`, project `viettable-api`, **Preview, Ready**, tạo **12/10/2026 01:35** (giờ VN), build 3m54s, 1 build `services/api/container` (iad1) |
| Stable alias hiện trỏ vào | `dpl_4CiTfSuo4LhKb2fjFJSX4zaWKTgH` (`viettable-o9udbp2p9-…`), **Preview, Ready**, tạo 10/10/2026 19:30. *Khác* với `dpl_7dbGYm…` ghi trong tài liệu cũ `ANDROID_CLOUD_PREVIEW_ACCEPTANCE.md`, tức alias đã được chuyển sau đó |
| Custom domain | 0 (chỉ dùng tên miền `*.vercel.app`) |
| Thông tin Git của deployment | **Không có** (metadata rỗng) ⇒ deploy bằng CLI từ thư mục trên máy, không qua Git |
| Gói Vercel | CLI **không hiển thị** tên gói (chỉ ghi build machine `basic`, `plan-default`). Xem trên dashboard (mục 7) |

### 8.2 Candidate chạy code nào (suy luận có căn cứ, chưa được Vercel xác nhận)

- Lúc 01:35 ngày 12/10, HEAD là `4d4abf6` (01:23). Commit `15799ed` (01:51) và các commit sau ra đời **sau** candidate.
- Thư mục chuẩn bị deploy gần nhất `%TEMP%\VietTable-staff-preview-e9864b42…` (tạo 01:12, link tới `viettable-api`)
  được so bằng hash git với 394 file nguồn backend/web:
  - giống `4d4abf6` ở 393/394 file; file khác duy nhất là `GatewayModule.cs`;
  - `GatewayModule.cs` và `vercel.json` trong thư mục đó **giống hệt các thay đổi chưa commit hiện tại**.
- **Kết luận:** candidate ≈ `4d4abf6` + thay đổi chưa commit ở `GatewayModule.cs` và `deploy/staff-preview/vercel.json`.
  Candidate **không có** thay đổi `OrdersController.cs` của `15799ed`. Giới hạn: không chứng minh được thư mục này
  chính là thư mục đã upload; chỉ trùng thời điểm và cấu hình.
- Hệ quả cho hướng A: QA sẽ deploy từ một commit cụ thể (bước 0), nên **QA sẽ không giống hệt candidate**. Cần ghi rõ
  điều này trong evidence, hoặc commit hóa thay đổi đang dở trước rồi coi commit đó là candidate mới.

### 8.3 Việc chỉ xem trên dashboard (👤)

1. vercel.com → scope `hienkhanhvu981-7145` → project **viettable-api** → **Settings → Deployment Protection**:
   chụp màn hình phần cài đặt, **không bấm thay đổi**.
2. **Settings → Billing** (ở cấp team/tài khoản) để biết gói (mục 7).

## 9. Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| Nhầm project khi deploy → ghi đè staging/stable | Kiểm tra `projectName`/`projectId` = `viettable-qa` trước lệnh deploy; không bao giờ dùng thư mục đã link `viettable-api` |
| Dùng nhầm connection string staging ⇒ QA ghi vào dữ liệu thật | Secret QA sinh mới; kiểm tra host DB = project ref QA trước khi deploy; `/readyz` QA không thấy Session #20 |
| QA lệch cấu hình so với candidate ⇒ kết quả QA không đại diện | Cùng commit, cùng Dockerfile; liệt kê khác biệt (chỉ DB/secret/namespace) trong evidence |
| Endpoint QA công khai bị dò quét | Dữ liệu giả; auth VietTable vẫn bắt buộc; xóa project khi xong |
| Chi phí phát sinh | Kiểm tra gói trước; dừng nếu bị yêu cầu nâng cấp; xóa môi trường khi xong |
| Secret lọt vào APK/log | Quét secret APK như các lượt trước; APK chỉ chứa URL công khai |
| Nhầm app QA và app thật trên thiết bị | `applicationIdSuffix .qa`, tên "VietTable QA" |

## 10. Rollback

Môi trường QA tách hẳn, nên rollback **không đụng stable, candidate hay dữ liệu thật**:

1. 👤 Vercel: project `viettable-qa` → Settings → **Delete Project** (hoặc chỉ xóa deployment).
2. 👤 Supabase: project `viettable-qa` → Settings → **Pause** (giữ dữ liệu) hoặc **Delete** (sau khi lưu evidence).
3. 🤖 Git: revert commit thêm `cloudQa` trên branch riêng (không đụng `main`).
4. 🤖/👤 Gỡ app `com.restaurant.viettable.qa` khỏi emulator; Samsung chỉ gỡ bản `.qa`, **không đụng app gốc**.
5. 👤 Thu hồi/xóa các secret QA và tài khoản QA (chúng chỉ tồn tại ở QA).
