# Checklist kiểm thử chức năng VietTable

Biên bản lượt đóng gap: 25/09/2026, Samsung SM-A528B, bắt đầu từ commit `3282079`, môi trường Docker local. Chỉ tick `[x]` khi đã quan sát kết quả trên môi trường này; `[ ]` là chưa nghiệm thu hoặc đang thiếu chức năng. Không ghi mật khẩu, JWT hoặc secret. Với mỗi lỗi, ghi severity `DEMO_BLOCKER`, `HIGH`, `MEDIUM`, `LOW` hoặc `INFO`, mã phiên/order/payment và bằng chứng, nhưng không đưa token vào log.

## 01 Infrastructure

- [ ] `docker compose ps` cho Gateway, Catalog, Orders, Customers, Identity, Billing, SQL Server, RabbitMQ đang chạy; Gateway `:5100` truy cập được.
- [ ] Gateway health/ready và các service `:5101–5105` trả trạng thái sẵn sàng; không có container lặp restart.
- [ ] `adb devices` thấy Samsung SM-A528B và `adb reverse --list` có `tcp:5100 tcp:5100`; Android chỉ gọi Gateway local.

## 02 Authentication

- [x] Customer `Quang` đăng nhập Android bằng mật khẩu → vào Home đúng profile, không cần tạo account giả.
- [x] `chef_hung`, `cashier_lan`, `admin` đăng nhập đúng ba web tương ứng → mỗi dashboard hiện đúng role và Chi nhánh Quận 1.
- [ ] Logout rồi login lại từng staff web → session cũ hết quyền, dữ liệu backend vẫn còn.

## 03 Customer

- [ ] Home Android tải chi nhánh/món từ Gateway → không có danh sách mock; đổi branch thì menu khớp branch.
- [ ] Customer Web `http://localhost:5100/` tải, đăng nhập nếu cần, xem branch và menu → không có React crash hay request API 5xx.

## 04 Reservation

- [ ] Customer xem ngày/giờ và bàn trống → availability đúng branch, không hiện bàn đang dùng.
- [ ] Tạo/sửa/hủy một reservation test hợp lệ → trạng thái lưu sau reload; không sửa reservation demo của người khác.

## 05 QR

- [ ] Chạy `SelfRestaurant-master\scripts\demo-find-table.ps1 -BranchId 1` ngay trước test → chọn QR của bàn Quận 1 đang trống.
- [ ] Quét camera hoặc nhập mã bàn thật → Gateway validate đúng branch/table; QR sai báo lỗi thật, không tạo session giả.

## 06 Dining Session

- [x] Customer check-in/walk-in trên bàn vừa chọn → tạo một DiningSession `ACTIVE` với ID thật và đúng bàn; lượt mới session `4013`, `4014` Quận 1 Bàn 5.
- [ ] Rời màn rồi mở lại → active session còn nguyên; người khác không được chiếm cùng bàn.

## 07 Ordering

- [x] Customer thêm hai món khác nhau, xác nhận giỏ và gửi order → Chef Web thấy đúng bàn, tên món, số lượng, tổng tiền; order `4018` 120.000đ.
- [x] Customer gọi thêm vòng 2 trong cùng DiningSession → tạo Order mới; Order vòng 1 không bị ghi đè; đã nghiệm thu session `4012`/orders `4016`, `4017` ở lượt trước.
- [ ] Không bấm Submit liên tục; kiểm tra source/test idempotency → retry cùng key không tạo hai order.

## 08 Chef/Kitchen

- [ ] CHEF Quận 1 mở `http://localhost:5100/app/chef/Staff/Account/Login` → chỉ thấy order active Quận 1, đúng bàn.
- [ ] Bấm `Bắt đầu` ở món A → backend món A `PREPARING`, món B giữ status cũ; order aggregate theo rule backend.
- [x] Bấm `BẮT ĐẦU NẤU` rồi `Hoàn thành` → backend/Chef UI thành `READY`; Chef không tự chuyển sang `SERVING`; order `4019` lượt mới.
- [x] Item `READY` hiển thị nút `Tôi đã nhận món` riêng; Samsung order `5016`: Bánh mì ốp la sang `SERVING`/Android `ĐÃ PHỤC VỤ`, Bò né vẫn `PREPARING`/`ĐANG NẤU`.
- [ ] Reload Chef rồi logout/login → order active và lịch sử vẫn khớp backend.
- [ ] Thử transition sai trên order test bằng API hợp lệ → backend trả conflict, status không đổi; không sửa SQL trực tiếp.

## 09 SignalR

