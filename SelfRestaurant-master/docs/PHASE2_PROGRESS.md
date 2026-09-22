# Phase 2 - Progress

## Muc tieu Phase 2

Tach du lieu that su theo database-per-service, dua moi service tu "DbContext rieng nhung schema rong" thanh "DbContext rieng va chi chua du lieu dung ownership".

## Trang thai hien tai

### Hoan tat dot 1 - Catalog service

Da hoan tat lat cat dau tien cho `Catalog`:

- `CatalogDbContext` duoc thu hep lai chi con cac entity thuoc ownership catalog:
  - `Branches`
  - `Categories`
  - `CategoryDish`
  - `DiningTables`
  - `DishIngredients`
  - `Dishes`
  - `Ingredients`
  - `MenuCategory`
  - `Menus`
  - `Restaurants`
  - `TableStatus`
- Da bo cac entity khong thuoc catalog ownership khoi model runtime cua `CatalogDbContext`.
- `CatalogDbBootstrapper` khong con seed `OrderStatus`.

### Hoan tat dot 2 - Orders service

Da hoan tat lat cat tiep theo cho `Orders`:

- `OrdersDbContext` duoc thu hep lai de chi giu cac entity phuc vu order workflow, kitchen workflow, order reporting hien tai va mot so du lieu tham chieu toi thieu:
  - `Branches`
  - `Categories`
  - `Customers`
  - `DiningTables`
  - `Dishes`
  - `LoyaltyCards`
  - `OrderItems`
  - `OrderStatus`
  - `Orders`
  - `TableStatus`
- Da bo cac entity billing/payment/report/auth thua khoi model runtime cua `OrdersDbContext`.
- `Orders` hien van giu mot so reference data nhu `Customers`, `Dishes`, `Branches` de tranh vo flow hien tai. Cac phu thuoc nay se tiep tuc duoc lam sach o Phase 3 thong qua internal API.

### Hoan tat dot 3 - Customers service

Da hoan tat lat cat tiep theo cho `Customers`:

- `CustomersDbContext` duoc thu hep lai de chi giu cac entity can cho:
  - customer login/register/profile
  - forgot/reset password
  - admin customer management
  - admin employee management
  - employee history view
- Model runtime hien giu:
  - `Bills`
  - `Branches`
  - `Customers`
  - `DiningTables`
  - `Dishes`
  - `EmployeeRoles`
  - `Employees`
  - `OrderItems`
  - `OrderStatus`
  - `Orders`
  - `PasswordResetTokens`
- Da bo khoi model runtime cac entity catalog/payment/report/view khong can thiet:
  - `Categories`
  - `CategoryDish`
  - `CustomerLoyalty`
  - `DishDetails`
  - `DishIngredients`
  - `Ingredients`
  - `LoyaltyCards`
  - `MenuCategory`
  - `Menus`
  - `OrderItemIngredients`
  - `PaymentMethod`
  - `PaymentStatus`
  - `Payments`
  - `Reports`
  - `Restaurants`
  - `TableNumbers`
  - `TableStatus`
- `CustomersDbBootstrapper` khong con seed `TableStatus`, chi giu `OrderStatus`.
- Service nay van con overlap voi `Identity` o muc route/nghiep vu admin-auth. Viec tach rach roi hon se duoc xu ly o Phase 3.

### Hoan tat dot 4 - Identity service

Da hoan tat lat cat tiep theo cho `Identity`:

- `IdentityDbContext` duoc thu hep lai de chi giu cac entity can cho:
  - customer login/register/change password/forgot-reset
  - staff login/change password/forgot-reset
  - admin employee/customer management
  - admin employee history view
- Model runtime hien giu:
  - `Bills`
  - `Branches`
  - `Customers`
  - `DiningTables`
  - `Dishes`
  - `EmployeeRoles`
  - `Employees`
  - `OrderItems`
  - `OrderStatus`
  - `Orders`
  - `PasswordResetTokens`
