# VietTable — Role-based functional QA (25/09/2026)

Mốc khảo sát: `829b3e1`. Kết quả dưới đây phân biệt rõ kiểm chứng trên Samsung, browser, API và source. Đây là môi trường Docker local; xác nhận thu ngân trong tài liệu chỉ áp dụng cho dữ liệu demo, không đại diện cho việc đã nhận tiền ngoài đời.

## A. URLs và cách mở

Gateway Docker (`cd D:\Quang\VietTable\SelfRestaurant-master; docker compose up -d`):

| Giao diện | URL qua Gateway | URL Vite nếu chạy riêng |
| --- | --- | --- |
| Customer Web | `http://localhost:5100/` | `http://localhost:5173/` |
| Chef | `http://localhost:5100/app/chef/Staff/Account/Login` | `http://localhost:5174/app/chef/Staff/Account/Login` |
| Cashier | `http://localhost:5100/app/cashier/Staff/Account/Login` | `http://localhost:5175/app/cashier/Staff/Account/Login` |
| Admin | `http://localhost:5100/app/admin/Admin/Account/Login` | `http://localhost:5176/app/admin/Admin/Account/Login` |

Các URL Gateway trên đã được mở trong browser thật. Frontend dự án nằm trong `SelfRestaurant-master/src/Frontend/`; mỗi app có script `npm run dev` và `npm run build`, Vite proxy `/api` tới Gateway `:5100`. Mỗi lệnh sau chạy trong một PowerShell riêng sau khi Gateway đã chạy:

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master\src\Frontend\selfrestaurant-customer-web
npm install
npm run dev
```
```powershell
cd D:\Quang\VietTable\SelfRestaurant-master\src\Frontend\selfrestaurant-chef-web
npm install
npm run dev
```
```powershell
cd D:\Quang\VietTable\SelfRestaurant-master\src\Frontend\selfrestaurant-cashier-web
npm install
npm run dev
```
```powershell
cd D:\Quang\VietTable\SelfRestaurant-master\src\Frontend\selfrestaurant-admin-web
npm install
npm run dev
```

Customer chính của demo là Samsung SM-A528B, package `com.restaurant.viettable`, gọi `http://127.0.0.1:5100` qua `adb reverse tcp:5100 tcp:5100`. Nếu ADB daemon khởi động lại phải đặt lại reverse. Không cho Android gọi trực tiếp port service.

## B. Accounts và roles

Không lưu mật khẩu trong tài liệu. Account existence, active status và branch được kiểm chứng từ Identity runtime; login role được kiểm chứng bằng UI.

| Vai trò / account demo | Kiểm chứng | Chi nhánh | Frontend / mục đích |
| --- | --- | --- | --- |
| `Quang` — CUSTOMER | VERIFIED, đăng nhập Android bằng mật khẩu | chọn khi vào bàn | Android / Customer Web |
| `chef_hung` — CHEF | VERIFIED, Chef dashboard tải được | Quận 1 | Bếp |
| `cashier_lan` — CASHIER | VERIFIED, Cashier dashboard tải được | Quận 1 | Thu ngân |
| `admin` — ADMIN | VERIFIED, Admin dashboard tải được | Quận 1 | Quản trị |
| `chef_nam` — CHEF, `cashier_huong` — CASHIER | VERIFIED existence, chưa login trong lượt QA này | Quận 2 | Tài khoản đối chứng branch |

Identity còn seed các role nhân viên `MANAGER`, `WAITER`, `KITCHEN_STAFF`. Mobile customer dùng role `customer` trong JWT; nhân viên dùng staff session cookie ở Gateway. `CHEF`/`KITCHEN_STAFF` thao tác bếp; `CASHIER` thu tiền; `MANAGER`/`ADMIN` có quyền rộng hơn theo controller, vì vậy demo này cố ý dùng đúng account CHEF và CASHIER, không dùng Admin để bypass.

