# SelfRestaurant - Ke hoach nang cap len microservice chuan

Tai lieu nay thay the lo trinh migration cu. Muc tieu moi khong chi la "tach service de chay duoc", ma la dua he thong SelfRestaurant toi muc:

- Chuc nang nghiep vu dat muc 9-10/10 de demo, UAT va nop do an.
- Kien truc dat muc microservice chuan hon ve bounded context, data ownership, integration va operability.

## 1. Danh gia hien trang

### 1.1. Diem da lam duoc

He thong hien tai da co cac thanh phan microservice ro rang:

- `SelfRestaurant.Gateway.Mvc` dong vai tro Gateway/BFF cho web.
- `SelfRestaurant.Catalog.Api`
- `SelfRestaurant.Orders.Api`
- `SelfRestaurant.Customers.Api`
- `SelfRestaurant.Identity.Api`
- `SelfRestaurant.Billing.Api`

Cac luong nghiep vu chinh da chay duoc va da duoc smoke test:

- Khach hang
- Khach hang <-> Bep
- Cashier <-> Khach hang
- Admin

Moi service da co `DbContext` rieng va connection string rieng. Day la buoc tien de tot de chuyen sang database-per-service.

### 1.2. Khoang trong so voi microservice chuan

He thong chua dat muc microservice chuan hoan toan vi cac ly do chinh sau:

1. Bounded context chua duoc cat gon den muc thuan theo mien nghiep vu.
2. Model persistence trong tung service van chua tinh gon theo ownership du lieu.
3. Giao tiep cheo mien van chua duoc chuan hoa het thanh internal API/event.
4. Chua co event-driven integration cho cac quy trinh quan trong.
5. Chua day du cac nang luc production-grade nhu tracing, resilience, outbox, central config, versioning.

## 2. Muc tieu dich

Sau khi hoan tat 5 giai doan duoi day, he thong phai dat duoc cac tieu chi sau:

### 2.1. Ve chuc nang

- Toan bo luong `khach hang -> bep -> cashier -> admin` hoat dong on dinh.
- Khong con loi blocking trong UI, session, thanh toan, loyalty, reset ban, popup thong tin mon.
- Build sach, khoi dong lai de test duoc tu dong va test tay.
- Co bo smoke test va regression test de xac nhan lai sau moi thay doi lon.

### 2.2. Ve kien truc microservice

- Moi service chi so huu du lieu thuoc bounded context cua minh.
- Moi service co migration rieng, schema rieng, DB rieng, seed rieng.
- Khong service nao doc truc tiep bang du lieu thuoc mien cua service khac.
- Moi phu thuoc cheo mien di qua internal HTTP API hoac event bus.
- Cac quy trinh quan trong ho tro eventual consistency va co co che retry/outbox.
- Co health check, readiness, logging, correlation id, tracing va resilience co ban.

## 3. Lo trinh 5 giai doan

## Giai doan 1 - Dong bang hien trang va chuan hoa bounded context

### Muc tieu

Chot phien ban hien tai dang chay tot, khong de code tiep tuc drift, dong thoi dinh nghia lai ranh gioi mien du lieu cho tung service.

### Cong viec can lam

1. Dong bang phien ban hien tai de lam baseline on dinh.
2. Tao tai lieu "service ownership map" cho 5 service:
   - `Catalog`
   - `Orders`
   - `Customers`
   - `Identity`
   - `Billing`
3. Liet ke entity nao thuoc service nao.
4. Liet ke endpoint cong khai va endpoint noi bo cua tung service.
5. Danh dau nhung diem doc cheo du lieu hien con ton tai.
6. Chot ro Gateway chi lam BFF/UI orchestration, khong chua business ownership.

### Ownership de xuat

- `Catalog`: branches, dining tables, menus, categories, dishes, ingredients, menu-category mapping.
- `Orders`: orders, order items, order statuses, kitchen workflow, customer-confirm-received flow.
- `Customers`: customer profile, loyalty balance, loyalty history, customer-facing profile metadata.
- `Identity`: accounts, employees, roles, password reset, auth metadata, session/token identity data.
- `Billing`: bills, payments, payment methods, payment statuses, cashier settlement, billing reports.

### Dau ra bat buoc

- Tai lieu ownership map ro rang.
- Danh sach cac bang va entity duoc phan cong 1-1 cho service.
- Danh sach dependency cheo service can duoc loai bo o giai doan sau.