- [x] Giữ Samsung ở `Tiến độ món ăn`, Chef đổi món A, không refresh Android → món A tự cập nhật còn món B giữ nguyên; item-level lượt trước và order `4019` lượt mới.
- [ ] Sau event, đọc REST canonical → Android và Orders DB cùng state; SignalR chỉ kích hoạt resync.
- [ ] Mất/reconnect mạng ngắn → Android resync theo REST, không nhân đôi order/session.

## 10 Service Request

- [x] Samsung Customer tạo `WATER` trên session `5012` → Android hiện `CHỜ TIẾP NHẬN`; request đúng Bàn 5 / Quận 1.
- [x] Cashier Web `http://localhost:5100/app/cashier/Staff/Account/Login`, cashier `cashier_lan` Quận 1 → mục `Yêu cầu phục vụ` hiện đúng bàn/type/time/status/note.
- [x] Cashier `Tiếp nhận` → Orders `ACKNOWLEDGED`; không refresh Samsung, Android tự resync thành `ĐANG XỬ LÝ`.
- [x] Cashier `Hoàn tất` → `COMPLETED`; Android tự hiện `ĐÃ HOÀN TẤT`.
- [x] Customer hủy request OPEN tiếp theo → history Android hiện `ĐÃ HỦY`; staff active list bỏ request.
- [x] Cashier `cashier_huong` branch 2 (UI runtime hiện tên Gò Vấp) không thấy request `4005` của branch 1; POST `/api/gateway/staff/cashier/service-requests/4005/acknowledge` trả **403 forbidden**, request vẫn `OPEN` sau lần thử.
- [x] Customer không có staff session gọi acknowledge/complete bị chặn bởi staff auth policy (401); state machine từ chối OPEN→COMPLETED/ACKNOWLEDGED→CANCELLED (contract test/source guard).
- [x] Payment `4042` thành công qua Cashier UI → session `5012` chuyển `PAYMENT_REQUESTED → CLOSED`; request `4005` tự `OPEN → CANCELLED` cùng thời điểm kết thúc session. Request `4003` đã `COMPLETED` vẫn `COMPLETED`; targeted backend source-contract test bảo vệ predicate chỉ gồm `OPEN`/`ACKNOWLEDGED`.

**Runtime status:** Staff UI→Android realtime, customer cancel, READY→SERVING, wrong-branch mutation rejection, và session-close cleanup đều PASS trên Samsung SM-A528B/Cashier Web, session `5012`, Bàn 5 Quận 1. Cashier UI payment `4042` (`PAY-20260925154843-62EEA9CE200A4691A87A0`, 120.000đ) xác nhận thành công; Samsung tự tới màn Payment Success; invoice `4016` phát hành đúng 120.000đ. Local demo state đã được khép lại; không phải giao dịch tiền thật.

**Kiểm thử lượt tiếp nối:** Android `:app:assembleDebug` + `:app:testDebugUnitTest` PASS; Orders API build PASS; Backend ContractTests 114/114 PASS; Cashier Web `npm run build` PASS. Android instrumentation không chạy lại (không có Android source change ở lượt này); lần trước trên cùng thay đổi Android: 13 PASS, 1 opt-in SKIPPED.

## 11 Checkout

- [ ] Customer vào Thanh toán → quote theo tất cả order rounds và đúng tổng tiền VND.
- [ ] Nếu món còn `READY`, app hiện cảnh báo đúng; tiếp tục chỉ sau thao tác xác nhận của Customer, không tự đổi item state.
- [x] Chọn ONSITE `Tiền mặt` và `Thanh toán tại quầy thu ngân` → tạo payment `PENDING` đúng session/amount, không phải paid giả; payment `3045` session `4014`.

## 12 Cashier

- [x] `cashier_lan` mở Cashier Web → payment mới nằm trong `Thanh toán ứng dụng tại chỗ`, đúng branch, bàn, session, amount và method.
- [x] Với payment demo đã xác định, bấm `Xác nhận đã thu tiền` một lần → Billing `SUCCEEDED`, confirmed employee đúng Cashier; không dùng thanh toán legacy để thu lần hai.
- [x] Cashier branch khác không xác nhận được payment Quận 1 → `cashier_huong` Quận 2 list không thấy `3043`, direct confirm HTTP 403 và payment vẫn `PENDING`.

## 13 Split Bill

- [x] Trên session test mới `4013`, chia hóa đơn hai phần → split `2004`: 60.000đ + 60.000đ = quote 120.000đ, payments `3043`/`3044`.
- [x] Cashier xác nhận phần 1 → part `2008` paid, Samsung về Split Bill; session chưa `CLOSED`, chưa có final invoice.
- [x] Cashier xác nhận phần cuối → part `2009` paid, split `PAID`, một invoice `3017` đúng 120.000đ, session `CLOSED`.