| Role thật | Purpose theo route hiện có | Frontend | Authentication | Branch scoped? |
| --- | --- | --- | --- | --- |
| `customer` / CUSTOMER | bàn, order, checkout, review của chính mình | Android, Customer Web | mobile JWT; Customer Web session riêng | Theo resource/branch được chọn, không có staff BranchId |
| `CHEF` | cập nhật kitchen order/item | Chef Web | staff cookie | Có, BranchId từ Identity |
| `KITCHEN_STAFF` | cùng policy Chef | Chef Web | staff cookie | Có |
| `CASHIER` | xác nhận ONSITE và quầy | Cashier Web | staff cookie | Có |
| `WAITER` | role tồn tại trong Identity; chưa có staff handler service-request được xác minh | chưa xác minh UI riêng | staff account | Có BranchId trong staff record |
| `MANAGER` | được phép vào Chef/Cashier/Admin Gateway theo source | staff web tương ứng | staff cookie | Có BranchId, không phải global bypass |
| `ADMIN` | quản trị; controller cũng cho vào Chef/Cashier | Admin Web và staff policy rộng | staff cookie | Staff record có BranchId; test Admin Quận 1 |

## C. Role matrix và bảo vệ backend

| Hành động / endpoint Gateway | CUSTOMER | CHEF / KITCHEN_STAFF | CASHIER | MANAGER / ADMIN |
| --- | --- | --- | --- | --- |
| `/api/mobile/v1/...` đăng nhập, check-in, gửi order, service request, checkout | có, theo JWT và ownership | không dùng staff session | không dùng staff session | không dùng staff session |
| `/api/gateway/staff/chef/orders/.../start`, `ready`, item mutations | không | có, theo branch | không | có, theo staff policy và branch |
| `/api/gateway/staff/cashier/...` xác nhận ONSITE | không | không | có, theo branch | có, theo staff policy và branch |
| `/api/gateway/admin/...` quản trị | không | không | không | có; `AdminRoles = ADMIN, MANAGER` |

Kiểm chứng negative: Customer JWT gửi tới staff Chef/Cashier mutation nhận HTTP 401 do route yêu cầu staff session, không phải 403. CHEF `chef_hung` đăng nhập thật nhưng gọi Cashier confirm cho payment đã hoàn tất nhận 401; gọi Admin deactivate với customer ID không tồn tại nhận 401. CASHIER `cashier_lan` đăng nhập thật nhưng gọi Chef start trên order đã hoàn tất nhận 401. CHEF Quận 1 gọi mutation cho order Quận 2 nhận 403; order Quận 2 giữ nguyên `CONFIRMED`. Gateway trước lượt audit chỉ lọc danh sách Chef theo branch nhưng mutation chưa ràng buộc branch; đã sửa trong `StaffChefGatewayController` và runtime-test lại sau rebuild, gồm cả route đọc ingredient của order Quận 2 (403). Chưa chạy đầy đủ matrix mỗi role với mọi endpoint, cũng chưa thử wrong-branch Cashier bằng account Quận 2. Mã 401 là policy staff session hiện tại, không phải bằng chứng request chưa tới backend.

## D. State machine thực tế

`Order` và `OrderItem` đều lưu trạng thái. Các code Order có trong Orders DB: `PENDING`, `CONFIRMED`, `PREPARING`, `READY`, `SERVING`, `COMPLETED`, `CANCELLED`. Item có `PENDING`, `CONFIRMED`, `PREPARING`, `READY`, `SERVING`, `CANCELLED`. Không có item state `SERVED` ở backend; `SERVED` là tên model Android cho backend `SERVING`.

| From backend | Action / nút UI | Actor | To backend | Android item label |
| --- | --- | --- | --- | --- |
| `PENDING` hoặc `CONFIRMED` | `BẮT ĐẦU NẤU` cả order; hoặc `Bắt đầu` từng món | CHEF | `PREPARING` | `ĐANG NẤU` |
| `PREPARING` | `Hoàn thành` cả order hoặc từng món | CHEF | `READY` | `SẴN SÀNG` |
| `READY` | `Tôi đã nhận món` trên Customer Web | CUSTOMER | `SERVING` | `ĐÃ PHỤC VỤ` qua Android mapping; Android chưa có nút confirm này |
| `PENDING` / `CONFIRMED` / `PREPARING` / `READY` | hủy hợp lệ | actor được route cho phép | `CANCELLED` | `ĐÃ HỦY` |
| sau thanh toán/đóng phiên | nghiệp vụ hệ thống | system | `COMPLETED` (Order) | `HOÀN THÀNH` |

