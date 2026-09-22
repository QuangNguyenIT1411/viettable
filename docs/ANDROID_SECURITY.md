# Android token security và HTTPS release

Kiểm chứng ngày 2026-09-22 trên Samsung SM-A528B (Android 14).
**HTTPS release enforcement implemented / production deployment externally blocked.**
Chưa có hostname Gateway production/certificate/termination topology được xác nhận.

## Token architecture

- `ApiClient` là cửa vào duy nhất cho token, dùng `SecureTokenStore`; repository, password/Google login, bootstrap và SignalR dùng cùng nguồn này.
- `SecureTokenStore` dùng AndroidKeyStore alias `viettable.auth.aes.v1`, AES-256/GCM/NoPadding, authentication tag 128 bit, IV ngẫu nhiên do Cipher tạo. Khóa không export, không yêu cầu biometric.
- Access + refresh được serialize thành một cặp, encrypt thành **một blob**, commit đồng bộ cùng migration marker; không ghi hai token rời nhau.
- Preference `viettable_secure_session.xml`: `auth_blob` có format `v1:<base64 IV>:<base64 ciphertext+tag>`, AAD `viettable.auth.aes.v1:auth_pair:v1`; `migration_complete` không chứa secret.
- `TokenVault` tách giao thức persistence/migration khỏi API Android để kiểm thử failure ordering bằng AES-GCM thật trên JVM. Device test dùng Keystore thật, không mock encryption.
- Không persist Google ID token. Google/password/register cùng gọi `tokenCallback` và chỉ xác nhận login khi lưu secure pair thành công.
- AES bảo vệ dữ liệu khi lưu; không bảo vệ token khỏi thiết bị/root/process đã bị chiếm quyền. Token vẫn phải hiện diện tạm thời trong RAM để gửi request.

## Migration, corruption, logout

1. Secure blob tồn tại: decrypt/validate, bỏ legacy; không đọc plaintext làm fallback.
2. Marker đã migrate nhưng không blob: giữ trạng thái signed out; xóa legacy còn sót.
3. Lần đầu có đủ token legacy trong `viettable_session.xml`: encrypt, commit, đọc/decrypt kiểm tra bằng nhau, **sau đó** xóa `access_token` và `refresh_token` cũ.
4. Write/readback thất bại trước verify: không sử dụng token ở process hiện tại, rollback secure marker/blob và giữ legacy để lần khởi động sau thử lại. Không xóa bản gốc trước verify.
5. Blob sai format, Base64/IV/tag hỏng, thiếu/invalidate key: fail closed, xóa auth blob và legacy, hủy key; ApiClient báo invalidation, repository xóa user state/stop SignalR, MainActivity về login khi lifecycle cho phép.
6. Logout giữ flow revoke backend hiện có. Cả success lẫn lỗi mạng đều clear local session. Secure store xóa blob và key để disk cleanup lỗi không tự phục hồi được token bằng khóa cũ. Login tiếp theo tạo key mới.

Không có secure-to-plaintext fallback. Không mã hóa hay xóa các preference nghiệp vụ khác.
Backup vẫn bật cho dữ liệu không nhạy cảm; `backup_rules.xml` và `data_extraction_rules.xml` loại trừ **cả hai** file auth khỏi backup/restore và device transfer. Restore bất thường thiếu key vẫn fail closed.

## Refresh và transport

- REST request ghi nhớ access token thực sự đã gửi. Refresh được synchronize cùng save/clear; nếu token hiện tại khác token bị 401 thì reuse token mới thay vì rotate lần nữa. SignalR truyền token của lần handshake lỗi vào cùng cơ chế.
- Rotation encrypt/persist nguyên cặp mới. Refresh 400/401 invalidates session; lỗi kết nối không tự xóa refresh token hợp lệ.
- ApiClient chỉ gửi request tới đúng scheme/authority Gateway đã cấu hình, không tự follow redirect; release có thêm guard HTTPS. Không gửi Bearer sang URL do response bên ngoài cung cấp.
- SignalR dựng `/hubs/mobile-notifications` từ cùng API base; HTTPS base sử dụng secure transport của thư viện, không hardcode HTTP/ws riêng. Kết nối WSS thực tế chưa test vì chưa có HTTPS Gateway.
- Không thêm TrustAll, hostname bypass, user CA trust hoặc certificate pinning. Network config chỉ tin system trust anchors.
- Log SignalR chỉ giữ trạng thái/ID nghiệp vụ; lỗi transport/auth được phân loại, không in nguyên exception message có thể chứa credential.

## Debug và release

Debug resource overlay chỉ cho HTTP tới `127.0.0.1`, `localhost`, `10.0.2.2` (không subdomain). Các host khác vẫn yêu cầu TLS. Manifest chung cleartext=false; domain exception trong network security config có hiệu lực cho debug minSdk24.

```powershell
.\gradlew.bat :app:assembleDebug :app:testDebugUnitTest -PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100
```

Emulator mặc định dùng `http://10.0.2.2:5100`; điện thoại giữ `adb reverse tcp:5100 tcp:5100`. Không đổi scripts development.

Release không có URL fallback. Cung cấp Gradle property `VIETTABLE_API_BASE_URL` bằng HTTPS origin của **Gateway thật**, không URL microservice. `preReleaseBuild` bắt buộc chạy `validateReleaseApiUrl`: reject thiếu URL, HTTP, local loopback/emulator, userinfo/query/fragment/path ngoài `/`. IPv6 literal cũng bị reject; sử dụng hostname production.

Release network config cấm cleartext toàn bộ, gồm localhost được khai báo tường minh để không hưởng implicit loopback exception trên Android mới. Release debuggable mặc định false, không instrumentation component.

