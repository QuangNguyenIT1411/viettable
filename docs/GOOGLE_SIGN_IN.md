# Google Sign-In cho VietTable Android

## Luồng đang triển khai

1. `LoginFragment` mở Google Credential Manager bằng nút **Tiếp tục với Google**.
2. Credential Manager trả Google ID token có audience là Web OAuth client ID của VietTable.
3. Android gửi duy nhất `{ "idToken": "..." }` đến Gateway:
   `POST /api/mobile/v1/auth/google`.
4. Gateway chuyển request sang Identity B1. Identity dùng `Google.Apis.Auth` để xác minh chữ ký,
   issuer/thời hạn chuẩn của Google, audience, email đã xác minh và subject.
5. Identity tạo hoặc tìm customer Google, sau đó phát hành access token và refresh token VietTable.
6. Android lưu cặp token VietTable qua cùng `ApiClient.saveTokenResponse()` như login mật khẩu,
   khởi động SignalR và bootstrap hồ sơ/dữ liệu. Google ID token không được dùng để gọi API VietTable.

Nếu email đã thuộc tài khoản mật khẩu, backend trả `409 email_provider_conflict`; hệ thống không tự
link hoặc đổi provider. Tài khoản Google mới được tạo trong database Identity và JWT luôn mang role
`customer`. Refresh rotation, reuse detection và logout dùng chung implementation B1.

## Cấu hình Google Auth Platform

Credential Manager cần hai loại OAuth client cùng nằm trong một Google Cloud project:

- Web application client: giá trị client ID phải giống ở Android và Identity. Không đặt client secret
  trong app hoặc tài liệu.
- Android client: package `com.restaurant.viettable` và fingerprint của signing certificate.

Trong Google Auth Platform, cấu hình OAuth consent/branding và thêm tài khoản test nếu app còn ở chế độ
Testing. Android client và Web client phải thuộc cùng project; release build phải đăng ký fingerprint của
release signing key riêng, không dùng fingerprint debug.

Fingerprint debug hiện tại:

```text
SHA-1: 25:BE:B1:70:41:22:28:02:1E:AC:3B:25:B5:81:1A:8A:26:33:65:82
SHA-256: 33:9B:2C:D4:CC:D3:3D:46:EC:C6:99:30:53:85:1A:CC:2F:75:94:7D:91:81:24:37:DC:DC:EA:D0:3D:61:94:1E
```

Lấy lại fingerprint của máy/signing key đang dùng:

```powershell
cd D:\Quang\VietTable
.\gradlew.bat :app:signingReport
```

Client ID public của project hiện tại được cấu hình ở `gradle.properties`:

```properties
VIETTABLE_GOOGLE_WEB_CLIENT_ID=85490395082-lgrrtpiskqdnf2d0km1c9v2a20o32vhf.apps.googleusercontent.com
```

`app/build.gradle` đọc Gradle property này vào `BuildConfig.GOOGLE_WEB_CLIENT_ID`;
không còn fallback client cũ trong build script. `LoginFragment` truyền nó cho
`GetSignInWithGoogleOption.Builder(clientId)` trong `GoogleSignInCoordinator`.
Đây là **Web client ID**, không phải Android OAuth client ID. Không cần Client Secret.

Ghi đè Web client ID khi build nếu môi trường khác development:

```powershell
.\gradlew.bat :app:assembleDebug `
  -PVIETTABLE_GOOGLE_WEB_CLIENT_ID="YOUR_WEB_CLIENT_ID.apps.googleusercontent.com"