Code allowed transitions chi tiết: Item `PENDING→CONFIRMED` hoặc `PREPARING`, `CONFIRMED→PREPARING`, `PREPARING→READY`, `READY→SERVING`; hủy từ các trạng thái trước `SERVING`. Order cho Chef `PENDING/CONFIRMED→PREPARING→READY`; `READY→SERVING` chỉ bằng xác nhận Customer. Chef endpoint `serve` trả 409 `customer_confirmation_required`. Status aggregate Order được tính từ item, không suy từ một nút trên Android. Với món A `READY`, món B `PREPARING`, order vẫn `PREPARING`; tất cả `READY` thì order `READY`. `COMPLETED` không phải nút Chef.

| Backend status | Chef UI | Android order/item |
| --- | --- | --- |
| `PENDING`/`CONFIRMED` | Chờ chế biến | Đã gửi/Nhà hàng đã nhận |
| `PREPARING` | Đang chế biến | Đang xử lý/ĐANG NẤU |
| `READY` | Sẵn sàng | Đang xử lý/SẴN SÀNG |
| `SERVING` | Đang phục vụ; rời cột bếp active | Hoàn thành/ĐÃ PHỤC VỤ |
| `CANCELLED` | Đã hủy | Đã hủy |

## E. Kitchen workflow và bằng chứng

1. Trên Samsung, Customer đăng nhập, chạy `D:\Quang\VietTable\SelfRestaurant-master\scripts\demo-find-table.ps1 -BranchId 1` ngay trước check-in; lượt này bàn 5 Quận 1 với QR `QR-Q1-B05` đang trống. Nhập QR, chọn `Bắt đầu gọi món tại bàn`.
2. Chọn 1–2 món, `Xem giỏ & Gọi` → `Gửi đơn gọi món tới Bếp`. Samsung lượt QA tạo DiningSession `#4012`, order `#4016` gồm Bánh mì ốp la và Bò né, 120.000đ.
3. Chef Web đăng nhập `chef_hung`, tìm Bàn 5 ở cột `Chờ chế biến`. Order mới xuất hiện đúng hai món. Nút `Bắt đầu` ở một món chỉ đổi món đó; món kia giữ nguyên. Samsung màn `Tiến độ món ăn` tự đổi item đó thành `ĐANG NẤU` mà không bấm refresh.
4. Bấm `BẮT ĐẦU NẤU` cho cả order, rồi `Hoàn thành` từng món hoặc cả order. Chef chuyển sang cột `Sẵn sàng`; Samsung tự hiện `SẴN SÀNG`. Giữa chừng một món `READY`, một món `PREPARING` đã quan sát được trên cả hai UI.
5. Bấm `GỌI THÊM MÓN` trên cùng phiên và gửi Bún Bò Huế 55.000đ. Lượt QA tạo order `#4017`, Chef thấy order mới đồng thời vẫn thấy #4016 trong cột Sẵn sàng. Session vẫn `#4012`, 2 đợt, 3 món, tổng 175.000đ.

Chef History reload giữ hai đơn theo backend. Chưa đo độ trễ SignalR bằng đồng hồ chính xác; quan sát cập nhật trong vài giây, không refresh Android. REST là canonical state, SignalR chỉ thông báo để resync.

Service Request được Customer tạo/đọc/hủy qua mobile Gateway, nhưng chưa tìm thấy staff route/UI xử lý trong Gateway Chef/Cashier/Admin. Android map text chứa `nước` thành `WATER`, chứa `bếp` thành `KITCHEN_REMINDER`, chứa `hủy` thành `CANCEL_ITEM`, còn lại thành `GENERAL_ASSISTANCE`; đây không phải bảng phân công nhân sự. Hiện không được quảng bá Chef đã nhận request chỉ vì Customer gửi thành công.

