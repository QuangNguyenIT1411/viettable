# Final Demo Dry Run — 2026-09-24

## Kết luận

**DEMO READY cho main flow local ONSITE non-split.** Đây là nghiệm thu demo qua Samsung SM-A528B + ADB reverse + Gateway local, **không** phải production HTTPS acceptance. Không thay đổi Android/backend hoặc reset DB. Thao tác Cashier được thực hiện trong UI thật, không gọi API confirm trực tiếp.

## Môi trường và credential

- Docker Desktop, SQL, RabbitMQ, Gateway và Catalog/Orders/Customers/Identity/Billing đều running; `:5100–:5105/readyz` PASS.
- Samsung SM-A528B qua USB, `adb reverse tcp:5100 tcp:5100` PASS. Pin cuối lượt **71%**, USB powered. APK đã cài, mở được; preflight cuối không phát hiện AndroidRuntime crash.
- Customer hiện có `Quang`: password login, authenticated profile PASS; không tạo account mới, không reset mật khẩu. Cashier hiện có role **CASHIER**, scope **chi nhánh Quận 1**: API login và browser Cashier UI PASS. Không dùng tài khoản Admin để xác nhận. Không lưu credential trong repo/tài liệu.
- Preflight cuối với credential runtime: Docker/services/SQL/RabbitMQ/Samsung/reverse/APK/battery/login customer/login Cashier/branch-menu/crash đều PASS, exit 0. Chạy preflight không có credential vẫn báo NOT READY theo thiết kế.

## Fixture chọn ngay trước dry run

`demo-fixture-audit.ps1`: 24 active Dining Session, 22 active Order, 6 Payment PENDING, 3 Split Bill OPEN, 0 upcoming reservation. Không xoá hoặc chỉnh các fixture này. `demo-find-table.ps1` lọc Catalog available và Orders active session/order, đề xuất **Chi nhánh Quận 1, bàn 5, `QR-Q1-B05`**. Gateway QR lookup xác nhận branch 1/table 5; Cashier UI cũng hiển thị bàn 5 trống. Bàn này không được giữ chỗ cho lần demo sau—chạy helper lại.

## Main rehearsal — Samsung + Cashier UI

| Bước | Kết quả/evidence |
|---|---|
| Login → Home/Menu | PASS. Samsung login customer, chọn Chi nhánh Quận 1; Home/menu hiện món và giá backend. |
| QR validate → walk-in | PASS bằng **manual QR**. Màn xác nhận hiển thị đúng `QR-Q1-B05`, bàn 5; check-in tạo Dining Session **3051** (Orders: branch 1/table 5/ACTIVE). Camera mở nhưng không có QR vật lý để chứng minh decode. |
| Order/Tracking | PASS. Samsung gửi 1 Bánh mì ốp la; Order **3055**, Orders lưu 1 món/35.000 VND và app hiển thị đợt gọi. |
| Checkout quote | PASS. Samsung hiển thị subtotal/total **35.000 VND**. App cảnh báo món còn `CONFIRMED`; tiếp tục được xác nhận rõ ràng trong dry run. |
| ONSITE request | PASS. Android chọn CASH tại CASHIER; Billing Payment **2089**, session 3051, **PENDING**, 35.000 VND. |
| Cashier confirm | PASS. Browser Cashier role/branch 1 hiển thị đúng payment code, session code, bàn 5, 35.000 VND, PENDING. Xác nhận qua nút UI và dialog đúng số tiền; hàng chuyển “Đã thu tiền”. Không đụng 6 payment PENDING cũ. |
| Android success/invoice | PASS. Android polling tự chuyển “Thanh toán thành công”, 35.000 VND. Billing Payment 2089 `SUCCEEDED`; Invoice **2037**/35.000 VND. |
| Session closure | PASS. Orders Session 3051 `CLOSED`; authenticated `/dining-sessions/active` sau đó trả 404. |
| Review | PASS. Android tạo review từ Invoice 2037 và màn “Đánh giá của tôi” hiện lại nội dung; Customers Review **2007** gắn invoice 2037/session 3051, rating 5. Nội dung ghi rõ đây là dry run. |

## Chưa nghiệm thu trong lượt này

- Physical QR camera decode: **MANUAL_ACCEPTANCE** (camera đã mở, chưa có mã vật lý trên màn hình thứ hai). Manual QR là fallback hợp lệ và đã PASS.
- SignalR event, service request, Split Bill: optional, không nằm trong main rehearsal này.
- Google OAuth E2E, VNPAY external, public HTTPS, Android release signing: không cần cho demo local; vẫn có blocker riêng.
- Thời lượng main flow chưa được đo bằng đồng hồ bấm giờ; khi tập lại, giữ 8–12 phút và bỏ các phần optional nếu quá thời gian.
- Tài khoản hiện có dùng mật khẩu yếu cho môi trường local; không trình chiếu hoặc commit credential, và nên đổi mật khẩu sau buổi demo.

## Lần trình bày kế tiếp

Không sửa code thêm cho main flow đã PASS. Sạc Samsung >=60% hoặc cắm nguồn; chạy `demo-preflight.ps1` với credential runtime, rồi `demo-find-table.ps1` ngay trước khi quét. Chọn bàn cùng chi nhánh Cashier và đối chiếu payment mới theo payment/session code + amount trước khi confirm. Không reset database/volume.

## Lifecycle stability follow-up — 2026-09-24

Crash Splash sau saved state và race SignalR stop-during-negotiate đã được sửa trong logic Android; không đổi ảnh/icon/backend. Samsung restart Gateway + resume bằng Recents **10/10 PASS, 0 crash**, cùng PID, một kết nối phục hồi và REST resync mỗi vòng. Gateway down **25,4 giây** rồi tự phục hồi; rapid foreground/background 10 vòng không crash; Home restore sau `am kill` tải lại menu thật. Build, 31 unit tests, instrumentation (bao gồm hồi quy lifecycle/fallback/logout-login) PASS theo kết quả chi tiết tại `ANDROID_LIFECYCLE_SIGNALR_FIX.md`.

Không mở rộng acceptance sang toàn bộ process-death UI: MenuFragment còn lỗi bind catalog sau restore process ngay tại màn Thực đơn; quay về Home/mở lại Menu phục hồi được. Không tạo order/payment mới trong lượt lifecycle này và không thay kết quả B6/ONSITE đã nghiệm thu ở trên.
