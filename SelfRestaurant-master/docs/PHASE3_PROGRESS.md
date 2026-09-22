# Phase 3 - Progress

## Muc tieu Phase 3

Lam sach phu thuoc cheo giua cac service bang cach chuyen ownership nghiep vu va internal API ve dung service thay vi tiep tuc dua vao route/db overlap tu cac service khac.

## Dot 1 - Dua admin customer ownership ve Customers.Api

Da thuc hien lat cat dau tien cua Phase 3:

- `Customers.Api` da cong bo route ownership-dung cho quan tri khach hang:
  - `GET /api/customers/admin/customers`
  - `GET /api/customers/admin/customers/{id}`
  - `POST /api/customers/admin/customers`
  - `PUT /api/customers/admin/customers/{id}`
  - `POST /api/customers/admin/customers/{id}/deactivate`
- Van giu cac route cuoi thua ke duoi namespace `api/identity/admin/customers...` de tranh vo Gateway trong giai doan chuyen tiep.
- `Gateway.Api` da duoc cap nhat de `CustomersClient` uu tien goi namespace ownership-moi cua `Customers.Api`, va chi fallback sang namespace cu qua `Identity` neu can.

## Y nghia kien truc

Day la buoc dau tien de tach overlap giua `Customers.Api` va `Identity.Api`:

- `Customers.Api` bat dau tro thanh noi so huu dung nghiep vu `admin customer management`.
- `Identity.Api` co the duoc thu gon dan ve auth, employee, role va password reset trong cac dot tiep theo.
- Gateway van hoat dong on dinh nho co fallback compatibility.

## Kiem chung bat buoc sau dot 1

- Build lai stack
- Khoi dong lai service 5100-5105
- Test lai cac luong:
  - customer
  - chef <-> customer
  - cashier <-> customer
  - admin

## Dot tiep theo de xuat

## Dot 2 - Bat dau tach `Billing -> Customers/Orders`

Da thuc hien them mot lat cat chuyen tiep an toan:

- `Customers.Api` bo sung internal endpoint:
  - `POST /api/customers/{customerId}/loyalty/settle`
- `Orders.Api` bo sung internal endpoint:
  - `POST /api/orders/{orderId}/billing/complete`
- `Billing.Api` da duoc cap nhat de:
  - uu tien goi `Customers.Api` de cap nhat loyalty sau checkout
  - uu tien goi `Orders.Api` de chuyen order sang `COMPLETED` va giai phong ban
  - van giu fallback local de tranh vo flow trong giai doan chuyen tiep

## Y nghia kien truc cua dot 2

Day la buoc chuyen doi tu tu nhung dung huong:

- `Billing.Api` giam phu thuoc truc tiep vao ghi du lieu cheo mien cho `customer loyalty`
- `Billing.Api` giam phu thuoc truc tiep vao mutation `order/table state`
- nghiep vu duoc day ve dung service ownership:
  - loyalty -> `Customers.Api`
  - complete order/release table -> `Orders.Api`

## Buoc tiep theo de xuat

1. Dua `admin customer stats/identity overlap` ra khoi `Customers.Api` hoac `Identity.Api` theo ownership cuoi cung.
2. Cat tiep `Orders -> Catalog` de loai bo reference data schema rong.
3. Giam dan fallback local trong `Billing.Api` sau khi internal contracts on dinh.

## Dot 3 - Bat dau tach `Orders -> Catalog`

Da thuc hien them mot lat cat nghiep vu:

- `Catalog.Api` bo sung internal endpoints:
  - `GET /api/internal/tables/{tableId}`
  - `GET /api/internal/dishes/{dishId}`
  - `GET /api/internal/table-statuses/{statusCode}`
- `Orders.Api` bo sung `CatalogApiClient` va cau hinh goi sang `Catalog`.
- `OrdersController` da duoc cap nhat de:
  - uu tien xac thuc ban qua `Catalog.Api` trong `occupy/reset/get-or-create-order`
  - uu tien xac thuc mon qua `Catalog.Api` trong `add item`
  - uu tien lay `table status id` qua `Catalog.Api`
  - van giu fallback local tren du lieu reference de dam bao runtime an toan

## Y nghia kien truc cua dot 3

Day la buoc chuyen `source of truth` cho ban, mon va trang thai ban ve dung bounded context cua `Catalog`.