| Request trên Android | Backend type | Handler role/UI hiện có |
| --- | --- | --- |
| Thêm nước lọc / Trà đá | `WATER` | Chưa có staff handler UI được xác minh |
| Kiểm tra món lâu lên | `GENERAL_ASSISTANCE` (text này không chứa `bếp`) | Chưa có staff handler UI được xác minh |
| Lấy thêm bát đũa, khăn / Thêm gia vị, nước chấm / Dọn đĩa / Yêu cầu khác | `GENERAL_ASSISTANCE` | Chưa có staff handler UI được xác minh |
| Từ chi tiết món `Giục bếp làm nhanh` | `KITCHEN_REMINDER` | Chưa có staff handler UI được xác minh |

## F. Cashier workflow

Trên Samsung từ phiên bàn 5: `Thanh toán` → cảnh báo còn món `READY` → `Vẫn yêu cầu thanh toán` → hóa đơn 175.000đ → `Chọn phương thức thanh toán` → `Thanh toán Trực tiếp` → `Tiền mặt` → `Thanh toán tại quầy thu ngân` → `Xác nhận yêu cầu thanh toán`. Cảnh báo `READY` là confirm dialog, không phải hard gate.

Cashier Web `cashier_lan` chi nhánh Quận 1 thấy payment `PAY-20260925021916-65C74CDC6F044FBA87B4E`, session `DS-20260925021016-1B90EDE2067A424`, Bàn 5, `175.000 đ`, `CASH / CASHIER`, `Chờ thu tiền`. Với giao dịch demo local đã xác định, nút `Xác nhận đã thu tiền` chuyển Billing payment ID `3042` thành `SUCCEEDED`, `ConfirmedByEmployeeId=3`. Browser automation treo ở native `window.confirm`, nên không có ảnh chụp modal đã bấm; DB và Android chứng minh kết quả server. Trên Samsung phải bấm `Kiểm tra trạng thái thanh toán` ở màn chờ để thấy `Thanh toán thành công!`; chưa nghiệm thu tự điều hướng không chạm. Hóa đơn `INV-0c7984acc30d4b6db928d9ce5e4a8f3b` gắn payment `3042`, tổng 175.000đ; Orders session `4012` là `CLOSED`.

Không dùng phần `Chi tiết đơn hàng`/`Xác nhận thanh toán` legacy của Cashier để thu lần hai; chỉ dùng vùng `Thanh toán ứng dụng tại chỗ` cho Android ONSITE.

## G. Admin functions thực tế

Admin Web login bằng `admin`, dashboard hiện branch Quận 1 và menu: `Tổng quan`, `Danh mục`, `Nguyên liệu`, `Quản lý kho`, `Món ăn`, `Bàn & QR`, `Nhân viên`, `Khách hàng`, `Báo cáo`, `Cài đặt`. Gateway có API tương ứng cho category/menu, ingredient/inventory, table, employee, customer, settings. Read-only runtime: dashboard hiện doanh thu hôm nay 175.000đ sau giao dịch demo; màn `Món ăn` tải 52 món gồm ảnh, giá và availability; màn `Bàn & QR` tải 20 bàn Quận 1 cùng QR code. Browser console các màn này không có error. Chưa chạy mutation hoặc từng màn CRUD; không sửa dữ liệu 52 ảnh/menu demo để spot-check.

Để kiểm tra thủ công an toàn: đăng nhập Admin Web bằng `admin`, mở `Tổng quan` và đối chiếu doanh thu giao dịch vừa thanh toán; mở `Món ăn` và kiểm tra tên/ảnh/giá/khả dụng của một món; mở `Bàn & QR` và xác nhận bàn vừa dùng thuộc đúng chi nhánh. Các menu còn lại được liệt kê từ UI nhưng chưa có kết quả thao tác CRUD trong lượt QA này. Dùng account Chef/Cashier riêng để xác nhận không truy cập được Admin mutation; không đổi role hoặc dữ liệu món demo.

## H. Android realtime

Chef chuyển một item sang `PREPARING`: Samsung item A hiện `ĐANG NẤU`, item B vẫn `ĐÃ NHẬN`. Chef chuyển item A sang `READY`: Samsung hiện `SẴN SÀNG`, item B vẫn `ĐANG NẤU`. Các thay đổi đến màn tiến độ khi giữ màn mở, không nhấn refresh. Mapping Android của Order `READY`/`SERVING` và Item `SERVING` trước QA bị fallback sai; đã sửa và thêm unit test. Thanh toán ONSITE kết thúc trên Samsung sau thao tác kiểm tra trạng thái; tự cập nhật payment khi màn chờ mở chưa được chứng minh.

