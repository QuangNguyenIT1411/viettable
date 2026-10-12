# Bật đăng nhập Google cho app Android trên Production mới

> **Trạng thái: KẾ HOẠCH, CHƯA THỰC THI.** Lập 12/10/2026, chỉ đọc code. Nhãn: 👤 chủ dự án tự làm · 🔒 cần duyệt ·
> 🤖 Claude làm.
> Production: `https://viettable-beta.vercel.app` (team `viet-table`, project `viettable`).

## 1. Kết luận: lỗi nằm ở backend, không phải APK

Câu "**Chưa cấu hình Google Client ID cho hệ thống.**" do **backend** trả về (HTTP 503, mã `google_not_configured`), tại
`SelfRestaurant-master/src/Services/SelfRestaurant.Identity.Api/Controllers/MobileAuthController.cs:107-110`:

```csharp
var clientId = _configuration["Authentication:Google:ClientId"]
    ?? Environment.GetEnvironmentVariable("SELFRESTAURANT_GOOGLE_CLIENT_ID");
if (string.IsNullOrWhiteSpace(clientId))
    return Error("google_not_configured", "Chưa cấu hình Google Client ID cho hệ thống.", 503);
```

Nếu thiếu cấu hình **phía Android**, app sẽ báo câu khác: "Google Sign-In chưa được cấu hình cho bản build này."
(`app/src/main/java/com/restaurant/viettable/ui/auth/GoogleSignInCoordinator.java:43-46`). App hiện báo câu của backend.
Từ đó suy ra (chưa kiểm tra trực tiếp): phía Android đã chạy được và Google đã trả mã đăng nhập (ID token) cho app. Chỉ
có backend là chưa biết Client ID để kiểm tra mã đó.

## 2. Client ID được lấy từ đâu

| Nơi | Tên biến chính xác | Hiện trạng |
|---|---|---|
| **Android** (build config, cố định trong APK lúc build) | Gradle property `VIETTABLE_GOOGLE_WEB_CLIENT_ID` (`gradle.properties`) → `BuildConfig.GOOGLE_WEB_CLIENT_ID` (`app/build.gradle:53-54`) | ✅ Đã có: `85490395082-lgrrtpiskqdnf2d0km1c9v2a20o32vhf.apps.googleusercontent.com`. Test `GoogleSignInConfigTest.buildUsesConfiguredGoogleClient` đạt |
| **Backend – app mobile** (`POST /api/mobile/v1/auth/google`) | Cấu hình `Authentication:Google:ClientId`, tức **biến môi trường `Authentication__Google__ClientId`**. Dự phòng: `SELFRESTAURANT_GOOGLE_CLIENT_ID` | ❌ **Thiếu** trên Vercel. Project cũ cũng không có biến này (20 biến, mục 1a của `DEPLOY_NEW_VERCEL.md`), nên trên stable cũ đăng nhập Google cũng chưa từng chạy |
| Backend – web khách hàng (`IdentityController`) | `Authentication__Google__WebClientId` | Không liên quan: web khách hàng không được deploy trong cấu hình staff-preview |
| Web khách hàng (Vite) | `VITE_GOOGLE_CLIENT_ID` | Không liên quan (như trên) |

- **Không có API cấu hình:** app không hỏi server Client ID; giá trị được cố định trong APK.
- **Tại sao CloudHost đọc được biến:** CloudHost đọc toàn bộ biến môi trường (`AddEnvironmentVariables`) và chuyển cho mọi module,
  kể cả Identity (`VietTable.CloudHost/Program.cs:22,45`). Không cần sửa code.
- **Backend dùng Client ID nào:** dùng **đúng Client ID mà Android dùng** (Web client `85490395082-…`), vì Google ghi Client ID này vào
  trường `aud` của ID token. `docker-compose.yml:119` cũng dùng giá trị này cho `Authentication__Google__ClientId`.
- **Client ID không phải bí mật:** Client ID (khác với Client *secret*) là thông tin công khai, đã nằm sẵn trong repo và trong APK.
  Không cần đánh dấu Sensitive.

## 3. OAuth client trên Google Cloud Console

Cơ chế Credential Manager / Sign in with Google cần **2 OAuth client trong cùng một Google Cloud project**:

| Client | Dùng để | Thông tin |
|---|---|---|
| **Web application** | Là "Client ID" mà app gửi lên Google và backend kiểm tra | `85490395082-lgrrtpiskqdnf2d0km1c9v2a20o32vhf.apps.googleusercontent.com` (đã có) |
| **Android** | Google kiểm tra app đúng gói + đúng khóa ký | Package `com.restaurant.viettable`; SHA-1 của **khóa debug trên máy này** (APK `cloudProd` và `cloudPreview` đều ký bằng khóa này, đã kiểm tra bằng `apksigner`): `25:BE:B1:70:41:22:28:02:1E:AC:3B:25:B5:81:1A:8A:26:33:65:82` |

