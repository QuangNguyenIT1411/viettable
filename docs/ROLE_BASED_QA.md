# VietTable — Role-based functional QA (25/09/2026)

Mốc khảo sát ban đầu: `829b3e1`; lượt đóng gap tiếp tục từ `3282079` ngày 25/09/2026. Kết quả dưới đây phân biệt rõ kiểm chứng trên Samsung, browser, API và source. Đây là môi trường Docker local; xác nhận thu ngân trong tài liệu chỉ áp dụng cho dữ liệu demo, không đại diện cho việc đã nhận tiền ngoài đời.

## Kết quả đóng 5 gap

> **Cập nhật cuối (25/09/2026, session 5012):** Các gap còn lại đã được đóng bằng runtime thật. `cashier_huong` branch 2 không thấy request `4005`; POST acknowledge trả **403** và request giữ `OPEN`. Cashier `cashier_lan` xác nhận payment `4042` qua Cashier Web; Samsung tự sang Payment Success. Billing xác nhận invoice `4016` 120.000đ, Orders xác nhận session `5012` đóng và cleanup `4005` thành `CANCELLED`; request `4003` đã `COMPLETED` vẫn nguyên trạng.

| Gap | Kết quả | Bằng chứng mới |
| --- | --- | --- |
| Staff xử lý Service Request | **PASS** | UI E2E trước đó: request OPEN được Cashier nhận/hoàn tất, Samsung realtime đổi trạng thái; Customer hủy request OPEN khác. Bổ sung runtime: request `4005` (session `5012`, branch 1, Bàn 5) trước `OPEN`; Cashier Web branch 2 không thấy request; POST acknowledge → **403 forbidden**; sau đó request vẫn `OPEN`. |
| ONSITE chờ thanh toán tự cập nhật | **PASS (3 lượt trước + session 5012)** | Các lượt trước: split parts `3043`/`3044` và single payment `3045` tự cập nhật. Bổ sung: Samsung không thao tác trong lúc Cashier xác nhận payment `4042`; app tự hiện Payment Success, `120.000đ`, invoice `INV-7e6b319a406840eebfc50c9239fe6e6a`. Poll/manual retry không cần dùng. |
| Cashier khác chi nhánh | **PASS** | Kiểm tra ONSITE trước đó: Cashier sai chi nhánh không thấy payment và direct confirm trả **403**. Bổ sung Service Request: `cashier_huong` đăng nhập thật, runtime hiện tên chi nhánh **Gò Vấp (branch 2)** (khác ghi chú cũ “Quận 2”); list không có request `4005` của branch 1; POST acknowledge qua Gateway với session đó trả **403 forbidden**, state vẫn `OPEN`. |
| Admin CRUD | **PASS** | Admin Web tạo danh mục tạm `QA TEMP Role Audit 20260925`, đọc list, sửa mô tả/thứ tự và kiểm tra sau reload, vô hiệu hóa, rồi xóa bằng nút UI sau khi người dùng xác nhận. Reload còn 10 danh mục gốc, không còn QA TEMP. Gateway được sửa để Admin thấy cả inactive category, nên có thể bật lại/xóa. CHEF gọi Admin DELETE nhận HTTP **401**. Không chạm 52 món demo. |
| Split Bill session mới | **PASS** | Session `4013`, order `4018`, quote 120.000đ, split `2004`, parts `2008`/`2009` mỗi phần 60.000đ, payments `3043`/`3044`. Sau part 1, session còn ACTIVE, chưa có final invoice; sau part 2, split `PAID`, đúng **một** invoice `3017` tổng 120.000đ, session `CLOSED`. Samsung cập nhật tự động cả hai lần. Review theo invoice tạo và xóa được. |

**Kết luận:** các flow lịch sử đã nghiệm thu ở bảng này không bị thay đổi. Service Request, wrong-branch isolation, session-close cleanup, READY→SERVING và payment auto-update đều có bằng chứng runtime. Phạm vi role-based QA hiện **PASS**; các checklist tổng thể vẫn giữ unchecked cho những hạng mục ngoài scope chưa chạy.

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
| `chef_nam` — CHEF, `cashier_huong` — CASHIER | VERIFIED; `cashier_huong` đã login và test payment Quận 1 bị chặn | Quận 2 | Tài khoản đối chứng branch |

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

