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
- [ ] Reload Chef rồi logout/login → order active và lịch sử vẫn khớp backend.
- [ ] Thử transition sai trên order test bằng API hợp lệ → backend trả conflict, status không đổi; không sửa SQL trực tiếp.

## 09 SignalR

- [x] Giữ Samsung ở `Tiến độ món ăn`, Chef đổi món A, không refresh Android → món A tự cập nhật còn món B giữ nguyên; item-level lượt trước và order `4019` lượt mới.
- [ ] Sau event, đọc REST canonical → Android và Orders DB cùng state; SignalR chỉ kích hoạt resync.
- [ ] Mất/reconnect mạng ngắn → Android resync theo REST, không nhân đôi order/session.

## 10 Service Request

- [x] Customer gửi `Thêm nước lọc / Trà đá` → lịch sử hiện request thật `WATER`, trạng thái `CHỜ TIẾP NHẬN`; request `3004` session `4013`.
- [ ] Xác định staff role/UI nhận request từ source; khi handler tồn tại, staff cập nhật → Android phản ánh đúng status, không tự giả định Chef xử lý.

`DEMO_BLOCKER`: source hiện chỉ có Customer create/list/cancel; không có staff handler/UI hay transition xử lý. Request `3004` từ `OPEN` thành `CANCELLED` khi đóng phiên. Không tick mục staff/realtime cho đến khi feature được triển khai theo task riêng.

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
