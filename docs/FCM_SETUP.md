# Bật thông báo đẩy FCM cho VietTable (chưa làm, chủ dự án tự quyết)

Hiện tại (06/11/2026) app **không dùng FCM**. Thông báo "Có bàn cho bạn" của danh sách chờ chạy như sau:

- Khi có bàn trống (khách huỷ, nhà hàng từ chối, khách không đến), server giữ bàn cho người chờ đầu tiên vừa chỗ
  trong 10 phút (`Waitlist:OfferMinutes`), ghi vào hộp thông báo trong app.
- App có `WaitlistPollWorker` (WorkManager) hỏi server **15 phút/lần** khi khách còn ở danh sách chờ; có lời mời thì
  hiện thông báo trên điện thoại. 15 phút là mức thấp nhất Android cho phép với việc chạy định kỳ, nên khách
  có thể thấy thông báo muộn hơn 10 phút giữ bàn. Mở app hoặc màn "Danh sách chờ của tôi" thì thấy ngay.
- Vercel không có tác vụ nền: hết 10 phút thì lượt giữ chỉ được đóng (và chuyển cho người tiếp theo) khi có
  ai đó đọc danh sách chờ, đặt bàn hoặc thu ngân mở bảng.

Muốn khách nhận ngay trong vài giây thì cần FCM. Các bước (Claude **không** tự tạo dự án Firebase):

1. Chủ dự án vào https://console.firebase.google.com, tạo dự án (ví dụ `viettable`), thêm app Android với
   package `com.restaurant.viettable`, tải `google-services.json`.
2. Đặt `google-services.json` vào `app/` (file này **không phải bí mật tuyệt đối** nhưng vẫn không commit lên
   repo công khai; thêm vào `.gitignore`).
3. Trong Firebase → Project settings → Service accounts → tạo khoá riêng (JSON). Đây là **bí mật**: chỉ đặt vào
   biến môi trường Vercel (ví dụ `Fcm__ServiceAccountJson`, Production), không bao giờ vào APK hay repo.
4. Báo Claude để làm phần code (ước tính 1 phiên):
   - App: thêm plugin `com.google.gms.google-services` + `firebase-messaging`, `FirebaseMessagingService`
     gửi token lên `POST /api/mobile/v1/devices` khi đăng nhập, xoá khi đăng xuất.
   - Backend: bảng `customers."DeviceTokens"` (migration + GRANT), gửi FCM khi tạo lời mời danh sách chờ,
     nhắc lịch, đơn món xong. Lỗi gửi FCM không được làm hỏng việc chính (vẫn còn hộp thông báo + WorkManager).
5. Giữ `WaitlistPollWorker` làm dự phòng khi máy tắt Google Play Services hoặc chưa cấp quyền thông báo.