`Orders.Api` van con giu reference data local de tranh vo flow, nhung tu diem nay:

- `Orders` da bat dau phu thuoc vao internal contract cua `Catalog`
- thay vi tu giai quyet nghiep vu chi bang schema reference noi bo

## Tinh trang sau dot 3

- build stack: pass
- health checks 5100-5105: pass
- customer: pass
- chef <-> customer: pass
- cashier <-> customer: pass
- admin: pass

## Dot 4 - Customers/Identity ownership cleanup at Gateway boundary

Date: 2026-03-25

Changes:
- `IdentityClient` in Gateway now calls `Identity.Api` directly for all staff/admin flows:
  - admin stats
  - staff login/change-password/forgot/reset
  - staff profile update
  - admin roles/employees/history management
- Customer-facing auth flows still keep compatibility fallback during transition:
  - customer login/register/change-password/forgot/reset
- Legacy duplicate `api/identity/*` routes still remain inside `Customers.Api` temporarily as transition scaffolding, but Gateway no longer depends on them for staff/admin runtime.

Why this matters:
- Tightens service ownership at the runtime call boundary before deleting transitional overlap.
- Reduces implicit coupling between `Customers` and `Identity` for admin/staff concerns.
- Keeps low-risk migration posture because customer auth compatibility is preserved.

Validation:
- Rebuild succeeded for all services and gateway.
- Full smoke after reset passed:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS

## Dot 5 - Remove legacy identity routes from Customers.Api

Date: 2026-03-25

Changes:
- Removed duplicated legacy `api/identity/*` endpoints from `Customers.Api` for:
  - admin stats
  - staff login/change-password/profile-update
  - admin roles/employees/history management
- Removed legacy request/helper types in `CustomersController` that only existed to support those duplicated identity routes.
- Gateway was already switched in Dot 4 to call `Identity.Api` directly for these flows, so runtime ownership now matches service boundaries more closely.

Why this matters:
- Eliminates one of the clearest remaining service-boundary violations.
- Makes `Customers.Api` closer to customer-domain ownership only.
- Reduces hidden fallback behavior that can mask architecture drift.

Validation:
- Rebuild passed.
- Full smoke after reset passed:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS

## Dot 6 - Reduce remaining local fallback in Orders and Billing

Date: 2026-03-25

Changes:
- `Orders.Api`
  - `AddItem` now trusts `Catalog.Api` as the authoritative source for dish existence/availability/pricing.
  - Removed the extra local `Dishes` existence gate in that path.
- `Billing.Api`
  - Checkout now requires `Customers.Api` for customer loyalty snapshot/settlement when a customer is attached to the order.
  - Checkout now requires `Orders.Api` to complete order + release table.
  - Removed local DB mutation fallback for cross-domain ownership in checkout.

Why this matters:
- Tightens runtime ownership further:
  - dish validation belongs to `Catalog`
  - loyalty belongs to `Customers`
  - order completion/table release belongs to `Orders`
- Moves the system away from hidden local writes that blur service boundaries.

Validation:
- Rebuild passed.
- Full smoke after reset passed:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS

## Dot 7 - Lock customer auth ownership to Identity.Api

Date: 2026-03-25

Changes:
- `Gateway.Api` now calls `Identity.Api` directly for customer-facing auth flows as well:
  - customer login
  - customer register
  - customer change-password
  - customer forgot-password
  - customer reset-password
- Removed duplicated customer-auth endpoints from `Customers.Api`, including legacy overlap under:
  - `api/customers/*` auth routes
  - `api/identity/*` compatibility auth routes that were still hosted in `Customers.Api`
- `Customers.Api` remains responsible for customer profile/admin customer management/loyalty, while auth is now owned by `Identity.Api`.

Why this matters:
- Closes one of the last major runtime overlaps between `Customers` and `Identity`.
- Makes authentication ownership explicit and consistent:
  - auth/account/password reset -> `Identity.Api`
  - customer domain/profile/loyalty -> `Customers.Api`
- Reduces hidden coupling and removes ambiguity about which service is the source of truth for customer authentication.

Validation:
- Rebuild passed.
- Full smoke after reset passed:
  - customer 18/18
  - chef <-> customer PASS
  - cashier <-> customer PASS
  - admin 6/6 PASS

