# Android testing

Kiểm chứng gần nhất: 2026-09-22, Samsung SM-A528B Android 14, Gateway local qua ADB reverse.

## Test pyramid và tiers

### Local JVM — `app/src/test`

Logic không phụ thuộc Android: QR payload parser, split amounts/model, review input, realtime event policy, Google config, TokenVault migration/failure ordering và state model. Chạy bằng `:app:testDebugUnitTest`; hiện có **27 test**.

### Instrumentation smoke

- `AppLaunchSmokeTest`: cold launch signed-out → Welcome/Login; secure authenticated session → Home; branch/menu thật non-empty; background/foreground giữ canonical data và SignalR reconnect.
- `SecureTokenStoreDeviceTest`: AndroidKeyStore thật, migration, IV rotation, corruption, missing key, logout clear và debug network policy.

Các class/method smoke có marker `@SmokeTest`. Smoke backend dùng test account riêng, không đọc/xóa session chính.

### Backend-connected integration

- `AuthUiIntegrationTest`: password login thật, Activity recreate trước submit, chọn branch thật, menu canonical, encrypted blob; invalid login hiển thị lỗi backend; logout clear secure session và dừng SignalR.
- `FaqUiIntegrationTest`: Home → Profile → Support; so sánh số FAQ UI với Gateway, kiểm tra question/answer non-empty; tạo lại màn và đảm bảo không duplicate.
- `QrManualUiIntegrationTest`: camera denied trước khi runner chạy, manual input vẫn hoạt động; QR sai bị backend từ chối và scan lại được; text qua Activity recreate; QR fixture thật điều hướng `WALK_IN_AVAILABLE`, không tự tạo session.
- `AuthSecurityRuntimeTest`: refresh rotation/concurrency và retry authenticated request bằng store/account riêng. Test đọc session người dùng là opt-in riêng.

Các test này mang `@BackendIntegrationTest`, gọi duy nhất `BuildConfig.API_BASE_URL`/Gateway từ Android. Script chỉ dùng Catalog read-only trên host để khám phá QR hiện hữu vì mobile table-layout không trả `qrCode`; giá trị được truyền dưới dạng instrumentation argument và không in ra. Không có direct microservice call trong app/test APK.

### Manual hoặc fixture-dependent

Chưa tự động hóa trong default suite:

- Camera decode QR vật lý; Google account chooser/E2E; VNPAY hosted page/IPN/webhook.
- Reservation create/change/cancel, check-in tạo dining session, order/service request, checkout/onsite, split bill và review CRUD.

Các flow nghiệp vụ cuối cần fixture có lifecycle/cleanup API công khai. Hiện chưa có account-delete/session-close/cashier transition tổng quát đủ để suite tự dọn mà không dùng SQL hoặc reset volume. Không tạo mock, không giả invoice/payment success và không skip im lặng bên trong default suite; chúng giữ trong checklist manual đến khi có fixture API an toàn.

## Isolation và đồng bộ

- UI tests thay `ApiClient` singleton **chỉ trong process instrumentation** bằng client có `SecureTokenStore`/Keystore alias riêng. Store thật của người dùng không được clear, logout hoặc đưa vào assertions. Sau mỗi test Activity đóng, SignalR dừng, test refresh token được revoke, key/preferences riêng bị xóa và singleton thật được phục hồi.
- Account có thể truyền bằng `-TestUsername/-TestPassword`; nếu bỏ trống, script sinh credential ngẫu nhiên trong RAM, register qua Gateway và không in giá trị. Do chưa có public delete-account API, identity row tên prefix `android_ui_` còn lại; mọi refresh session được logout. Không SQL DELETE/truncate/reset volume.
- `UiWaiter` là Espresso `IdlingResource`, poll nhẹ trên main looper và kết thúc khi Fragment/view/canonical adapter đạt trạng thái mong đợi. Không rải `Thread.sleep`.
- Recycler branch action thao tác child trong row đầu của danh sách canonical; test không assert tên/position dữ liệu cố định.
- QR code không hardcode. Script khám phá fixture hoặc nhận `-QrCode`.
- Script dùng `adb install -r` cho cả APK, không gọi `connectedAndroidTest`, Test Orchestrator, uninstall hoặc `pm clear`.