## 14 Invoice

- [x] Android **tự** chuyển `Thanh toán thành công!` sau Cashier confirm, không chạm Samsung: 3/3 payments `3043`, `3044`, `3045` khoảng 2–5 giây; nút `Kiểm tra trạng thái thanh toán` là fallback.
- [x] Invoice/payment/session/amount khớp Billing: split invoice `3017` 120.000đ; Orders sessions `4013`, `4014` đều `CLOSED`.

## 15 Review

- [ ] Sau paid session, Customer tạo review thật → chỉ review session đủ điều kiện; reload vẫn còn.
- [ ] Edit/delete review của chính Customer → backend cho phép đúng ownership, Customer khác bị chặn.

## 16 FAQ/Support

- [ ] FAQ Android tải từ backend B7 → không phải nội dung mock và empty/error state hiển thị đúng.
- [ ] Customer tạo support request → nhận ID thật, retry idempotent không tạo bản sao.

## 17 Admin

- [ ] Admin login → dashboard và menu `Tổng quan`, `Danh mục`, `Nguyên liệu`, `Quản lý kho`, `Món ăn`, `Bàn & QR`, `Nhân viên`, `Khách hàng`, `Báo cáo`, `Cài đặt` tải không crash.
- [ ] Read-only: danh mục, món, giá, availability, ảnh, bàn/QR, nhân viên/khách hàng đúng dữ liệu backend.
- [x] Admin CRUD qua UI với danh mục `QA TEMP Role Audit 20260925`: create/read/update persist sau reload, vô hiệu hóa rồi xóa sau xác nhận; reload còn 10 danh mục gốc, không thay 52 ảnh/menu.
- [ ] Báo cáo thu ngân/Admin (nếu màn có) phản ánh giao dịch paid mới đúng ngày/branch.

## 18 Security Roles

- [ ] Customer JWT gọi Chef mutation → HTTP 401/403 và order không đổi.
- [ ] Customer/Chef gọi Cashier confirm → HTTP 401/403 và payment vẫn `PENDING`.
- [x] Non-admin CHEF gọi Admin DELETE trên category ID không tồn tại → HTTP 401; dữ liệu không đổi.
- [x] CHEF/CASHIER đúng role nhưng sai branch → Chef Q1 order Q2 HTTP 403 (lượt trước), Cashier Q2 payment Q1 `3043` HTTP 403 (lượt mới).
- [ ] Customer không thể tự đổi kitchen item thành `READY` hoặc tự set ONSITE `SUCCEEDED`.

## 19 Recovery

- [ ] Refresh/reload Chef và Cashier → trạng thái hydrate từ backend, không mất order/payment.
- [ ] Khởi động lại Android hoặc reconnect SignalR → phiên, order, payment tải lại từ REST canonical; không có mock/scenario fallback.
- [ ] ADB daemon restart → khôi phục `adb reverse tcp:5100 tcp:5100`, app kết nối Gateway bình thường.

## 20 Demo Final

- [ ] Trong 8–12 phút: Customer Android login → QR bàn trống → order `4019` → Chef Web bắt đầu/hoàn thành → Samsung tự cập nhật → Customer checkout ONSITE → Cashier Web xác nhận `3045` → Samsung tự Success → invoice đúng → session `4014` `CLOSED`. Flow đã PASS ở mục ghi nhận; thời lượng không được bấm giờ nên tiêu chí 8–12 phút chưa tick.
- [ ] Mở Admin Web khi được hỏi → giải thích đúng các menu đã hiện; không khẳng định một CRUD chưa test là PASS.
- [ ] Ghi lại commit, thiết bị, thời gian, ID session/order/payment và từng PASS/FAIL; không ghi password/token, không push nếu chưa được yêu cầu.

**Cập nhật service workflow (25/09/2026):** Dùng `CASHIER` là handler role vì chưa có waiter UI; staff page mục `Yêu cầu phục vụ`, state machine `OPEN → ACKNOWLEDGED → COMPLETED`, Customer chỉ hủy `OPEN → CANCELLED`. Samsung E2E thật cho tạo/nhận/hoàn tất/hủy Service Request và READY→SERVING PASS. Bổ sung runtime branch isolation (`4005`, branch 2 → 403, giữ OPEN) và session-close cleanup (`4005` → CANCELLED; `4003` COMPLETED được giữ). Những mục unchecked khác trong checklist tổng thể thuộc các luồng chưa chạy ngoài task này.