Kiểm chứng negative: Customer JWT gửi tới staff Chef/Cashier mutation nhận HTTP 401 do route yêu cầu staff session, không phải 403. CHEF `chef_hung` đăng nhập thật nhưng gọi Cashier confirm cho payment đã hoàn tất nhận 401; gọi Admin deactivate với customer ID không tồn tại và Admin DELETE category ID không tồn tại nhận 401. CASHIER `cashier_lan` đăng nhập thật nhưng gọi Chef start trên order đã hoàn tất nhận 401. CHEF Quận 1 gọi mutation cho order Quận 2 nhận 403; order Quận 2 giữ nguyên `CONFIRMED`. Gateway trước lượt audit chỉ lọc danh sách Chef theo branch nhưng mutation chưa ràng buộc branch; đã sửa trong `StaffChefGatewayController` và runtime-test lại sau rebuild, gồm cả route đọc ingredient của order Quận 2 (403). CASHIER Quận 2 gọi confirm payment Quận 1 `3043` nhận 403, payment vẫn `PENDING`; Cashier Quận 1 sau đó xác nhận được. Mã 401 là policy staff session hiện tại, không phải bằng chứng request chưa tới backend.

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

Snapshot trước task này: Customer tạo/đọc/hủy qua mobile Gateway; chưa có staff handler. Request `3004`/`WATER` khi đó ở session `4013` đã là lịch sử. Implementation hiện tại: Orders vẫn là owner; `OPEN→ACKNOWLEDGED→COMPLETED`, Customer chỉ được `OPEN→CANCELLED`; khi session đóng, OPEN/ACKNOWLEDGED được CANCELLED. Cashier Web là staff UI được chọn vì không có waiter frontend; `WAITER` có trong Identity nhưng không có UI phục vụ. Mobile hub `serviceRequestUpdated` được phát tới customer group sau thao tác staff. Samsung E2E mới được ghi ở phần cập nhật cuối tài liệu.

| Request trên Android | Backend type | Handler role/UI |
| --- | --- | --- |
| Thêm nước lọc / Trà đá | `WATER` | `CASHIER` / Cashier Web → Yêu cầu phục vụ |
| Gọi nhân viên | `CALL_STAFF` | `CASHIER` / Cashier Web → Yêu cầu phục vụ |
| Dụng cụ | `UTENSILS` | `CASHIER` / Cashier Web → Yêu cầu phục vụ |
| Khác | `OTHER` | `CASHIER` / Cashier Web → Yêu cầu phục vụ |

State/UI mapping: `OPEN` → Android `CHỜ TIẾP NHẬN`; `ACKNOWLEDGED` → `ĐANG XỬ LÝ`; `COMPLETED` → `ĐÃ HOÀN TẤT`; `CANCELLED` → `ĐÃ HỦY`. UI Staff list polling 5 giây; Android nhận event customer-scoped rồi đọc lại REST canonical. Cashier list chỉ gồm OPEN/ACKNOWLEDGED của branch đang đăng nhập. Mutation yêu cầu BranchId từ Gateway staff session và Orders so với BranchId lưu trên request + context DiningSession; contract test cùng runtime test branch 2 → 403 xác nhận guard.

## F. Cashier workflow

Trên Samsung từ phiên bàn 5: `Thanh toán` → cảnh báo còn món `READY` → `Vẫn yêu cầu thanh toán` → hóa đơn 175.000đ → `Chọn phương thức thanh toán` → `Thanh toán Trực tiếp` → `Tiền mặt` → `Thanh toán tại quầy thu ngân` → `Xác nhận yêu cầu thanh toán`. Cảnh báo `READY` là confirm dialog, không phải hard gate.

