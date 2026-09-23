# Database ownership audit — 2026-09-23

Baseline: `1d48b8b`. Read-only inspection of source, Docker configuration and live SQL Server; no migration, data cleanup, volume reset or `.env` change. This report evaluates the running microservices, not the retired MVC application. `PASS` means no cross-database runtime mapping/object found in the inspected source and SQL metadata; it does **not** mean SQL permissions are least-privilege or that every dynamic path was exercised.

## Executive summary and expected ownership

| Service | Expected/running DB | Synonyms | SQL cross-DB dependencies | Status |
|---|---|---:|---:|---|
| Catalog | `RESTAURANT_CATALOG` | 0 | 0 | PASS |
| Orders | `RESTAURANT_ORDERS` | 26 | 0 | FAILED — cross-DB aliases remain |
| Customers | `RESTAURANT_CUSTOMERS` | 30 | 0 | FAILED — cross-DB aliases remain |
| Identity | `RESTAURANT_IDENTITY` | 0 | 0 | PASS |
| Billing | `RESTAURANT_BILLING` | 0 | 0 | PASS (revalidated) |
| Gateway | none | n/a | n/a | PASS — no runtime DB client/context |

All 56 surviving service synonyms target `[RESTAURANT].[dbo]`. They are real SQL cross-database capabilities and are recreated by fresh `docker/db-init.sh`. The audit found **no production EF mapping, raw SQL or SQL module that uses those particular Orders/Customers aliases**. Thus their presence fails a strict physical-isolation guard, but is not evidence of an active cross-DB read/write. No Type A active runtime dependency is confirmed. Removing aliases is a separate domain-scoped change requiring regression; do not infer that 56 business tables need data migration.

## Source and integration audit

- Each service has one production DbContext wired by its Program.cs to its named DB; Compose and appsettings agree. Catalog/Identity have a `RestaurantDb` fallback key but both values point to their **own** DB. Orders/Customers/Billing use `RestaurantDb` as a configuration key, not the legacy `RESTAURANT` DB. Production Gateway has no DbContext, SQL connection, raw SQL or database connection string. Its uploads volume is file storage, not a DB.
- Catalog context maps local branch/table/dish/menu/category/ingredient entities; its raw SQL and bootstrap run on that context. Catalog also has a local table-release worker. No qualified external DB name or alternate connection found.
- Orders context maps local Orders/OrderItems/OrderStatus/DiningSessions/pre-orders/service requests plus local Catalog snapshots. `OrdersController` and `DiningSessionsController` obtain current Catalog dish/table/branch data through `CatalogApiClient`/`ICatalogReadModel`; order item price is assigned from `dishSnapshot.Price`, not a client price or a Catalog SQL connection. Orders payment consumer polls Billing events through `BillingEventsClient`, updates local Inbox/Orders state, and uses Catalog API for table release. External IDs (dish, table, customer, payment) in Orders rows are logical references, not SQL joins/FKs across DBs.
- Customers context maps Reservations/ReservationTables, favorites, notifications, reviews and support locally. `CustomersController` checks the 120-minute anti-overlap against local Reservations; Gateway first obtains Catalog table layout and asks Customers for occupied IDs. `OrdersQueryClient`/`OrdersEventsClient` use HTTP, not Orders SQL. The ready-event worker writes local Inbox/notification rows. Gateway checks the Billing-issued invoice before calling Customers review creation; Customers does not query Billing SQL.
- Identity context maps local Customers, employees, refresh/reset tokens, loyalty/settings, settlement receipts and Catalog branch snapshots. `CatalogApiClient` uses HTTP and updates a local snapshot/cache for branch validation; no Customers DB connection was found. Google identity validation and profile/loyalty operate on Identity-owned rows.
- Billing's `billing.Bills`, quotes, payments, split parts and invoices are local after commit `1d48b8b`; no synonym or module cross-DB dependency recurred. Gateway calls service HTTP clients for orchestration. Shared DTO/entity-looking classes are not proof of a shared database mapping.
- Source search covered production `.cs`, context registration, bootstrap raw SQL, ADO.NET (`GetDbConnection`/commands), hosted services/consumers and Compose/appsettings. No production three-part `RESTAURANT.*` SQL target or alternate SQL connection was found. Local raw SQL and `sp_getapplock` calls are not cross-DB. HTTP/event calls have service-specific clients; timeout/auth hardening is outside this ownership audit.

