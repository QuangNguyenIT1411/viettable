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

Backend vẫn bắt buộc đăng nhập VietTable cho mọi API cần quyền.

### 3.1 Làm sao để QA không bị Vercel 401 (chính là lỗi VT-HO-01)

Theo tài liệu Vercel, Deployment Protection được **cài riêng cho từng project**. Project `viettable-api` đang dùng
*Standard Protection*: chặn mọi URL **trừ domain Production**. Một project mới cũng sẽ nhận Standard Protection
(theo mặc định của team). Có 3 cách để project `viettable-qa` truy cập công khai:

| Cách | Làm gì | Ưu | Nhược |
|---|---|---|---|
| **Q1 (khuyến nghị)** — Deploy làm **Production** của `viettable-qa` | Không đổi cài đặt protection nào. Domain Production (dạng `viettable-qa.vercel.app`) công khai sẵn theo Standard Protection | Không phải "mở khóa" gì, không có cài đặt nào phải nhớ hoàn tác | Instance tắt sau **5 phút** không có request (Preview là 30 giây), tốn hạn mức bộ nhớ hơn một chút. Chấp nhận được vì QA chỉ bật khi test |
| Q2 — Deploy **Preview** + **Deployment Protection Exception** cho đúng 1 alias QA | Chủ dự án thêm alias QA vào *Deployment Protection Exceptions* của `viettable-qa`. Có trên mọi gói, không tốn phí. Giống cách stable đang được mở | Tắt sau 30 giây, tiết kiệm hạn mức | Mỗi lần deploy phải gán lại alias (lệnh `alias`, hiện bị cấm, cần duyệt); thêm một cài đặt phải nhớ gỡ |
| Q3 — Tắt Vercel Authentication cho cả project `viettable-qa` | Settings → Deployment Protection của `viettable-qa` → tắt | Đơn giản | **Mọi** URL QA công khai, phạm vi rộng nhất; không khuyến nghị |

> **Đính chính:** ở phiên trước tôi gợi ý "deploy QA dạng Preview để tiết kiệm bộ nhớ" mà không nói rằng Preview sẽ
> bị 401 nếu không có Exception (cách Q2). Kế hoạch chọn **Q1**. Chỉ chuyển sang Q2 nếu hạn mức bộ nhớ (mục 7) bị căng.

**Cam kết không thay đổi `viettable-api`:**
- Mọi thao tác Q1/Q2/Q3 chỉ làm trong trang Settings của project **`viettable-qa`**. Trước khi bấm, kiểm tra tên project ở đầu trang.
- **Không** đổi *Team Settings → Deployment Protection* (cài đặt mặc định của team). Theo tài liệu, nó chỉ áp dụng cho
  project mới, nhưng vẫn không đụng tới.
- Kiểm tra trước và sau khi dựng QA (chỉ đọc):
  1. 👤 Chụp màn hình *Settings → Deployment Protection* của `viettable-api`, so với ảnh chụp ngày 12/10. Phải giống hệt.
  2. 🤖 `vercel inspect` stable alias: vẫn trỏ `dpl_4CiTfSuo4LhKb2fjFJSX4zaWKTgH` (hoặc bản anh đã duyệt sau này).
  3. 🤖 Stable `/healthz` không cần đăng nhập Vercel: 200. Candidate không đăng nhập: vẫn 401.

## 4. Các bước

| # | Bước | Ai | Ghi chú |
|---|---|---|---|
| 0 | Chốt **commit nguồn** cho QA: commit hóa thay đổi đang dở hoặc chọn `103bebb`. Không deploy từ working tree bẩn | 👤 quyết định, 🤖 chuẩn bị | Liên quan VT-HO-07 |
| 1 | Kiểm tra gói Vercel và Supabase (mục 7) | 👤 | **Đã làm 12/10:** Vercel Hobby, Supabase Free; số project Supabase: chờ chủ dự án báo |
| 1b | **Xem Vercel Usage và đặt ngưỡng dừng** (mục 7.3) | 👤 | Bất kỳ chỉ số nào ≥ 50% hạn mức tháng thì **không dựng QA** tháng đó |
| 1c | Supabase: tổng project đang hoạt động (không *Paused*) ở mọi organization phải **< 2** | 👤 | Không được tạm dừng project staging để lấy chỗ |
| 2 | Tạo Supabase project `viettable-qa` (cùng region với staging); tạo bucket ảnh; tải CA (Database → Settings → SSL) | 👤 | Ghi mật khẩu DB vào trình quản lý mật khẩu, **không gửi qua chat** |
| 3 | Tạo schema + role runtime cho 5 service trên DB QA | 🔒 | Trước đây làm bằng `tools/SelfRestaurant.CloudPreparation` (action `Roles`/`Migrate`) — **đang bị cấm**. Cần duyệt: (a) cho phép chạy tool **chỉ với DB QA**, hoặc (b) Claude soạn SQL để anh chạy trong Supabase SQL Editor |
| 4 | Thử nghiệm local trước: chạy CloudHost với Postgres local, `Database__ApplyMigrations=true`, `Database__SeedProfile=Demo` để xem app tự dựng schema + dữ liệu mẫu | 🤖 (khi mở việc) | **Giả thuyết**, chưa kiểm chứng; có test `SelfRestaurant.Postgres.Tests` làm tham chiếu |
| 5 | Sinh **secret mới** cho QA (không sao chép từ staging) | 👤/🔒 | Để token/cookie staging không dùng được ở QA và ngược lại |
| 6 | Tạo Vercel project `viettable-qa`, khai báo biến môi trường (mục 5) | 👤 | Settings → Environment Variables; secret chọn loại *Sensitive* |
| 7 | Deploy commit đã chốt (branch `qa/vt-ho-01`) vào project QA, dạng **Production của `viettable-qa`** (cách Q1, mục 3.1) | 🔒 | Lệnh deploy chỉ chạy sau khi có duyệt; kiểm tra lại project = `viettable-qa` trước khi chạy |
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