- **Ghi chú (suy luận):** Google đã trả token cho app. Thông thường điều đó nghĩa là Android client với package + SHA-1 trên **đã tồn tại**
  trong project `85490395082`, nên **có thể không cần tạo gì trên Google Cloud**. Bước 4.1 chỉ để xác nhận.
- **Nếu đổi khóa ký sau này** (bản release ký bằng keystore riêng, hoặc build trên máy khác): phải thêm SHA-1 mới vào Android client.
  Lấy SHA-1 bằng:
  `D:\Quang\Android\build-tools\36.0.0\apksigner.bat verify --print-certs <apk>`

## 4. Các bước

| # | Bước | Ai | Build lại APK? | Deploy lại? |
|---|---|---|---|---|
| 4.1 | (Xác nhận, không bắt buộc) Kiểm tra trên Google Cloud Console | 👤 | Không | Không |
| 4.2 | Thêm biến `Authentication__Google__ClientId` vào Production của project `viettable` | 👤 | Không | — |
| 4.3 | Kiểm tra tên biến (`env ls`, chỉ tên): 20 biến | 🤖 | — | — |
| 4.4 | **Deploy lại Production** (biến mới chỉ có hiệu lực với deployment mới). Nên gom với việc 3.3 (trang chủ `/`) trong `DEPLOY_NEW_VERCEL.md` mục 2b | 🔒 🤖 | Không | **Có** |
| 4.5 | Kiểm tra `inspect` (region `syd1`) + `/healthz`, `/readyz` | 🤖 | — | — |
| 4.6 | Thử đăng nhập Google trên Samsung (app đang cài, **không cần cài lại**) | 👤 | Không | — |

### 4.1 (👤) Xác nhận trên Google Cloud Console — chỉ xem, không bắt buộc

1. Mở https://console.cloud.google.com bằng tài khoản Google **đã tạo** Client ID `85490395082-…`.
2. Ở thanh trên cùng, chọn project có số **85490395082**.
3. Menu ☰ → **APIs & Services** → **Credentials**.
4. Mục **OAuth 2.0 Client IDs** phải có 2 dòng:
   - Một dòng loại **Web application**, Client ID bắt đầu bằng `85490395082-lgrr…`.
   - Một dòng loại **Android**: bấm vào, kiểm tra **Package name** = `com.restaurant.viettable` và **SHA-1** =
     `25:BE:B1:70:41:22:28:02:1E:AC:3B:25:B5:81:1A:8A:26:33:65:82`.
5. **Nếu thiếu dòng Android:** bấm **+ Create credentials** → **OAuth client ID** → Application type **Android** → nhập Package name và
   SHA-1 ở trên → **Create**.
6. **Nếu không vào được project này** (không biết tài khoản nào tạo): báo Claude. Khi đó phải tạo bộ client mới và **build lại APK** với
   `VIETTABLE_GOOGLE_WEB_CLIENT_ID` mới. Sẽ lập kế hoạch riêng.

### 4.2 (👤) Thêm biến trên Vercel

1. Mở https://vercel.com → team **VietTable** → project **viettable** → **Settings** → **Environment Variables**.
2. Bấm thêm biến mới:
   - **Key:** `Authentication__Google__ClientId` (hai dấu gạch dưới ở mỗi chỗ, viết hoa đúng như vậy)
   - **Value:** `85490395082-lgrrtpiskqdnf2d0km1c9v2a20o32vhf.apps.googleusercontent.com`
   - **Environments:** chỉ tick **Production**
   - **Sensitive:** để tắt (Client ID là công khai)
3. Bấm **Save**. Nếu Vercel hỏi "Redeploy?" thì bấm **không** / bỏ qua. Claude sẽ deploy lại ở bước 4.4 sau khi anh duyệt.
4. Báo Claude: "đã thêm biến Google".

### 4.6 (👤) Thử lại

Mở app trên Samsung → **Đăng nhập bằng Google** → chọn tài khoản.

- **Thành công:** vào được app.
- **Báo "Không thể xác thực tài khoản Google":** Client ID trên Vercel khác Client ID trong APK. Báo Claude.
- **Vẫn báo "Chưa cấu hình…":** deployment mới chưa nhận biến. Báo Claude.

## 5. Rủi ro / lưu ý

- Đăng nhập Google **tạo hoặc liên kết tài khoản khách** trong DB dùng chung. Đây là dữ liệu thử, chấp nhận được.
- Deploy lại: tốn Build CPU (khoảng 2 phút), không tốn Active CPU; có một lần khởi động lạnh.
- Không sửa code backend hay Android cho việc này.