### Tieu chi hoan thanh

- Team co the chi ro moi entity thuoc service nao ma khong mo ho.
- Khong con tranh cai ve ownership du lieu khi bat dau tach thuc su.

## Giai doan 2 - Tach du lieu that su theo database-per-service

### Muc tieu

Bien viec "moi service co DbContext rieng" thanh "moi service chi chua du lieu dung cua minh".

### Cong viec can lam

1. Cat gon model persistence trong tung service:
   - Xoa entity thua khong thuoc ownership service do.
2. Tao migration rieng cho tung service.
3. Tao script bootstrap/schema rieng cho tung service.
4. Bo dan cac bridge tam thoi, synonym hoac schema compatibility layer cu.
5. Kiem tra lai appsettings va startup de dam bao moi service chi ket noi DB cua minh.
6. Tach seed data theo mien:
   - catalog seed
   - identity seed
   - customers seed
   - orders seed toi thieu cho test
   - billing seed/reference

### Nguyen tac quan trong

- `Orders` khong duoc luu bang `Dishes`, `Branches`, `Customers`, `Employees` nhu authoritative source.
- `Billing` khong duoc luu hay doc toan bo domain objects cua `Orders/Customers/Identity` lam schema goc.
- Neu can snapshot de tinh bill, chi luu snapshot can thiet cho nghiep vu billing.

### Dau ra bat buoc

- 5 database dung nghia, khong chi khac ten.
- 5 bo migration rieng.
- 5 bo seed/schema script rieng.

### Tieu chi hoan thanh

- Khong service nao con can bang du lieu ngoai ownership de khoi dong/van hanh chinh.
- Kiem tra code cho thay `DbContext` cua moi service chi map nhung entity dung ownership cua service do.

## Giai doan 3 - Chuan hoa giao tiep noi bo giua service

### Muc tieu

Loai bo doc DB cheo mien, chuyen sang internal API va contract ro rang.

### Cong viec can lam

1. Xac dinh cac dependency chinh:
   - `Orders -> Catalog`: validate branch, table, dish.
   - `Orders -> Customers`: customer snapshot can thiet.
   - `Billing -> Orders`: order summary, order total, order state.
   - `Billing -> Customers`: loyalty balance, loyalty apply/earn.
   - `Billing -> Identity`: cashier/account metadata neu can.
2. Tao internal client va DTO/contract rieng cho cac dependency tren.
3. Refactor service layer de moi phu thuoc cheo mien deu qua HTTP API.
4. Dinh nghia chuan response, error model, validation va timeout.
5. Bo sung resilience co ban:
   - retry nhe
   - timeout ro rang
   - fallback khi hop ly

### API noi bo de xay dung

- `Catalog API`
  - validate branch/table
  - validate dish/menu visibility
  - lay dish snapshot cho order
- `Orders API`
  - get order summary by order/table
  - update state
  - confirm received
  - complete/release hooks sau billing
- `Customers API`
  - get customer profile
  - get/apply/earn loyalty
  - get loyalty history
- `Identity API`
  - get employee/account metadata
  - auth/claims lookup noi bo
- `Billing API`
  - process payment using order summary + loyalty summary
  - issue bill and payment result

### Dau ra bat buoc

- Moi service co bo internal contracts ro rang.
- Khong con duong doc truc tiep DB service khac de lay du lieu nghiep vu.

### Tieu chi hoan thanh

- Tim kiem static trong code khong con logic nghiep vu chinh phu thuoc schema service khac.
- Core flows van pass sau refactor.

## Giai doan 4 - Event-driven integration va eventual consistency

### Muc tieu

Giam coupling dong bo va dua he thong toi huong microservice thuc thu hon.

### Cong viec can lam

1. Chon message broker:
   - RabbitMQ la phuong an phu hop cho do an.
2. Tao event contracts cho cac su kien chinh:
   - `OrderSubmitted`
   - `OrderPreparing`
   - `OrderReady`
   - `OrderReceivedConfirmed`
   - `PaymentCompleted`
   - `LoyaltyPointsGranted`
   - `TableReleased`
3. Ap dung `outbox pattern` cho `Orders` va `Billing`.
4. Tinh lai cac flow dang dong bo thanh bat dong bo neu hop ly:
   - sau thanh toan -> cong diem -> giai phong ban
   - order ready -> thong bao khach