Lượt đầu: Cashier Web `cashier_lan` chi nhánh Quận 1 thấy payment `3042` cho session `4012`, Bàn 5, `175.000 đ`, `CASH / CASHIER`, `Chờ thu tiền`; xác nhận thành `SUCCEEDED`, invoice `3016`, session `CLOSED`. Lượt đóng gap: Cashier Web xác nhận payments `3043`, `3044` của split session `4013` và payment `3045` của single session `4014`; Samsung **không chạm** trong lúc chờ và tự chuyển đúng màn trong cả 3 lần. Poll đang chạy 3 giây/lần khi màn active; sau lỗi mạng/API chờ 5 giây rồi tự thử lại. Nút `Kiểm tra trạng thái thanh toán` giữ làm fallback, không phải bước bắt buộc. Chỉ test dữ liệu demo local, không chứng minh đã thu tiền thật ngoài đời.

Không dùng phần `Chi tiết đơn hàng`/`Xác nhận thanh toán` legacy của Cashier để thu lần hai; chỉ dùng vùng `Thanh toán ứng dụng tại chỗ` cho Android ONSITE.

## G. Admin functions thực tế

Admin Web login bằng `admin`, dashboard hiện branch Quận 1 và menu: `Tổng quan`, `Danh mục`, `Nguyên liệu`, `Quản lý kho`, `Món ăn`, `Bàn & QR`, `Nhân viên`, `Khách hàng`, `Báo cáo`, `Cài đặt`. Gateway có API tương ứng cho category/menu, ingredient/inventory, table, employee, customer, settings. Read-only runtime: dashboard hiện doanh thu hôm nay 175.000đ sau giao dịch demo; màn `Món ăn` tải 52 món gồm ảnh, giá và availability; màn `Bàn & QR` tải 20 bàn Quận 1 cùng QR code. Browser console các màn này không có error. Chưa chạy mutation hoặc từng màn CRUD; không sửa dữ liệu 52 ảnh/menu demo để spot-check.

Để kiểm tra thủ công an toàn: đăng nhập Admin Web bằng `admin`, mở `Tổng quan` và đối chiếu doanh thu giao dịch vừa thanh toán; mở `Món ăn` và kiểm tra tên/ảnh/giá/khả dụng của một món; mở `Bàn & QR` và xác nhận bàn vừa dùng thuộc đúng chi nhánh. Lượt đóng gap đã nghiệm thu CRUD **danh mục tạm** `QA TEMP Role Audit 20260925` qua UI: create, read, update persist sau reload, vô hiệu hóa, xóa vĩnh viễn theo xác nhận của người dùng, reload còn 10 danh mục gốc. Lỗi Admin list ẩn inactive category được sửa ở Gateway `GetCategories`; không đổi dữ liệu 52 món. Các menu còn lại chỉ liệt kê theo source/UI, chưa tuyên bố mọi CRUD đã PASS.

## H. Android realtime

Chef chuyển một item sang `PREPARING`: Samsung item A hiện `ĐANG NẤU`, item B vẫn `ĐÃ NHẬN`. Chef chuyển item A sang `READY`: Samsung hiện `SẴN SÀNG`, item B vẫn `ĐANG NẤU`. Lượt mới order `4019`/session `4014` trên Chef Web chuyển `BẮT ĐẦU NẤU` → `Hoàn thành`; Samsung tự hiện `ĐANG NẤU` → `SẴN SÀNG` không refresh. Mapping Android của Order `READY`/`SERVING` và Item `SERVING` trước QA bị fallback sai; đã sửa và thêm unit test. Cashier confirm ONSITE khiến Samsung tự cập nhật 3/3 lần: split part 1 về Split Bill, final part và single payment tới Payment Success. REST Payment/Invoice là canonical; poll/realtime chỉ kích hoạt đọc lại.

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
| Service request Customer → staff → Customer (snapshot trước triển khai) | FAIL/NOT IMPLEMENTED khi đó | request 3004 WATER tạo thật, khi đó chưa có staff endpoint/UI; E2E đã PASS sau khi feature được thêm (xem cập nhật cuối tài liệu) |
| Checkout → ONSITE pending → Cashier thấy đúng payment | PASS | payment 3042, 175.000đ |
| Cashier confirm → Invoice → CLOSED | PASS tại backend và Android sau bấm kiểm tra | Billing SUCCEEDED, invoice 3016, Orders CLOSED |
| Android tự điều hướng payment không chạm | PASS 3/3 | payments 3043, 3044, 3045, khoảng 2–5 giây; nút thủ công là fallback |
| Split Bill cross-role session mới | PASS | session 4013, split 2004, quote/parts 120.000đ = 60.000đ + 60.000đ; sau part 1 ACTIVE; sau part 2 PAID, 1 invoice 3017, CLOSED |
| Customer Web login, branch/menu | PASS smoke | Home → chọn Quận 1 → chọn Bàn 1 → menu có món/giá/ảnh; đăng nhập `Quang` hiện tên Nguyễn Vinh Quang; đã reset table context tạm; browser console không có error |
| Admin dashboard, món ăn và Bàn & QR read-only | PASS smoke | doanh thu 175.000đ, 52 món, 20 bàn Quận 1, không console error |
| Admin category CRUD qua Web | PASS | danh mục QA tạm create/read/update/inactivate/delete, reload còn 10 mục gốc; Chef Admin DELETE 401 |
| Wrong-branch Cashier | PASS | cashier_huong Quận 2 list không thấy payment 3043 Quận 1; direct confirm 403, PENDING không đổi |
| Main role regression mới | PASS | session 4014, order 4019 → Chef PREPARING/READY → Android tự cập nhật → payment 3045 → Cashier UI confirm → invoice → CLOSED |

