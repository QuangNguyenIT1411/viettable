# VietTable — Final Demo Runbook

Cập nhật: 26/09/2026 · Môi trường: local Docker + Samsung SM-A528B · Gateway: `http://localhost:5100`

> Dùng tài liệu này để chạy demo lặp lại. Mỗi lần tạo order/payment mới, ID và số tiền sẽ thay đổi theo bàn/món chọn. ONSITE ở môi trường local là trạng thái demo; không dùng tài liệu này để khẳng định đã nhận tiền thật. Không reset database/volume, không sửa trạng thái trực tiếp bằng SQL.

## 1. Preflight trước buổi demo

- [ ] Docker Desktop đang chạy.
- [ ] Mở PowerShell và kiểm tra backend:

  ```powershell
  cd D:\Quang\VietTable\SelfRestaurant-master
  docker compose ps
  Invoke-WebRequest http://localhost:5100/readyz
  ```

  Gateway, Catalog, Orders, Customers, Identity, Billing, SQL Server và RabbitMQ phải `Up`; `/readyz` phải trả HTTP 200.

- [ ] Kết nối/build/cài/mở Android trên Samsung (script giữ app data với `adb install -r`):

  ```powershell
  cd D:\Quang\VietTable
  powershell -ExecutionPolicy Bypass -File .\scripts\connect-phone.ps1 -OpenApp
  ```

  Kết quả mong đợi có `SM_A528B`, `phone -> Gateway ... PASS`, `BUILD SUCCESSFUL`, `Status: ok` và base URL `http://127.0.0.1:5100`.

- [ ] Xác nhận reverse tunnel (không cần thêm ADB vào PATH toàn cục):

  ```powershell
  & 'D:\Quang\Android\platform-tools\adb.exe' reverse --list
  ```

  Phải có `tcp:5100 tcp:5100`.

- [ ] Ngay trước khi vào bàn, tìm bàn đang khả dụng bằng script read-only:

  ```powershell
  cd D:\Quang\VietTable
  powershell -ExecutionPolicy Bypass -File .\scripts\demo-find-table.ps1 -BranchId 1
  ```

  Chọn một dòng của `Chi nhánh Quận 1` và dùng đúng `QrCode` trong kết quả. Không mặc định bàn 5 vẫn còn trống: script phải được chạy lại ngay trước lần check-in. Script nằm ở `D:\Quang\VietTable\scripts` (không phải thư mục backend).

- [ ] Mở sẵn các giao diện staff cần trình bày:

  | Vai trò | URL qua Gateway |
  |---|---|
  | Customer | Samsung Android, package `com.restaurant.viettable` |
  | Chef | `http://localhost:5100/app/chef/Staff/Account/Login` |
  | Cashier | `http://localhost:5100/app/cashier/Staff/Account/Login` |
  | Admin | `http://localhost:5100/app/admin/Admin/Account/Login` |

- [ ] Dùng account đúng role đã xác minh; không dùng Admin thay Chef/Cashier:

  | Vai trò | Account | Chi nhánh / ghi chú |
  |---|---|---|
  | Customer | `Quang` | Đăng nhập trong Android; chọn Quận 1 khi demo |
  | Chef | `chef_hung` | `CHEF`, Quận 1 |
  | Cashier | `cashier_lan` | `CASHIER`, Quận 1 |
  | Admin | `admin` | `ADMIN`, Quận 1 |

  Mật khẩu không được lưu trong tài liệu. Dùng credential demo đã được cấp/lưu an toàn.

## 2. Luồng demo chính (khoảng 8–12 phút)

### Customer tạo phiên và order

1. **Samsung:** đăng nhập Customer `Quang`, chọn **Chi nhánh Quận 1**.
2. **Samsung:** vào **Scan QR** và nhập mã bàn thật vừa nhận từ `demo-find-table.ps1` nếu không có QR vật lý. Đây vẫn là mã Catalog thật; không dùng mã/session giả.
3. **Samsung:** nếu bàn cho phép walk-in, xác nhận **Bắt đầu gọi món tại bàn**. Xác nhận UI hiển thị đúng bàn và phiên đang hoạt động.
4. **Samsung:** thêm hai món thật từ menu rồi gửi order. Ghi lại bàn, mã order và tổng tiền hiển thị; các ID sẽ khác mỗi lần chạy.

### Chef → Android realtime → xác nhận món