## Prerequisites

- Docker services/Gateway chạy và `http://localhost:5100/readyz` trả `{"status":"ready"}`.
- Một Samsung/emulator authorized bởi ADB. Nếu nhiều thiết bị, truyền `-Serial`.
- JDK tại `JAVA_HOME` hoặc `D:\Quang\Android1\jbr`.
- Physical device dùng `adb reverse tcp:5100 tcp:5100`; emulator build dùng `10.0.2.2`.

## Commands

Build/unit/test APK:

```powershell
cd D:\Quang\VietTable
$env:JAVA_HOME='D:\Quang\Android1\jbr'
.\gradlew.bat :app:assembleDebug :app:testDebugUnitTest :app:assembleDebugAndroidTest `
  -PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100
```

Safe default suite trên thiết bị duy nhất:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-android-instrumentation.ps1 -Tier All
```

Chọn tier/device hoặc account CI do người vận hành cấp:

```powershell
.\scripts\test-android-instrumentation.ps1 -Tier Smoke -Serial R5CRC14LFYD
.\scripts\test-android-instrumentation.ps1 -Tier Integration `
  -TestUsername $env:VIETTABLE_TEST_USERNAME -TestPassword $env:VIETTABLE_TEST_PASSWORD
.\scripts\test-android-instrumentation.ps1 -Tier Security
```

`-SkipBuild` chỉ dùng khi cả hai APK đã build với đúng API URL. `-QrCode` thay fixture discovery. `-RunExistingSessionSecurity` cho phép đúng một test đọc secure session đang có; không dùng trong CI/default vì test này không còn hoàn toàn isolated.

Không chạy `connectedAndroidTest`/UTP trên điện thoại chứa phiên cần giữ: tooling trước đây đã uninstall target APK và xóa data. Runner an toàn là cài bằng `adb install -r` rồi `am instrument` với class list như script.

## Runner behavior và diagnostics

Script:

1. chọn đúng một device hoặc serial cụ thể;
2. kiểm tra Gateway ready;
3. đặt reverse/build URL theo physical/emulator;
4. build unit + debug + test APK;
5. cài bằng `install -r`;
6. tạo/reuse test account và QR fixture;
7. lưu trạng thái CAMERA, revoke trước QR permission test, khôi phục trong `finally`;
8. chạy class list theo tier và trả exit code khác 0 nếu build/runner/assertion lỗi.

Khi fail, artifact được ghi vào `artifacts/android-tests/<timestamp>/`: output instrumentation, current Activity, logcat đã giới hạn tag và screenshot. Không thu shared preferences, Authorization header hoặc token. Artifact là dữ liệu chẩn đoán local, không commit.

## Flaky-test policy

- Không tăng sleep để che lỗi. Xác định điều kiện đang thiếu: callback, Fragment transaction, adapter hoặc lifecycle.
- Selector dùng resource ID/Fragment type; text chỉ dùng khi đó là business-visible outcome/dialog ổn định.
- Mọi mutation phải có cleanup API hợp lệ. Nếu environment thiếu fixture, chuyển test sang documented manual tier thay vì fake/SQL cleanup.
- External Google/VNPAY không thuộc default runner.

## Kết quả baseline 2026-09-22

- Build debug, unit và androidTest APK: PASS.
- Unit: 27/27 PASS.
- Samsung `All`: runner báo `OK (11 tests)`; 10 thực thi PASS, 1 assumption-skip (`existingEncryptedSession...`) vì không bật opt-in.
- App launch/auth/branch-menu/FAQ/QR/SignalR/security: PASS, không crash/ANR.
- QR permission được khôi phục; ADB reverse được giữ. Secure blob phiên chính còn tồn tại, legacy plaintext keys không xuất hiện.
- Một lần chạy trung gian fail do selector branch khớp hai card cùng hiển thị; selector đã đổi sang Recycler row action và baseline cuối PASS. Failure artifact local được tạo đúng cơ chế.