## 7. Gói dịch vụ và giới hạn

Hiện trạng (chủ dự án xác nhận 12/10/2026): **Vercel Hobby**, **Supabase Free**. Số liệu dưới đây lấy từ tài liệu
chính thức ngày 12/10/2026; phải kiểm tra lại trước khi làm, vì nhà cung cấp có thể thay đổi.

### 7.1 Chi phí

**0 đồng.** Hobby và Supabase Free đều miễn phí, không gắn thẻ. Nếu Vercel hoặc Supabase yêu cầu nâng cấp hay add-on
trả phí ⇒ **dừng và hỏi** (đúng quy tắc trong `docs/VERCEL_API_STAGING.md`). Exception (cách Q2) không cần add-on trả phí.

### 7.2 Giới hạn

| Giới hạn | Giá trị | Ảnh hưởng tới QA |
|---|---|---|
| ⚠ Hạn mức Hobby/tháng **dùng chung cả tài khoản**: Active CPU 4 giờ, Provisioned Memory 360 GB-giờ, 1.000.000 lượt gọi function, 100 GB Fast Data Transfer, 10 GB Fast Origin Transfer | Vượt ⇒ thường phải **chờ 30 ngày** mới dùng lại | **Rủi ro lớn nhất:** QA ăn chung hạn mức với `viettable-api`. Vượt hạn mức thì **stable và app trên Samsung cũng có thể ngừng chạy** |
| Function chạy tối đa 300 giây | — | Kết nối realtime có thể bị ngắt sau 5 phút, app tự kết nối lại (stable đang như vậy) |
| Container runtime | Beta, có trên mọi gói, tính phí theo Active CPU | Dùng được trên Hobby |
| Container tắt khi không có request | Production 5 phút, Preview 30 giây | Khi tắt thì không tốn CPU; bộ nhớ chỉ tính lúc instance còn sống |
| Project / deploy / build | 200 project; 100 deploy/ngày; 1 build cùng lúc; build ≤ 45 phút; upload CLI ≤ 100 MB | Đủ (lần upload trước khoảng 21 MB, build khoảng 4 phút) |
| Điều khoản Hobby | Chỉ dùng cá nhân, **phi thương mại** | Quyết định của chủ dự án nếu VietTable phục vụ nhà hàng thật |
| Supabase Free: số project | **2 project** đang hoạt động, tính gộp mọi organization mà bạn là Owner/Admin; project *Paused* không tính | Hết chỗ thì chưa dựng QA được |
| Supabase Free: tạm dừng | Project ít hoạt động trong 7 ngày bị tạm dừng; khôi phục trong Studio | QA có thể bị dừng giữa chừng |
| Supabase Free: dung lượng | DB 500 MB/project; Storage 1 GB; băng thông ra 5 GB | Đủ cho dữ liệu QA nhỏ |

Nguồn: [Vercel Hobby](https://vercel.com/docs/plans/hobby), [Vercel Limits](https://vercel.com/docs/limits),
[Container Runtime](https://vercel.com/docs/functions/runtimes/container),
[Deployment Protection](https://vercel.com/docs/deployment-protection),
[Deployment Protection Exceptions](https://vercel.com/docs/deployment-protection/methods-to-bypass-deployment-protection/deployment-protection-exceptions),
[Supabase billing](https://supabase.com/docs/guides/platform/billing-on-supabase),
[Supabase project pausing](https://supabase.com/docs/guides/platform/free-project-pausing).

### 7.3 Ngưỡng dừng theo hạn mức (👤, bước 1b)

1. vercel.com → scope `hienkhanhvu981-7145` → **Usage** ở menu bên trái.
2. Ghi lại phần trăm đã dùng trong chu kỳ hiện tại của: Active CPU, Provisioned Memory, Function Invocations,
   Fast Data Transfer, Fast Origin Transfer. Lưu vào evidence (ảnh chụp hoặc số).
3. Quy tắc:
   - Trước khi dựng QA: chỉ số nào **≥ 50%** ⇒ **không dựng QA** tháng đó.
   - Sau mỗi buổi test: xem lại. Chỉ số nào **≥ 70%** ⇒ dừng test QA, xóa deployment QA, chờ chu kỳ sau.
   - Không bao giờ để QA làm stable bị ngừng.

### 7.4 Nguyên tắc vận hành QA

- **Chỉ bật QA khi đang test.** Xong buổi test thì xóa deployment QA (👤 Deployments → ⋯ → Delete) hoặc xóa project.
  Lần sau cần thì deploy lại từ commit đã chốt.
- Lý do: khi không có request, container tự tắt và hầu như không tốn hạn mức. Nhưng để URL công khai nằm đó vẫn bị
  dò quét (tốn lượt gọi) và giữ một bề mặt tấn công không cần thiết.
- Supabase QA có thể để tự tạm dừng sau 7 ngày (không mất dữ liệu). Khôi phục khi cần.

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