5. Bo sung dead-letter/retry strategy cho event handler.

### Dau ra bat buoc

- Message broker chay duoc trong local/dev.
- Service publish/consume event cho 2-3 flow quan trong.
- Event flow duoc log va truy vet duoc.

### Tieu chi hoan thanh

- Cac luong chinh khong phu thuoc qua nhieu vao chain HTTP dong bo.
- Loyalty/update table/payment completion co the van hoan tat khi mot service khac cham trong ngan han.

## Giai doan 5 - Hardening, observability va nang cap chat luong len 9-10

### Muc tieu

Dat muc san sang demo/bao cao manh hon va gan hon voi production.

### Cong viec can lam

1. Chuan hoa auth va security:
   - token/claims ro rang hon
   - giam phu thuoc session neu co the
   - role policy ro rang cho Gateway va service
2. Bo sung observability:
   - structured logging
   - correlation id end-to-end
   - distributed tracing co ban
   - dashboard health
3. Bo sung resilience:
   - retry policy
   - timeout policy
   - circuit breaker cho internal HTTP clients
4. Chuan hoa API:
   - versioning
   - swagger docs day du
   - error contract nhat quan
5. Nang cap chat luong chuc nang:
   - regression test day du hon cho customer/chef/cashier/admin
   - test data reset reproducible
   - build/release script sach
   - docker compose/onboarding docs ro rang
6. Chuan hoa tai lieu:
   - architecture overview
   - deployment guide
   - local setup guide
   - troubleshooting guide

### Dau ra bat buoc

- Build sach, startup sach, health day du.
- Log/tracing du de debug nhanh.
- Full regression test pass on dinh.
- Tai lieu day du de demo va ban giao.

### Tieu chi hoan thanh

- He thong dat muc 9-10/10 ve trai nghiem demo va do on dinh chuc nang.
- Kien truc co the bao ve duoc khi giang vien hoi sau ve microservice standard.

## 4. Thu tu uu tien thuc thi

Neu thoi gian co han, uu tien nen la:

1. Giai doan 1
2. Giai doan 2
3. Giai doan 3
4. Giai doan 5
5. Giai doan 4

Ly do:
- Giai doan 1-3 giai quyet cai cot loi cua microservice la ownership va coupling.
- Giai doan 5 giup he thong dep, on dinh, de demo va bao cao.
- Giai doan 4 la huong microservice dep hon nhat, nhung cung ton cong nhat. Neu co du thoi gian thi lam, neu khong co the lam mot phan de minh hoa.

## 5. Dinh nghia thanh cong cuoi cung

He thong duoc xem la hoan thanh tot theo huong microservice khi:

- Luong chuc nang A-Z pass on dinh: customer -> kitchen -> cashier -> admin.
- Moi service co data ownership ro rang, migration rieng, DB rieng, schema rieng.
- Khong con doc DB cheo service trong nghiep vu chinh.
- Co internal API contracts ro rang giua service.
- Co it nhat 1-2 flow quan trong di qua event bus.
- Build, run, reset data, test smoke deu co script ro rang.
- Tai lieu va demo noi duoc vi sao day la microservice that su, khong chi la monolith bi cat thanh nhieu project.

## 6. Ke hoach trien khai thuc te de xuat

### Milestone A - 1 tuan

- Hoan tat Giai doan 1
- Bat dau Giai doan 2 voi `Catalog`, `Orders`

### Milestone B - 2 tuan

- Hoan tat Giai doan 2 cho ca 5 service
- Hoan tat Giai doan 3 cho `Orders`, `Billing`, `Customers`

### Milestone C - 3 tuan

- Hoan tat Giai doan 3 toan bo
- Bat dau Giai doan 5: logging, docs, resilience co ban, build scripts

### Milestone D - 4 tuan

- Chon 1-2 flow quan trong de event hoa trong Giai doan 4
- Chot regression test + docs demo + hardening

## 7. Khuyen nghi thuc te cho project hien tai

Neu muc tieu la vua nop duoc, vua nang cap cho dep:

- Ngan han: hoan tat Giai doan 1, 2, 3 va mot phan Giai doan 5.
- Trung han: them mot phan Giai doan 4 cho thanh toan/loyalty/thong bao mon san sang.
- Bao cao/do an: mo ta trung thuc rang he thong hien tai da dat microservice-style architecture, va roadmap tren la lo trinh dua len microservice chuan.