```

Identity đọc `Authentication:Google:ClientId` (hoặc `SELFRESTAURANT_GOOGLE_CLIENT_ID`). Hai phía phải
dùng cùng Web client ID. Client ID là public configuration; không đưa client secret, refresh token hoặc
service-account key vào source/APK.

Development chạy trực tiếp dùng `SelfRestaurant-master/src/Services/SelfRestaurant.Identity.Api/appsettings.Development.json`.
Docker Compose ánh xạ `SELFRESTAURANT_GOOGLE_CLIENT_ID` sang
`Authentication__Google__ClientId`; default local và `.env.example` đều là client mới ở trên.
Không sửa `.env` thật trong lượt này. Với `dotnet run`, dùng chuẩn ASP.NET
`Authentication__Google__ClientId` để override appsettings; biến legacy
`SELFRESTAURANT_GOOGLE_CLIENT_ID` chỉ được controller dùng khi configuration key không tồn tại.

### Audience riêng cho customer web (theo yêu cầu user)

`Authentication:Google:ClientId` **chỉ cho mobile**. Endpoint Google web cũ trong
`IdentityController` đọc riêng `Authentication:Google:WebClientId` (fallback environment
`SELFRESTAURANT_GOOGLE_WEB_CLIENT_ID` khi key vắng), không fallback sang mobile audience.
Compose ánh xạ biến web này sang `Authentication__Google__WebClientId`.

Audit `703089463102`:

- `src/Frontend/selfrestaurant-customer-web/.env.local`: client Google web riêng, giữ nguyên,
  không commit hoặc sửa file local này.
- Identity `WebClientId` trong Development config, Compose và `.env.example`: giữ **chỉ cho web**
  để không làm mất tương thích khi mobile chuyển client. Giá trị là
  `703089463102-cbuu53j035vbrp8ea1hespobcqpbtll7.apps.googleusercontent.com`.
- `.tmp-build/verify/appsettings.Development.json`: artifact cũ, không sửa và không dùng làm
  nguồn cấu hình. `build/bin/obj/.gradle` không được chỉnh tay.
- Android và mobile controller không có fallback/audience `703089…`. Không nới validation
  thành một danh sách chấp nhận cả hai project.

Web OAuth E2E với account thật chưa được nghiệm thu trong task Android này; việc giữ riêng
audience web đã được kiểm tra bằng source contract và runtime configuration.

Sau khi đổi runtime config, recreate Identity bằng
`docker compose up -d --no-deps --build identity`. Không reset DB/volume. Kiểm tra
**chỉ key audience** trong container, không dump toàn bộ environment chứa secrets.

Nếu build property trống hoặc không đúng định dạng Google client ID, app không mở Credential Manager,
hiện thông báo cấu hình và vẫn cho đăng nhập mật khẩu. Cancel account picker không tạo lỗi backend và
không thay đổi session hiện tại.

## Checklist runtime

- Backend `5100` và Identity `5104` ready; điện thoại thật giữ `adb reverse tcp:5100 tcp:5100`.
- Google Auth Platform đã có Android OAuth client đúng package + SHA fingerprint.
- Nút Google chỉ mở một account picker dù bấm liên tiếp.
- Cancel picker quay lại form, login mật khẩu vẫn hoạt động.
- Token Google sai bị `/api/mobile/v1/auth/google` từ chối và không tạo account/session.
- Login Google thành công vào luồng ứng dụng bình thường; đóng/mở app vẫn giữ phiên VietTable.
- Access token hết hạn tiếp tục dùng refresh rotation B1; logout thu hồi refresh session.

## Acceptance trên Samsung — 2026-09-24

User xác nhận Google Cloud cùng project có Android OAuth client **VietTable Android**,
package/SHA-1 như trên, Web client **VietTable Backend**, app ở **Testing** và Gmail test đã
được thêm. Không tự sửa Google Cloud console. Gmail được chọn qua Google chooser thật;
không dùng password login hoặc token giả làm bằng chứng Google thành công.

Đã xác minh:

- `:app:signingReport`: SHA-1 khớp `25:BE:B1:70:41:22:28:02:1E:AC:3B:25:B5:81:1A:8A:26:33:65:82`.
- Source Gradle property, BuildConfig của APK và `Authentication__Google__ClientId` trong
  Identity container đều bằng `85490395082-lgrrtpiskqdnf2d0km1c9v2a20o32vhf.apps.googleusercontent.com`.
- APK cài bằng `adb install -r`, không clear data; Samsung SM-A528B giữ
  `adb reverse tcp:5100 tcp:5100`, Gateway `http://127.0.0.1:5100`.