Command kiểm chứng build (không cấu hình production, không cài APK này):

```powershell
.\gradlew.bat :app:assembleRelease -PVIETTABLE_API_BASE_URL=https://developer.android.com
```

URL tài liệu Android này **chỉ dùng kiểm tra build**. APK unsigned sinh ra từ command đó không được deploy/chạy như VietTable release. Không có request/token nào được gửi tới URL này trong kiểm thử. Phải rebuild với Gateway thật và signing/release pipeline trước phát hành.

## Backend readiness audit — không thay backend/.env

- Gateway có HSTS ngoài Development, HTTPS redirection opt-in (mặc định false). Không thấy `UseForwardedHeaders` trong source. Khi triển khai TLS termination phải xác định trusted proxy/network và scheme forwarding, tránh redirect loop; không bật trust forwarded headers từ mọi nguồn.
- Hub mobile đi qua Gateway. HTTP giữa các service trong local Docker là cấu hình nội bộ, không phải URL để Android gọi.
- VNPAY PaymentUrl mặc định sandbox HTTPS, ReturnUrl cấu hình ngoài; `IsConfigured` mới kiểm tra absolute URI, không enforce HTTPS. Phải cấu hình/kiểm chứng Return/IPN public HTTPS thật tại deployment; chưa sửa provider hoặc giả lập callback.
- Password reset có cấu hình `PublicBaseUrl`: cần URL public HTTPS thật khi deploy. Android native không dựa vào CORS; không tự mở CORS wildcard.
- Source config SQL kiểm tra không có password embedded trong connection strings. Development JWT signing key tồn tại dưới cấu hình Development: tuyệt đối không dùng cho production. Giá trị secret không được xuất báo cáo/log và không được chuyển vào Android.
- Google client ID là public identifier, không phải client secret; không tạo token store riêng. Không xác minh Google E2E trong task này.
- `.env` nằm trong ignore rules; không đọc/đổi/rotate nó. Workspace và backend hiện không có Git metadata, do đó không thể kết luận lịch sử secret đã từng tracked hay chưa.

## Bằng chứng kiểm thử

- `assembleDebug`, `testDebugUnitTest`: PASS **27 tests**, gồm 7 tests TokenVault: migrate/restart, write failure, readback failure, corruption/no fallback, lost key, rotation/logout/relogin, failed rotation.
- `assembleRelease` với HTTPS build-only URL: PASS, lintVitalRelease PASS. Không dependency bảo mật deprecated mới.
- Negative validation: missing URL, HTTP loopback, HTTPS localhost/127.0.0.1/10.0.2.2/localhost có dấu chấm cuối đều bị reject.
- Đã kiểm tra merged manifests debug/release và resource XML đóng gói bằng aapt2 (release optimize đổi tên resource): cleartext=false ở release, chỉ ba ngoại lệ ở debug, INTERNET/CAMERA giữ nguyên, không test instrumentation. Release không có thuộc tính debuggable=true (absent = false); Credential Manager components vẫn được giữ.
- Samsung: trước upgrade có hai legacy token keys; `adb install -r` không clear/uninstall. Sau cold launch legacy keys=false, secure blob=true. Authenticated `/api/mobile/v1/me` và SignalR CONNECTED PASS. Force-stop/cold start tiếp tục kết nối; không fatal crash/JWT-like/Bearer log quan sát được trong process đã kiểm tra.
- **4 instrumented tests PASS**: 2 controlled Keystore/network-policy tests và 2 opt-in Gateway runtime tests. Runtime test tài khoản riêng: register/login → invalid access trong isolated test store → 4 REST requests 401 → refresh rotation → retry `/me` thành công → hai late refresh callers reuse cặp hiện tại → reopen secure store → logout → login lại → revoke/clear cuối test.
- Account runtime test tạo qua API có username prefix `auth_security_`; credential chỉ ở RAM. Không xóa dữ liệu backend; tài khoản test còn lại nhưng sessions do test cấp đã logout. Session chính trên Samsung không bị clear.
- Corruption/key loss được thử trên preferences/Keystore alias riêng. Chưa cố tình phá kho token chính hoặc test UI navigation bằng corruption tài khoản thật.

Chạy lại có chủ đích (Gateway local + điện thoại đã login):

```powershell
.\gradlew.bat :app:assembleDebug :app:assembleDebugAndroidTest -PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100
$adb='D:\Quang\Android\platform-tools\adb.exe'
& $adb install -r app\build\outputs\apk\debug\app-debug.apk
& $adb install -r app\build\outputs\apk\androidTest\debug\app-debug-androidTest.apk
& $adb reverse tcp:5100 tcp:5100
& $adb shell am instrument -w -e class com.restaurant.viettable.network.SecureTokenStoreDeviceTest,com.restaurant.viettable.network.AuthSecurityRuntimeTest com.restaurant.viettable.test/androidx.test.runner.AndroidJUnitRunner
```

Không dùng connectedAndroidTest/UTP trên điện thoại cần giữ session: runner có thể uninstall main APK và xóa app data. Runtime suite cần gọi rõ class, có tác động tạo tài khoản test như mô tả.

## Còn lại trước phát hành

Hostname Gateway HTTPS, certificate hợp lệ, proxy allowlist/forwarding, public payment/reset URLs và production secret provisioning/signing. Chưa test HTTPS/WSS end-to-end hoặc cloud/device-transfer restore thực tế. Không fallback release về HTTP để bỏ qua các điều kiện này.

Tham khảo chính thức: [Android network security configuration](https://developer.android.com/privacy-and-security/security-config), [Keystore KeyGenParameterSpec](https://developer.android.com/reference/android/security/keystore/KeyGenParameterSpec), [Android backup rules](https://developer.android.com/guide/topics/data/autobackup).
