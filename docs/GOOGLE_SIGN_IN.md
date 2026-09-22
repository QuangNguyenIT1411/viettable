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

Ghi đè Web client ID khi build nếu môi trường khác development:

```powershell
.\gradlew.bat :app:assembleDebug `
  -PVIETTABLE_GOOGLE_WEB_CLIENT_ID="YOUR_WEB_CLIENT_ID.apps.googleusercontent.com"
```

Identity đọc `Authentication:Google:ClientId` (hoặc `SELFRESTAURANT_GOOGLE_CLIENT_ID`). Hai phía phải
dùng cùng Web client ID. Client ID là public configuration; không đưa client secret, refresh token hoặc
service-account key vào source/APK.

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