- Chooser thật hiển thị → chọn Gmail test → `POST /api/mobile/v1/auth/google` **200**
  → chọn chi nhánh → Home/menu thật. Gateway correlation đầu:
  `android-7425f58e-a463-4060-85a0-dfcb7a81c1dd`.
- Identity ghi customer **5026**, `AuthProvider=Google`, `ExternalProvider=Google`,
  subject không rỗng và có refresh session. ID này chỉ là evidence, không hardcode vào app/test.
- Logout bằng Profile/dialog → Welcome; Google chooser/login lại → Home.
- Test opt-in `GoogleSessionAcceptanceTest` trên **chính phiên Google đó** PASS:
  profile đúng Gmail đã chọn, refresh token thật rotate, customerId không đổi,
  JWT sau refresh kết nối SignalR. Test không inject token và không thay account.
- Token sai trong negative test bị Gateway từ chối **401 `invalid_google_token`**;
  đây chỉ là kiểm tra rejection, không phải bằng chứng Google E2E.

Phát hiện và sửa cục bộ khi audit: mobile tạo username `google_` + GUID (39 ký tự)
nhưng cắt `[..50]`, gây `ArgumentOutOfRangeException` cho Google user mới. Bỏ phép cắt
không hợp lệ; thêm guard subject rỗng. Giữ nguyên Google.Apis.Auth verification và
`409 email_provider_conflict`, không tự link password account.

### Lệnh kiểm chứng

```powershell
.\gradlew.bat :app:assembleDebug :app:testDebugUnitTest :app:assembleDebugAndroidTest -PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100
powershell -ExecutionPolicy Bypass -File .\SelfRestaurant-master\scripts\test-b1.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\test-android-instrumentation.ps1 -Tier All -RunExistingSessionSecurity
```

Build Android + **32 unit tests PASS**. Identity publish/build PASS trong Docker;
Gateway build và **13/13 MobileAuthB1 contract tests PASS** qua SDK Docker 8.0 (host hiện
không có .NET SDK). B1 runtime register/password/refresh/reuse/password change/logout/reset PASS.
Android instrumentation All với existing-session opt-in: **14/14 PASS** (45.692 s),
bao gồm password login/logout, concurrent refresh, profile, SignalR và lifecycle regression.

Sau khi deploy lại Identity với audience web riêng: `/readyz` ready, hai key audience
runtime đúng scope; chạy lại GoogleSessionAcceptanceTest trên Samsung **1/1 PASS** (1.406 s).
**GOOGLE SIGN-IN: PASS** trong phạm vi debug Samsung/local backend.

Sau Google login thật, chạy test riêng dưới đây với Gmail đã chọn (không truyền password/token):

```powershell
adb install -r .\app\build\outputs\apk\androidTest\debug\app-debug-androidTest.apk
adb shell am instrument -w -r -e class com.restaurant.viettable.network.GoogleSessionAcceptanceTest -e runGoogleSessionAcceptance true -e expectedGoogleEmail <GMAIL_TEST> -e expectedGoogleWebClientId 85490395082-lgrrtpiskqdnf2d0km1c9v2a20o32vhf.apps.googleusercontent.com com.restaurant.viettable.test/androidx.test.runner.AndroidJUnitRunner
```

Test này opt-in, không tự chạy vào session cá nhân trong suite All. Nó rotate refresh
token của session thật theo API bình thường, không xóa phiên. Các test regression
password dùng account/store riêng; account test còn trong DB theo cơ chế cleanup hiện có.

### Phạm vi

Acceptance là debug Samsung/local Gateway, không chứng nhận release signing/public HTTPS
hay Google Cloud Publishing. Test-user/Android-client configuration bên Cloud do user
xác nhận; token thật được backend chấp nhận là evidence runtime cho cấu hình đang dùng.