## Live SQL object inventory

Counts come from `sys.tables`, `sys.synonyms`, `sys.sql_expression_dependencies`, `sys.sql_modules` and object type catalogs in each ONLINE DB. Cross-DB module counts combine definition search with dependency metadata; no encrypted/hidden (`definition IS NULL`) modules were found. Synonyms require a separate count because dependency metadata does not prove or enumerate callers of aliases.

| Database | Tables | Synonyms | Cross-DB views | Cross-DB procedures | Cross-DB functions | Cross-DB triggers | Other cross-DB dependency rows |
|---|---:|---:|---:|---:|---:|---:|---:|
| `RESTAURANT` | 25 | 0 | 0 | 0 | 0 | 0 | 0 |
| `RESTAURANT_CATALOG` | 16 | 0 | 0 | 0 | 0 | 0 | 0 |
| `RESTAURANT_ORDERS` | 16 | 26 | 0 | 0 | 0 | 0 | 0 |
| `RESTAURANT_CUSTOMERS` | 8 | 30 | 0 | 0 | 0 | 0 | 0 |
| `RESTAURANT_IDENTITY` | 10 | 0 | 0 | 0 | 0 | 0 | 0 |
| `RESTAURANT_BILLING` | 12 | 0 | 0 | 0 | 0 | 0 | 0 |

The shared `RESTAURANT` contains five local views (`ActiveOrders`, `BranchRevenue`, `CustomerLoyalty`, `DishDetails`, `TableNumbers`); Identity contains one local view. No DB contains a stored procedure, function or trigger in this runtime snapshot. Views were inspected by definition/metadata, not merely counted.

### Synonym inventory

Orders' 26 `dbo` aliases: `ActiveOrders`, `Bills`, `Branches`, `BranchRevenue`, `Categories`, `CategoryDish`, `CustomerLoyalty`, `Customers`, `DiningTables`, `DishDetails`, `Dishes`, `DishIngredients`, `EmployeeRoles`, `Employees`, `Ingredients`, `LoyaltyCards`, `MenuCategory`, `Menus`, `PasswordResetTokens`, `PaymentMethod`, `Payments`, `PaymentStatus`, `Reports`, `Restaurants`, `TableNumbers`, `TableStatus`.

Customers' 30 `dbo` aliases: all 26 above plus `OrderItemIngredients`, `OrderItems`, `Orders`, `OrderStatus`. Each alias points to the same-named object in `RESTAURANT.dbo`. Catalog, Identity and Billing have none. No service SQL module dependency on an alias was recorded; production EF models and inspected raw SQL reference local physical entities instead. This is an **unused-alias finding based on available static/metadata evidence**, not a guarantee no unseen ad-hoc query could ever use one under the shared SA credential.

## Legacy `RESTAURANT` inventory and fresh setup

Shared DB tables (25): `Bills`, `Branches`, `Categories`, `CategoryDish`, `Customers`, `DiningTables`, `Dishes`, `DishIngredients`, `EmployeeRoles`, `Employees`, `Ingredients`, `LoyaltyCards`, `MenuCategory`, `Menus`, `OrderItemIngredients`, `OrderItems`, `Orders`, `OrderStatus`, `PasswordResetTokens`, `PaymentMethod`, `Payments`, `PaymentStatus`, `Reports`, `Restaurants`, `TableStatus`. Five views are listed above. Important current row counts: shared Bills 0, Orders 0, OrderItems 1, Customers 9, Branches 3 and DiningTables 47. Local Orders has 53 Orders/53 Sessions; local Identity has 115 Customers; local Billing has 1 Bill/124 MobilePayments/36 Invoices. These differences show why the shared DB must not be treated as an authoritative live store or deleted casually.

