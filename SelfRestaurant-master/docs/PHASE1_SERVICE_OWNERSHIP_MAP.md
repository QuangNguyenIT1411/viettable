# Phase 1 - Service Ownership Map

Tai lieu nay chot ranh gioi nghiep vu cho he thong SelfRestaurant o thoi diem hien tai. Muc tieu cua Phase 1 la thong nhat ro service nao so huu du lieu nao, endpoint nao la public/internal, va nhung dependency cheo mien nao phai duoc xu ly o Phase 2-3.

## 1. Nguyen tac chung

- Gateway MVC chi dong vai tro BFF/UI orchestration.
- Moi service phai co mot bounded context ro rang.
- Ownership du lieu duoc xac dinh theo nghiep vu, khong theo thuan tien code hien tai.
- Neu mot service can du lieu cua service khac, dich huong dich den la internal API hoac event, khong doc DB cheo mien.

## 2. Bounded Context va Ownership de xuat

### 2.1. Catalog Service

Project:
- `src/Services/SelfRestaurant.Catalog.Api`

Ownership dung:
- Branches
- DiningTables
- TableStatus
- Menus
- Categories
- MenuCategory
- Dishes
- Ingredients
- DishIngredients
- CategoryDish
- QR/table lookup metadata

Public API hien tai:
- `GET /api/branches`
- `GET /api/branches/{branchId}/tables`
- `GET /api/branches/{branchId}/menu`
- `GET /api/tables/qr/{code}`
- `GET/POST/PUT/DELETE /api/categories`
- Admin catalog routes cho dishes, ingredients, tables

Internal API can chot ve sau:
- validate branch/table
- validate dish availability
- get dish snapshot cho Orders
- table release/occupy contract do Orders/Billing goi

### 2.2. Orders Service

Project:
- `src/Services/SelfRestaurant.Orders.Api`

Ownership dung:
- Orders
- OrderItems
- OrderStatus
- Kitchen workflow state
- Customer confirm received
- Active cart/order lifecycle theo ban

Public API hien tai:
- occupy/reset table
- active order by table
- add/update/remove order item
- submit order
- confirm received
- chef queue/history/start/ready/serve/cancel
- admin stats/reports lien quan order

Internal API can chot ve sau:
- get order summary by orderId/tableId cho Billing
- complete order after checkout
- release order/table workflow

### 2.3. Customers Service

Project:
- `src/Services/SelfRestaurant.Customers.Api`

Ownership dung:
- Customer profile
- Loyalty balance
- Loyalty history
- Customer-facing profile metadata
- Customer order history view model (khong authoritative order storage)

Public API hien tai:
- customer login/register/profile/order history
- mot so route `api/identity/...` dang ton tai trong service nay

Luu y quan trong:
- `Customers.Api` hien dang chua ca customer profile va nhieu auth/admin route co namespace `api/identity/...`.
- Day la dau hieu bounded context chua sach. Ve dich den, auth/employee/role phai thuoc `Identity`, con `Customers` chi giu profile va loyalty.

### 2.4. Identity Service

Project:
- `src/Services/SelfRestaurant.Identity.Api`

Ownership dung:
- Customer account auth data
- Employee account auth data
- Employee roles
- Password reset tokens / auth metadata
- Claims/identity lookup

Public API hien tai:
- customer auth
- staff auth
- admin employee/customer management mot phan
- role lookup

Luu y quan trong:
- `Identity.Api` hien dang giao thoa mot phan voi `Customers.Api` o customer auth va admin-customer routes.
- Phase 3 can quyet dinh ro route nao giu lai tai Identity, route nao chuyen ve Customers.

### 2.5. Billing Service

Project:
- `src/Services/SelfRestaurant.Billing.Api`

Ownership dung:
- Bills
- Payments
- PaymentMethod
- PaymentStatus
- Cashier settlement/reporting
- Checkout result

Public API hien tai:
- cashier active orders by branch
- checkout
- cashier bill history
- cashier report

Internal API can chot ve sau:
- payment completed event/contract
- loyalty application contract voi Customers
- order completion contract voi Orders

## 3. Gateway Ownership

Project:
- `src/Gateway/SelfRestaurant.Gateway.Api`

Gateway chi nen so huu:
- Views
- MVC Controllers
- session/UI state tam thoi
- HTTP clients den cac service
- role-based route protection tai UI layer

Gateway khong nen so huu:
- nghiep vu cot loi
- authoratitive business data
- schema persistence chia se

## 4. Khoang trong hien tai can xu ly o giai doan sau

### 4.1. DbContext cua moi service van qua rong

Trang thai hien tai:
- Moi service da co `DbContext` rieng.
- Tuy nhien, cac `DbContext` nay van map gan nhu toan bo schema cu, khong chi map phan ownership thuc su.

Y nghia:
- day moi la "tach ky thuat" hon la "tach bounded context".
- Phase 2 phai cat gon model entity theo ownership trong tai lieu nay.

### 4.2. Overlap giua Customers va Identity

Trang thai hien tai:
- `CustomersController` dang co cac route `api/identity/...`
- `IdentityController` cung co cac route `api/identity/...`

Y nghia:
- chuc nang auth/customer profile dang chua tach dep.
- Phase 3 phai quyet dinh ro auth thuoc Identity, profile/loyalty thuoc Customers.

### 4.3. Orders va Billing dang co dau hieu dung schema rong

Trang thai hien tai:
- `Orders` va `Billing` van dang co kha nang dua vao schema rong thay vi snapshot/internal API toi gian.

Y nghia:
- Phase 2-3 phai thay the bang internal contracts.

## 5. Dinh nghia thanh cong cua Phase 1

Phase 1 duoc xem la hoan tat khi:

- Team thong nhat duoc ownership map nay.
- Moi entity chinh deu co service so huu ro rang.
- Moi overlap lon deu duoc danh dau de xu ly o Phase 2-3.
- Gateway duoc xac dinh ro chi la BFF/UI layer.