5. **Chef Web:** đăng nhập `chef_hung`, tìm order mới theo đúng bàn vừa chọn. Đối chiếu tên món và số lượng trước khi thao tác.
6. **Chef Web:** với từng món đang chờ, nhấn **Bắt đầu** (hoặc nút bắt đầu order nếu đang dùng thao tác cả order). Backend chuyển món sang `PREPARING`.
7. **Samsung:** không bấm làm mới; màn order phải tự cập nhật sang **ĐANG NẤU**.
8. **Chef Web:** hoàn thành một món bằng **Hoàn thành**. Backend chuyển item đó sang `READY`.
9. **Samsung:** item đó tự hiện **SẴN SÀNG** và nút **Tôi đã nhận món**; item còn nấu vẫn giữ trạng thái riêng.
10. **Samsung:** nhấn **Tôi đã nhận món** cho đúng item. Trạng thái chuyển sang `SERVING`, hiển thị **ĐÃ PHỤC VỤ**. Lặp lại với món còn lại sau khi Chef hoàn tất món đó.

### Service Request qua Cashier staff UI

11. **Samsung:** trong Dining Session, chọn **Gọi nhân viên**, chọn **Xin nước / WATER** hoặc **Gọi nhân viên**, rồi gửi yêu cầu.
12. **Cashier Web:** trong mục **Yêu cầu phục vụ**, xác nhận request đúng bàn/loại yêu cầu/trạng thái **Chờ tiếp nhận**.
13. **Cashier Web:** nhấn **Tiếp nhận**. **Samsung** phải tự chuyển sang **ĐANG XỬ LÝ**, không refresh thủ công.
14. **Cashier Web:** nhấn **Hoàn tất**. **Samsung** phải tự chuyển sang **ĐÃ HOÀN TẤT**. Cashier UI tự làm mới theo chu kỳ ngắn; không cần refresh toàn trang.

### ONSITE → Cashier → invoice → đóng phiên

15. **Samsung:** vào **Thanh toán**, rà soát quote/tổng tiền thật, chọn **Thanh toán Trực tiếp** → **Tiền mặt** → **Thanh toán tại quầy thu ngân** và xác nhận yêu cầu.
16. **Samsung:** để nguyên màn hình chờ ONSITE. Không nhấn **Kiểm tra trạng thái thanh toán**; nút này chỉ là fallback.
17. **Cashier Web:** trong **Thanh toán ứng dụng tại chỗ**, tìm payment mới nhất bằng **đúng bàn + đúng số tiền + phiên**; bỏ qua payment pending cũ nếu có. Chỉ xác nhận đúng payment của phiên hiện tại.
18. **Cashier Web:** nhấn **Xác nhận đã thu tiền** và xác nhận hộp thoại. Đây là xác nhận giao dịch demo local; chỉ trình bày là thanh toán tiền thật nếu quy trình tại buổi bảo vệ thực sự có nhận tiền.
19. **Samsung:** không chạm vào máy; trong khoảng vài giây, app phải tự chuyển tới **Thanh toán thành công**, số tiền và invoice. Kiểm tra invoice thuộc đúng Dining Session.
20. Phiên phải kết thúc ở `CLOSED`; chạy lại `demo-find-table.ps1 -BranchId 1` để xác nhận bàn đã được giải phóng trước khi tái sử dụng.

## 3. Bằng chứng rehearsal đã chạy

Ngày 26/09/2026, một session mới đã được chạy qua Samsung thật, Chef Web và Cashier Web:

- Samsung: `SM-A528B`, customer `Quang`, branch Quận 1; QR thật `QR-Q1-B05` được **nhập thủ công**. Không kiểm nghiệm camera/QR vật lý trong lượt này.
- Dining Session `6012`, bàn 5; Order `6016`, hai món thật, tổng `120.000đ`.
- Chef `chef_hung` thao tác từng item trên UI; Samsung tự cập nhật `PREPARING` → `READY`; Customer xác nhận item qua **Tôi đã nhận món** → `SERVING`.
- Cashier `cashier_lan` nhận request `WATER`, bấm **Tiếp nhận** rồi **Hoàn tất**; Samsung tự hiện `ĐANG XỬ LÝ` rồi `ĐÃ HOÀN TẤT`.
- Cashier xác nhận ONSITE payment `PAY-20260926074157-5CB3D86AE6144D50B5E13`, session `DS-20260926072904-EBA6ED3187324CA`, `120.000đ`; Samsung không bấm kiểm tra thủ công và tự hiện thanh toán thành công + invoice `INV-f83fff354cb842bc8e17ccb69db037c7`.
- Bàn 5 sau đó xuất hiện lại trong danh sách khả dụng; trạng thái Admin cũng hiển thị 20 bàn Quận 1, 0 bàn đang sử dụng.
- Admin `admin` đăng nhập thật: dashboard PASS; trang Danh mục tải 10 danh mục; Món ăn tải 52 món; Bàn & QR tải 20 bàn. Chỉ đọc, không mutate dữ liệu.
- Google quick smoke trên Samsung PASS hai lượt: logout → chọn Gmail test `bao1479991@gmail.com` trong chooser → xác thực backend → chọn Quận 1 → Home; logout rồi đăng nhập lần hai thành công. Không dùng token giả hay Client Secret trong Android.
- Gateway `/readyz` trả HTTP 200; Docker services đều `Up`; `connect-phone.ps1 -OpenApp` build/install/launch PASS; `adb reverse` là `tcp:5100 tcp:5100`; log AndroidRuntime gần nhất không có `FATAL EXCEPTION`.

Đây là bằng chứng cho lượt rehearsal cụ thể; ID payment/order/session và số tiền không phải giá trị cần nhập cố định trong lần demo kế tiếp.

## 4. Kiểm tra nhanh role/Admin

- **Chef:** `chef_hung` → Chef URL → xác nhận branch Quận 1 → order đúng bàn → **Bắt đầu** / **Hoàn thành**.
- **Cashier:** `cashier_lan` → Cashier URL → xác nhận branch Quận 1 → request ở **Yêu cầu phục vụ**; payment ở **Thanh toán ứng dụng tại chỗ**.
- **Admin:** `admin` → Admin URL → dashboard, **Danh mục**, **Món ăn**, **Bàn & QR**. Final rehearsal chỉ xem dữ liệu; CRUD tạm đã được acceptance riêng trước đó.
- Role account và UI staff đều đã được xác minh. Các URL staff sử dụng trang xác thực nhân viên chung nhưng mở đúng ứng dụng/route theo role.

## 5. Google và split bill

- Google: quick smoke hai lượt đã PASS trên Samsung. Nếu cần trình bày, dùng Gmail test được cấu hình sẵn và chọn branch; không thay đổi Google Cloud trong buổi demo.
- Split Bill: không đưa vào luồng 8–12 phút. Acceptance cross-role trên session mới đã PASS trước lượt rehearsal này; không tạo thêm payment chỉ để minh họa nếu không cần.

## 6. Nếu có sự cố

- **Samsung mất Gateway / reverse:** chạy lại `powershell -ExecutionPolicy Bypass -File .\scripts\connect-phone.ps1 -OpenApp` từ `D:\Quang\VietTable`.
- **ADB daemon/device vừa restart:** chạy lại đúng script trên; nó tự đặt `adb reverse tcp:5100 tcp:5100` và kiểm tra phone → Gateway.
- **Bàn đang occupied/không check-in được:** không cố reuse; chạy `demo-find-table.ps1 -BranchId 1` từ workspace root và chọn bàn đang available.
- **Staff browser hết session:** đăng nhập lại đúng account/role tại URL tương ứng; không dùng Admin để thao tác Chef/Cashier.
- **Payment vẫn `PENDING`:** giữ Samsung ở màn chờ; Cashier tra payment mới theo đúng bàn, phiên và số tiền rồi xử lý qua UI. Chỉ dùng nút kiểm tra trạng thái trên Samsung làm fallback khi cần.
- **Không có QR vật lý:** nhập đúng `QrCode` thật từ helper trong Android. Không đưa mã test/fallback vào production.
- **Có lỗi bất ngờ:** dừng thao tác, chụp lại trạng thái/mã lỗi và kiểm tra health/log; không reset Docker volume, không xóa database, không dùng SQL để ép trạng thái.

## 7. Những gì lượt freeze này không nghiệm thu

- QR camera: rehearsal dùng mã QR thật nhập thủ công; không có QR vật lý để kiểm tra decode camera trong lượt này.
- Production HTTPS/domain, VNPAY external, Google Web E2E và release signing: ngoài phạm vi local demo này; xem `RELEASE_READINESS.md` để biết các giới hạn production riêng.
- ONSITE local là luồng demo; không thay thế tích hợp payment provider hoặc bằng chứng nhận tiền thực.