Kiểm thử lượt đóng gap: `.\gradlew.bat :app:assembleDebug :app:testDebugUnitTest -PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100 --console=plain` PASS. APK cài trên Samsung; `scripts/test-android-instrumentation.ps1 -Tier All` PASS 14 tests (13 pass, 1 opt-in skip). Docker SDK `dotnet test ...Backend.ContractTests.csproj -c Release` PASS 110/110. Docker build sạch riêng từng stage `customer_frontend`, `chef_frontend`, `cashier_frontend`, `admin_frontend` PASS; Customer build giữ ba cảnh báo ảnh hero resolve lúc runtime, Cashier có Rollup comment warning trong dependency SignalR. `docker compose build gateway` PASS và Gateway `/readyz` HTTP 200 sau khi deploy. Android app process đang chạy, `adb reverse tcp:5100 tcp:5100` còn hoạt động; không có `FATAL EXCEPTION` sau khởi động kiểm tra. Runtime cross-role và security kết quả được nêu tại bảng gap ở đầu tài liệu.

## J. Known limitations và mức độ

| Severity | Finding | Tình trạng |
| --- | --- | --- |
| HIGH | Chef mutation trước QA thiếu branch authorization mặc dù list đã lọc branch | Đã sửa và đã test Q1→Q2 HTTP 403; đây là regression đã đóng ở lượt QA trước |
| PASS | Service Request staff handler/UI, realtime, branch isolation và session-close cleanup | Samsung + Cashier UI E2E PASS; request `4005` branch 2 → 403 và cleanup thành CANCELLED khi session đóng |
| PASS | Android `Tôi đã nhận món` để chuyển `READY→SERVING` | Samsung item-level E2E PASS; item khác giữ PREPARING |
| INFO | Cashier UI còn một số giao dịch split cũ pending từ ngày khác | Không xác nhận nhầm; lượt mới 4013/2004 đã PASS |

**Kết luận tại snapshot cũ:** `ROLE-BASED QA: NOT READY` do hai tính năng trên còn thiếu. Bằng chứng runtime mới ở phần cuối đã đóng các gap; trạng thái hiện tại là `ROLE-BASED QA: PASS`.

## K. Kịch bản demo vai trò 8–12 phút