- Da bo khoi model runtime cac entity catalog/payment/report/view khong can thiet:
  - `Categories`
  - `CategoryDish`
  - `CustomerLoyalty`
  - `DishDetails`
  - `DishIngredients`
  - `Ingredients`
  - `LoyaltyCards`
  - `MenuCategory`
  - `Menus`
  - `OrderItemIngredients`
  - `PaymentMethod`
  - `PaymentStatus`
  - `Payments`
  - `Reports`
  - `Restaurants`
  - `TableNumbers`
  - `TableStatus`
- `IdentityDbBootstrapper` khong con seed `TableStatus`, chi giu `OrderStatus`.
- Service nay van con giu mot so entity thuoc order/billing de phuc vu lich su nhan vien. Phan nay se duoc tiep tuc lam sach o Phase 3 thong qua internal API.

### Hoan tat dot 5 - Billing service

Da hoan tat lat cat cuoi cung cua Phase 2 cho `Billing`:

- `BillingDbContext` duoc thu hep lai de chi giu cac entity can cho:
  - cashier order list
  - checkout / tao bill
  - cap nhat diem khach hang
  - giai phong ban sau thanh toan
  - bill history va branch report
- Model runtime hien giu:
  - `Bills`
  - `Branches`
  - `Customers`
  - `DiningTables`
  - `Dishes`
  - `Employees`
  - `OrderItems`
  - `OrderStatus`
  - `Orders`
  - `TableStatus`
- Da bo khoi model runtime cac entity catalog/auth/report/view khong can thiet:
  - `ActiveOrders`
  - `BranchRevenue`
  - `Categories`
  - `CategoryDish`
  - `CustomerLoyalty`
  - `DishDetails`
  - `DishIngredients`
  - `EmployeeRoles`
  - `Ingredients`
  - `LoyaltyCards`
  - `MenuCategory`
  - `Menus`
  - `OrderItemIngredients`
  - `PasswordResetTokens`
  - `PaymentMethod`
  - `PaymentStatus`
  - `Payments`
  - `Reports`
  - `Restaurants`
  - `TableNumbers`
- `BillingDbBootstrapper` van giu `OrderStatus` va `TableStatus`, vi checkout hien tai can ca hai.

## Kiem chung sau thay doi

Da build/khoi dong lai stack va chay lai smoke test:

- customer detailed: PASS
- admin flow: PASS
- chef <-> customer: PASS
- cashier <-> customer: PASS

Luu y:
- Co mot lan fail do chay `chef` va `cashier` song song cung dung chung table/order test.
- Sau khi reset du lieu va chay lai tuan tu, `cashier <-> customer` PASS.
- Day la loi test collision, khong phai loi nghiep vu hay loi refactor.

Kiem chung sau dot 3 (`Customers`):

- rebuild stack: PASS
- health checks 5100-5105: PASS
- customer detailed: PASS `18/18`
- chef <-> customer: PASS
- cashier <-> customer: PASS
- admin flow: PASS `6/6`

Kiem chung sau dot 4 (`Identity`):

- rebuild stack: PASS
- health checks 5100-5105: PASS
- customer detailed: PASS `18/18`
- chef <-> customer: PASS
- cashier <-> customer: PASS
- admin flow: PASS `6/6`

Kiem chung sau dot 5 (`Billing`):

- rebuild stack: PASS
- health checks 5100-5105: PASS
- customer detailed: PASS `18/18`
- chef <-> customer: PASS
- cashier <-> customer: PASS
- admin flow: PASS `6/6`

## Con lai cua Phase 2

Phase 2 da hoan tat cho:

1. `Catalog`
2. `Orders`
3. `Customers`
4. `Identity`
5. `Billing`

## Nguyen tac cho cac dot tiep theo

- Moi dot chi refactor 1 service hoac 1 lat cat nho.
- Sau moi dot phai:
  - build lai
  - khoi dong lai stack
  - reset data neu can
  - test lai full smoke flow lien quan