## I. Kết quả kiểm thử

| Flow | Kết quả | Bằng chứng |
| --- | --- | --- |
| Docker Gateway + 5 services, SQL, RabbitMQ | PASS | container running; Gateway Docker build thành công |
| Login Customer, Chef, Cashier, Admin đúng account/branch | PASS | Android/browser runtime |
| QR bàn trống → DiningSession ACTIVE | PASS | Q1 Bàn 5, session 4012 |
| 2 món → Chef thấy đúng bàn/món | PASS | order 4016, 120.000đ |
| Item-level Chef → Android realtime | PASS | A đổi, B giữ trạng thái; không refresh |
| Chef order PREPARING → READY | PASS | Chef cột Sẵn sàng, Android label đúng |
| Transition sai `COMPLETED→PREPARING` | PASS | Orders API trả HTTP 409; order 4016 vẫn `COMPLETED` |
| Vòng order thứ hai cùng session | PASS | order 4017, session 4012 |
| Service request Customer | PASS một chiều | WATER tạo được, trạng thái CHỜ TIẾP NHẬN; chưa có staff handler UI |
| Checkout → ONSITE pending → Cashier thấy đúng payment | PASS | payment 3042, 175.000đ |
| Cashier confirm → Invoice → CLOSED | PASS tại backend và Android sau bấm kiểm tra | Billing SUCCEEDED, invoice 3016, Orders CLOSED |
| Android tự điều hướng payment không chạm | CHƯA CHỨNG MINH | màn chờ vẫn đứng đến khi bấm `Kiểm tra trạng thái thanh toán` |
| Split Bill cross-role | PASS theo bằng chứng cũ được revalidate, chưa chạy session mới | `ONSITE_PAYMENT_ACCEPTANCE.md`: Cashier UI/Samsung đã test hai phần; DB hiện còn split 14/session 2051, phần 1074/1075 cùng `SUCCEEDED`, invoice 1028 = 70.000đ, session `CLOSED` |
| Customer Web login, branch/menu | PASS smoke | Home → chọn Quận 1 → chọn Bàn 1 → menu có món/giá/ảnh; đăng nhập `Quang` hiện tên Nguyễn Vinh Quang; đã reset table context tạm; browser console không có error |
| Admin dashboard, món ăn và Bàn & QR read-only | PASS smoke | doanh thu 175.000đ, 52 món, 20 bàn Quận 1, không console error |
| Toàn bộ Admin CRUD, wrong-branch Cashier | CHƯA CHẠY | không suy diễn PASS từ source |

Build: Android `:app:assembleDebug :app:testDebugUnitTest` PASS với `-PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100`, APK cài Samsung. Chef `npm run build` local PASS. `docker compose build --no-cache gateway` PASS: chạy mới `npm install` và `npm run build` cho cả bốn frontend, publish Gateway .NET; đã deploy Gateway mới. Ba lệnh `npm run build` local Windows còn lại từng fail do `node_modules` local thiếu `tsc`, không phải lỗi source; Docker no-cache là build chính thức đã chạy lại. Gateway branch guard runtime sau deploy: Chef Q1 sửa order Q2 và đọc ingredient Q2 đều 403, order không đổi. Customer JWT gọi staff mutations và non-admin gọi Admin mutation trả 401 theo cơ chế staff cookie. Orders API từ chối transition sai `COMPLETED→PREPARING` bằng HTTP 409, không đổi trạng thái.
Build: Android `:app:assembleDebug :app:testDebugUnitTest` PASS với `-PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100`, APK cài Samsung. Chef `npm run build` local PASS. `docker compose build --no-cache gateway` PASS: chạy mới `npm install` và `npm run build` cho cả bốn frontend, publish Gateway .NET; đã deploy Gateway mới. Ba lệnh `npm run build` local Windows còn lại từng fail do `node_modules` local thiếu `tsc`, không phải lỗi source; Docker no-cache là build chính thức đã chạy lại. Gateway branch guard runtime sau deploy: Chef Q1 sửa order Q2 và đọc ingredient Q2 đều 403, order không đổi. Customer JWT gọi staff mutations nhận 401; CHEF→Cashier, CHEF→Admin, CASHIER→Chef mutation đều nhận 401 theo cơ chế staff cookie. Orders API từ chối transition sai `COMPLETED→PREPARING` bằng HTTP 409, không đổi trạng thái.

