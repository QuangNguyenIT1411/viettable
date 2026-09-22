# Phase 1 - Baseline Status

Tai lieu nay chot hien trang ky thuat cua project truoc khi buoc sang Phase 2.

## 1. Kien truc dang chay

- Gateway:
  - `src/Gateway/SelfRestaurant.Gateway.Api`
- Services:
  - `SelfRestaurant.Catalog.Api`
  - `SelfRestaurant.Orders.Api`
  - `SelfRestaurant.Customers.Api`
  - `SelfRestaurant.Identity.Api`
  - `SelfRestaurant.Billing.Api`

## 2. Hien trang build va startup

Hien trang du kien cho Phase 1:
- solution build duoc
- Gateway va 5 service co health/readiness endpoint
- co script startup/rebuild va script smoke test

Script chinh:
- `scripts/start-phase1.ps1`
- `scripts/reset-orders-bills-only.ps1`
- `scripts/test-customer-detailed-new.ps1`
- `scripts/test-chef-customer-old-new.ps1`
- `scripts/test-cashier-customer-old-new.ps1`
- `scripts/test-admin-old-new.ps1`

## 3. Hien trang chuc nang

Cac luong da dat muc smoke-test tot:
- Khach hang
- Khach hang <-> Bep
- Cashier <-> Khach hang
- Admin

Ket luan:
- Project da du on dinh de dung lam baseline cho refactor tiep theo.
- Moi thay doi o Phase 2 tro di phai build lai va smoke test lai tren baseline nay.

## 4. Rui ro chinh truoc khi qua Phase 2

1. Moi service da co `DbContext` rieng nhung chua co ownership model sach.
2. Van ton tai `src/Shared/SelfRestaurant.Database`, can tranh de runtime quay lai phu thuoc vao schema shared cu.
3. Customers/Identity dang giao thoa nghiep vu.
4. Billing/Orders can duoc chuyen dan sang internal contract thay vi schema rong.

## 5. Quy tac lam viec cho cac phase sau

- Moi phase deu phai:
  - build lai
  - khoi dong lai stack
  - reset data test neu can
  - chay lai smoke test chinh
- Khong merge refactor du lieu lon neu chua co ket qua smoke test sau cung.