`docker/db-init.sh` still creates/retains the shared schema, then drops/recreates alias shells for Orders and Customers from an allowlist. Catalog, Identity and Billing skip alias creation and materialize physical owned tables. The shared `SelfRestaurant.Database` project contains a broad legacy `RestaurantDbContext`/bootstrap, but no running service or Gateway project references it; it is not a production alternate DbContext. Bootstrap and migration/test scripts can intentionally read shared data (Type E) and are not business-request runtime access. This audit did not test a fresh empty container, and did not touch the current SQL volume.

## Findings and priorities

| ID | Type/severity | Evidence and impact | Separate action |
|---|---|---|---|
| O1 | B / HIGH physical-isolation debt | Orders has 26 live aliases to shared DB; `db-init.sh` recreates them. No active business consumer found in inspected code/metadata. Orders is payment/order critical path; accidental or future alias use remains possible. | Orders-only alias retirement/fresh-bootstrap change after usage verification, with B3–B6, cashier and payment regression. Migrate data only if deeper audit finds a real consumer. |
| O2 | B / HIGH physical-isolation debt | Customers has 30 analogous aliases, including four order-related objects; no active business consumer found. | Customers-only alias retirement and B3/B7/review/notification regression after Orders. |
| O3 | E / MEDIUM | Shared DB/schema and privileged SA credential remain in development Compose; script-based initial materialization and legacy artifacts are not service runtime ownership. | Future least-privilege credentials and shared-DB retirement only after data provenance/backup review. |
| O4 | D / not a violation | External IDs and local Catalog snapshots appear in Orders/Identity/Customers, but no direct cross-DB query follows from them. | No migration for IDs alone. |

No Type A active runtime dependency or Type C dual-writer conflict was proven. The strict global ownership result is **FAILED** because Orders/Customers still expose cross-DB aliases and fresh setup recreates them. This remains a release architecture condition; Billing's scoped PASS must not be generalized. Migration order is **Orders first**, then Customers, based on order/payment criticality and bootstrap behavior—not synonym count. Do not migrate both in one task.

## Reproducible guard and safety

Run `powershell -ExecutionPolicy Bypass -File .\scripts\test-database-ownership.ps1` from `SelfRestaurant-master`. It queries metadata and scans production C# for qualified shared-DB references; it does not write SQL. At this audit's original baseline, it exited 1 naming Orders and Customers with counts 26/30; see the later Orders addendum below for the current result. Rerunning it must not change rows or aliases. All `:5100`–`:5105` `/healthz` and `/readyz` endpoints returned 200 before the audit; SQL DBs ONLINE and RabbitMQ ping passed. No Android build/instrumentation was required because production code and API contracts were not changed.

## Orders follow-up (2026-09-23; original inventory above retained)

[Orders ownership cleanup](ORDERS_OWNERSHIP_CLEANUP.md) removed the 26 unused Orders aliases after exact target/consumer verification and changed fresh bootstrap so they are not recreated. Orders cold start, B4/B5, B6/split, ONSITE non-split/split, internal signed-provider and B7 regressions passed. Post-cutover: Orders 0 synonyms and 0 cross-DB SQL dependencies; Customers unchanged at 30. The global guard now exits 1 **only for Customers**. The baseline matrix and inventory in this audit remain the pre-cleanup snapshot, not a current Orders failure. No evidence of active use of the retired aliases emerged from those runtime flows.

## Customers follow-up (2026-09-23; earlier inventories retained)

[Customers ownership cleanup](CUSTOMERS_OWNERSHIP_CLEANUP.md) retired the 30 unused Customers aliases and closed both Docker and LocalDB bootstrap recreation paths. Cold-start, B3/B4, full B7, paid-invoice/ONSITE, Android and signed local-provider regression passed. The global guard now exits **0**: Catalog, Orders, Customers, Identity and Billing each have 0 synonyms and 0 cross-DB SQL objects; Gateway has no DB. Shared `RESTAURANT` remains ONLINE with its legacy data, and least-privilege credentials remain a separate concern. All earlier FAILED matrices above are historical snapshots.