## Khắc phục lỗi mạng trên Samsung — 2026-09-26

### Root cause

Google chooser và Credential Manager không phải điểm lỗi. `app/build.gradle` mặc định tạo
debug `API_BASE_URL=http://10.0.2.2:5100`, địa chỉ dành cho emulator. Trên Samsung, tunnel
`adb reverse tcp:5100 tcp:5100` chỉ làm Gateway khả dụng tại `http://127.0.0.1:5100`.
`scripts/connect-phone.ps1 -OpenApp` trước đây chỉ kiểm tra tunnel bằng `curl` từ thiết bị
rồi mở APK đã cài; nó không đảm bảo APK đó được build/install với URL dành cho điện thoại.
Vì thế tunnel có thể báo PASS trong khi app gọi host emulator cũ, gây `IOException` và
hiện thông báo mạng.

Evidence: trước khi cài APK Samsung build đúng URL, lỗi mạng được tái hiện nhưng Gateway
không có request `/api/mobile/v1/auth/google`. Sau khi build/install với
`-PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100`, cùng Gmail test đi qua các bước credential
parse và POST thành công. Không thay Android OAuth client, SHA-1, Web Client ID hay backend
audience.

### Android fix và error handling

- `connect-phone.ps1 -OpenApp` hiện build debug với Gateway URL `127.0.0.1:5100`, xác minh
  `BuildConfig.API_BASE_URL`, cài APK bằng `adb install -r`, rồi mở app. Luồng emulator giữ
  nguyên mặc định `10.0.2.2:5100`.
- Thêm DEBUG diagnostics chỉ gồm stage, credential type, exception category, HTTP status
  và backend error code. Không ghi ID token, access/refresh token, JWT, email hoặc request body.
- Chỉ `IOException` được phân loại là network error. Exception parse/response bất ngờ có
  message riêng, không còn bị gộp thành “Không thể kết nối”. API errors giữ HTTP status và
  message backend; `email_provider_conflict` hướng người dùng đăng nhập bằng mật khẩu.
- Hủy chooser được xử lý như cancellation, reset trạng thái nút và không hiện network error.

### Nghiệm thu Samsung

- Thiết bị `SM-A528B`, Android 14/API 34, package `com.restaurant.viettable`; `adb reverse`
  được giữ trong lúc test.
- Credential Manager trả `TYPE_GOOGLE_ID_TOKEN_CREDENTIAL`; ID token parse thành công
  (token không được in vào log).
- Android gọi Gateway `POST /api/mobile/v1/auth/google`; Gateway gọi Identity
  `/api/internal/mobile/auth/google`; Identity trả 200. Android lưu VietTable token pair,
  hiện danh sách chi nhánh rồi vào Home sau khi chọn chi nhánh.
- Đã logout, hủy chooser thật: callback là `GetCredentialCancellationException`, nút Google
  trở lại trạng thái sẵn sàng, không có network error. Đăng nhập Google lần tiếp theo cũng
  trả 200 và vào Home.
- Test Gmail đã chọn không gặp provider conflict. Mapping 409
  `email_provider_conflict` được kiểm thử riêng bằng unit test; không xóa hoặc sửa tài khoản
  chỉ để test.

### Lệnh kết nối Samsung

```powershell
cd D:\Quang\VietTable
powershell -ExecutionPolicy Bypass -File .\scripts\connect-phone.ps1 -OpenApp
```

Lệnh này giờ build/install APK phù hợp cho thiết bị thật; không cần tự nhớ thêm Gradle
property `VIETTABLE_API_BASE_URL`. Build trực tiếp thủ công vẫn phải truyền property đó khi
target là Samsung.

Nguồn đối chiếu: [Android Credential Manager Google button flow](https://developer.android.com/identity/sign-in/credential-manager-siwg-implementation),
[Google backend ID-token verification](https://developers.google.com/identity/sign-in/android/backend-auth).