1. Mở Docker Gateway, đặt `adb reverse tcp:5100 tcp:5100`, mở VietTable trên Samsung; đăng nhập `Quang`. Chạy `demo-find-table.ps1 -BranchId 1` ngay trước khi chọn mã QR bàn trống.
2. Trên Samsung: nhập mã QR bàn vừa tìm, bắt đầu phiên, chọn hai món, bấm `Gửi đơn gọi món tới Bếp`. Ghi mã bàn và order trên màn.
3. Mở Chef Web URL ở mục A, đăng nhập `chef_hung`, tìm đúng bàn ở `Chờ chế biến`. Bấm `Bắt đầu` một món rồi nhìn Samsung tự đổi `ĐANG NẤU` trong `Tiến độ món ăn`, không refresh.
4. Ở Chef, bấm `BẮT ĐẦU NẤU` và `Hoàn thành` để hai món sang `Sẵn sàng`; trên Samsung đối chiếu từng món `SẴN SÀNG`. Có thể gọi thêm một món để minh họa order round thứ hai cùng phiên.
5. Trên Samsung chọn thanh toán, đọc cảnh báo món `READY`, tiếp tục theo quy tắc hiện tại; chọn `Thanh toán Trực tiếp` → `Tiền mặt` → `Thanh toán tại quầy thu ngân`.
6. Mở Cashier Web URL ở mục A, đăng nhập `cashier_lan`, tìm payment theo bàn/amount/session trong `Thanh toán ứng dụng tại chỗ`; chỉ khi đã nhận tiền demo theo kịch bản mới bấm `Xác nhận đã thu tiền` và xác nhận hộp thoại.
7. Trên Samsung **không chạm màn chờ** và đợi tự chuyển trong khoảng 2–5 giây sau Cashier confirm; `Kiểm tra trạng thái thanh toán` chỉ là fallback khi mạng gián đoạn. Đối chiếu `Thanh toán thành công!`, invoice và phiên `CLOSED`. Admin Web chỉ mở phụ để xem `Tổng quan`, `Món ăn`, `Bàn & QR` nếu cần trình bày.

Hướng dẫn nhanh (snapshot cũ): **Bếp** mở URL Chef ở mục A, login `chef_hung`, tìm order mới đúng bàn ở `Chờ chế biến`, bấm `BẮT ĐẦU NẤU` rồi `Hoàn thành`; nhìn Samsung tự đổi `ĐANG NẤU` rồi `SẴN SÀNG`. **Thu ngân** mở URL Cashier ở mục A, login `cashier_lan`, tìm payment trong `Thanh toán ứng dụng tại chỗ` theo bàn/số tiền, bấm `Xác nhận đã thu tiền` và xác nhận hộp thoại; Samsung tự vào Success. **Admin** mở URL Admin ở mục A, login `admin`, kiểm tra `Tổng quan`, `Danh mục`, `Món ăn`, `Bàn & QR`; chỉ tạo/sửa/xóa entity QA tạm khi được phép.

## Service Request và xác nhận món trên Android (cập nhật)

**Owning service/database:** Orders API / `RESTAURANT_ORDERS`. Không chuyển owner và không có database cross-read. API Customer hiện tại vẫn dùng `/api/mobile/v1/dining-sessions/{sessionId}/service-requests` (create/list) và `/api/mobile/v1/service-requests/{id}/cancel`.

**Handler:** `CASHIER` chi nhánh hiện tại (cũng áp dụng policy sẵn có cho `MANAGER`/`ADMIN`); chọn vì chưa có waiter/staff UI riêng và Cashier là staff web branch-scoped đã có. Mở `http://localhost:5100/app/cashier/Staff/Account/Login`, đăng nhập `cashier_lan` ở Quận 1; dashboard có mục `Yêu cầu phục vụ`. API staff qua `/api/gateway/staff/cashier/service-requests`, `/.../{id}/acknowledge`, `/.../{id}/complete`; Orders enforce branch, session/table/customer ownership context và transition. UI tự poll 5s, Android có event `serviceRequestUpdated` rồi REST resync.

| From | Actor | UI action | To | Android |
| --- | --- | --- | --- | --- |
| OPEN | Customer | Hủy (chỉ trước staff nhận) | CANCELLED | ĐÃ HỦY |
| OPEN | CASHIER/MANAGER/ADMIN branch-scoped | Tiếp nhận | ACKNOWLEDGED | ĐANG XỬ LÝ |
| ACKNOWLEDGED | CASHIER/MANAGER/ADMIN branch-scoped | Hoàn tất | COMPLETED | ĐÃ HOÀN TẤT |
| OPEN hoặc ACKNOWLEDGED | Orders khi session đóng | cleanup | CANCELLED | ĐÃ HỦY |