## J. Known limitations và mức độ

| Severity | Finding | Tình trạng |
| --- | --- | --- |
| HIGH | Chef mutation trước QA thiếu branch authorization mặc dù list đã lọc branch | Đã sửa, test Q1→Q2 403; cần rerun targeted test sau rebuild cuối |
| MEDIUM | Android chưa có `Tôi đã nhận món` để chuyển `READY→SERVING`; Customer Web có | Chưa sửa ngoài scope QA; checkout chỉ cảnh báo rồi cho tiếp tục |
| MEDIUM | Service request tạo được nhưng chưa thấy staff route/UI nhận, xử lý, đổi status | Chưa có cross-role service-request E2E |
| MEDIUM | Payment success trên Samsung cần bấm `Kiểm tra trạng thái thanh toán` trong lượt quan sát | Cần xác minh polling/autonavigation riêng |
| INFO | Cashier UI chứa giao dịch split pending cũ từ ngày khác | Không xác nhận nhầm; split mới chưa test, nhưng bằng chứng split đã PASS được đối chiếu read-only |
| INFO | Toàn bộ Admin CRUD và wrong-branch Cashier chưa kiểm chứng runtime | Customer Web login/menu smoke đã PASS; phần còn lại chưa tính PASS |

**Kết luận hiện tại: ROLE-BASED QA: NOT READY** theo đủ 30 acceptance criteria của task. Main demo Customer → Chef → Android → Cashier → Android/Invoice/CLOSED đã đi tới cuối, nhưng còn các mục chưa nghiệm thu nêu rõ ở bảng trên.

## K. Kịch bản demo vai trò 8–12 phút

1. Mở Docker Gateway, đặt `adb reverse tcp:5100 tcp:5100`, mở VietTable trên Samsung; đăng nhập `Quang`. Chạy `demo-find-table.ps1 -BranchId 1` ngay trước khi chọn mã QR bàn trống.
2. Trên Samsung: nhập mã QR bàn vừa tìm, bắt đầu phiên, chọn hai món, bấm `Gửi đơn gọi món tới Bếp`. Ghi mã bàn và order trên màn.
3. Mở Chef Web URL ở mục A, đăng nhập `chef_hung`, tìm đúng bàn ở `Chờ chế biến`. Bấm `Bắt đầu` một món rồi nhìn Samsung tự đổi `ĐANG NẤU` trong `Tiến độ món ăn`, không refresh.
4. Ở Chef, bấm `BẮT ĐẦU NẤU` và `Hoàn thành` để hai món sang `Sẵn sàng`; trên Samsung đối chiếu từng món `SẴN SÀNG`. Có thể gọi thêm một món để minh họa order round thứ hai cùng phiên.
5. Trên Samsung chọn thanh toán, đọc cảnh báo món `READY`, tiếp tục theo quy tắc hiện tại; chọn `Thanh toán Trực tiếp` → `Tiền mặt` → `Thanh toán tại quầy thu ngân`.
6. Mở Cashier Web URL ở mục A, đăng nhập `cashier_lan`, tìm payment theo bàn/amount/session trong `Thanh toán ứng dụng tại chỗ`; chỉ khi đã nhận tiền demo theo kịch bản mới bấm `Xác nhận đã thu tiền` và xác nhận hộp thoại.
7. Trên Samsung chờ cập nhật; nếu màn chờ không tự chuyển, bấm `Kiểm tra trạng thái thanh toán`. Đối chiếu `Thanh toán thành công!`, invoice và phiên `CLOSED`. Admin Web chỉ mở phụ để xem `Tổng quan`, `Món ăn`, `Bàn & QR` nếu cần trình bày.