**READY→SERVING:** Order detail Android hiển thị nút `Tôi đã nhận món` riêng từng item chỉ khi status `READY`. Request có order/item IDs; Gateway kiểm tra customer sở hữu Order và item thuộc đúng Order; Orders kiểm tra lại `Order.CustomerID`, session writable, item ID và READY status. Click disable tới callback; nếu status SERVING đã được áp dụng thì retry là idempotent no-op. Samsung session `5012`, order `5016`: Chef Web chuyển `Bánh mì ốp la` sang `READY`, Android hiển thị nút; sau khi Customer bấm thì label thành `ĐÃ PHỤC VỤ`, trong khi `Bò né` vẫn `ĐANG NẤU`. Đây là item-level E2E thật, không dùng trạng thái giả.

**Runtime evidence bổ sung, 25/09/2026:** `cashier_huong` đăng nhập qua Cashier Web với role `CASHIER`, branch 2 / Chi nhánh Gò Vấp. Staff list không trả request `4005` (branch 1); POST `/api/gateway/staff/cashier/service-requests/4005/acknowledge` bằng session staff hợp lệ trả **403** với code `forbidden`. Trước/sau đều đọc qua Q1 Cashier/Orders API và là `OPEN`.

Trước thao tác thanh toán: payment `4042` / `PAY-20260925154843-62EEA9CE200A4691A87A0` là `PENDING`, 120.000đ, session `5012` là `PAYMENT_REQUESTED`, request `4005` là `OPEN`; Billing invoice endpoint trả 404 `invoice_not_found`. Cashier UI hiển thị chính xác Bàn 5, 120.000đ, `CASH / CASHIER`; `cashier_lan` bấm `Xác nhận đã thu tiền` và chấp nhận hộp thoại xác nhận. Gateway→Billing confirm trả HTTP 200 sau **622 ms**, không có lỗi Billing; Orders `/payment-succeeded` đóng session.

Sau thao tác: Billing API trả payment `SUCCEEDED`; invoice `4016` / `INV-7e6b319a406840eebfc50c9239fe6e6a`, 120.000đ, gắn payment `4042`; Orders API trả session `5012` `CLOSED` (kết thúc `16:07:51.898 UTC`). Request `4005` đổi `OPEN → CANCELLED` cùng thời điểm đóng session. Request `4003` đã `COMPLETED` vẫn `COMPLETED`, `CancelledAtUtc=null`; điều này cũng được bảo vệ bằng targeted source-contract test. Samsung SM-A528B, không chạm sau khi Cashier xác nhận, tự hiện `Thanh toán thành công!`, `120.000đ`, invoice code trên.

**Điều tra timeout cũ:** Trước lần xác nhận UI mới, Billing vẫn canonical `PENDING`, session `PAYMENT_REQUESTED`, invoice 404; Gateway/Billing log trong 2 giờ không có request confirm nào trước lần thành công. Cashier UI dùng native `window.confirm`; lần click gây timeout trước đó phù hợp với hộp thoại xác nhận chưa được xử lý bởi trình điều khiển browser, không phải call Billing treo. Ở lần này chấp nhận đúng hộp thoại, Gateway nhận HTTP 200 sau 622 ms và không có Billing error. Log có đúng một POST confirm cho payment này. Endpoint không nhận idempotency key riêng; Billing tuần tự hóa theo quote lock và `ConfirmAsync` trả lại payment nếu đã `SUCCEEDED`, đồng thời tránh phát hành invoice trùng. Không sửa timeout bằng cách tăng timeout hay thêm workaround vì không tái hiện lỗi backend.

**Kiểm thử lượt tiếp nối:** Android `:app:assembleDebug` và `:app:testDebugUnitTest` PASS; Orders API `dotnet build` PASS (0 warning/error); toàn bộ Backend ContractTests PASS (114/114), gồm test cleanup mới; Cashier Web `npm run build` PASS. Android instrumentation không chạy lại vì Android source không đổi trong lượt này; lần chạy trước trên cùng working-tree Android changes là 13 PASS, 1 opt-in SKIPPED.

**Kết luận: ROLE-BASED QA: PASS.** Không có database reset hoặc trạng thái payment/request được sửa trực tiếp; payment được xác nhận qua Cashier Web thật, branch-negative mutation chỉ dùng để xác minh authorization.
