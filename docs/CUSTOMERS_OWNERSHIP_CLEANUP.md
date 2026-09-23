# Customers database ownership cleanup — 2026-09-23

Baseline `fa26f60`. Scope: retire unused Customers compatibility aliases; preserve the shared `RESTAURANT` database and all service data. Before cutover, `RESTAURANT_CUSTOMERS` had 8 physical tables, 30 `dbo` synonyms, 0 cross-DB SQL dependency rows, and no view/procedure/function/trigger. Reservations/ReservationTables/CustomerFavorites/CustomerReviews/SupportRequests/ReadyDishNotifications held 7/6/0/1/7/7 rows. No volume or database was reset.

## Exact inventory and consumer classification

Every row below has schema `dbo`, target database `RESTAURANT`, target schema `dbo`, and base object `[RESTAURANT].[dbo].[Name]` where `Name` equals the alias. Every row is `UNUSED` for inspected Customers runtime business paths and `BOOTSTRAP_ONLY` for provisioning. No row was classified `UNKNOWN` or `RUNTIME_CONSUMER_FOUND`. The classification is supported by production source, EF mappings, raw SQL, SQL metadata and post-retirement runtime regressions; it is not a claim to have observed every possible ad-hoc query under the privileged development login.

| Alias | Runtime | Provisioning |
|---|---|---|
| ActiveOrders | UNUSED | BOOTSTRAP_ONLY |
| Bills | UNUSED | BOOTSTRAP_ONLY |
| Branches | UNUSED | BOOTSTRAP_ONLY |
| BranchRevenue | UNUSED | BOOTSTRAP_ONLY |
| Categories | UNUSED | BOOTSTRAP_ONLY |
| CategoryDish | UNUSED | BOOTSTRAP_ONLY |
| CustomerLoyalty | UNUSED | BOOTSTRAP_ONLY |
| Customers | UNUSED | BOOTSTRAP_ONLY |
| DiningTables | UNUSED | BOOTSTRAP_ONLY |
| DishDetails | UNUSED | BOOTSTRAP_ONLY |
| Dishes | UNUSED | BOOTSTRAP_ONLY |
| DishIngredients | UNUSED | BOOTSTRAP_ONLY |
| EmployeeRoles | UNUSED | BOOTSTRAP_ONLY |
| Employees | UNUSED | BOOTSTRAP_ONLY |
| Ingredients | UNUSED | BOOTSTRAP_ONLY |
| LoyaltyCards | UNUSED | BOOTSTRAP_ONLY |
| MenuCategory | UNUSED | BOOTSTRAP_ONLY |
| Menus | UNUSED | BOOTSTRAP_ONLY |
| OrderItemIngredients | UNUSED | BOOTSTRAP_ONLY |
| OrderItems | UNUSED | BOOTSTRAP_ONLY |
| Orders | UNUSED | BOOTSTRAP_ONLY |
| OrderStatus | UNUSED | BOOTSTRAP_ONLY |
| PasswordResetTokens | UNUSED | BOOTSTRAP_ONLY |
| PaymentMethod | UNUSED | BOOTSTRAP_ONLY |
| Payments | UNUSED | BOOTSTRAP_ONLY |
| PaymentStatus | UNUSED | BOOTSTRAP_ONLY |
| Reports | UNUSED | BOOTSTRAP_ONLY |
| Restaurants | UNUSED | BOOTSTRAP_ONLY |
| TableNumbers | UNUSED | BOOTSTRAP_ONLY |
| TableStatus | UNUSED | BOOTSTRAP_ONLY |

`CustomersDbContext` registers/maps only the eight physical Customers-owned sets: InboxEvents, ReadyDishNotifications, Reservations, ReservationPreOrderItems, ReservationTables, CustomerFavorites, CustomerReviews and SupportRequests. Legacy entity *class files* bearing alias names exist but are not registered/mapped by this context or used by production controllers. Its bootstrap raw SQL creates/updates only owned tables, indexes and reservation relations. Its only ADO.NET command is `SELECT 1` for readiness. The production source search found no three-part SQL or alternate cross-DB connection. SQL metadata found no module definitions, synonym references or cross-DB dependencies. A heavyweight SQL trace was not enabled; cold restart and transaction regressions after removal are runtime evidence.

Reservations check overlap against local Reservations/ReservationTables; Gateway asks Catalog for branch/table layout and Customers for reserved IDs. Favorites store customer/dish external IDs and a dish-name snapshot; Gateway validates the dish through Catalog HTTP. Ready notifications come from Orders outbox via `OrdersEventsClient` HTTP and persist locally. Gateway validates a paid Billing invoice over HTTP before Customers creates a review; Customers does not read Billing SQL. FAQ is currently an in-controller static response, **not** a Customers-owned FAQ table. Support requests are local rows. The legacy `Customers` alias is not used for auth/profile; the user ID is an external Identity reference.

## Cutover, rollback, and evidence

- `docker/db-init.sh` now skips alias-shell creation for Customers while preserving owned-table startup; `sql/setup-service-db-shells.ps1` also assigns an empty alias allowlist to every current owned service DB, closing a second LocalDB bootstrap path. No API contract or service business code changed.
- `sql/retire-customers-synonyms.sql` checks the database name, explicit 30-name inventory, exact targets and SQL dependencies, then drops only Customers synonyms in a transaction. A second execution returned 0 again. Target objects in `RESTAURANT` were not touched.
- Customers Docker image published successfully and only Customers was force-recreated for the cold-start check. Customers and Gateway health/ready returned 200. Later provider regression ran Docker `db-init` twice; Customers remained at 0 aliases. The Customers guard and global guard both passed; every service DB now has 0 synonyms and 0 SQL cross-DB objects in the inspected metadata.
- B3 reservation/availability/change/cancel/ownership/pre-order and B4 reservation check-in/walk-in passed with retained fixtures. ONSITE cashier confirmation produced Payment 2074 SUCCEEDED, Invoice 2030 and Session 3038 CLOSED. Local signed-IPN wrapper passed B6, split and full B7: favorites, FAQ, support, notification event projection/read/ownership, paid-invoice review create/retry/list/update/delete/ownership. This is internal signing acceptance, not an external VNPAY merchant transaction.
- Android debug build/unit tests passed. Samsung `R5CRC14LFYD` instrumentation tier All returned `OK (11 tests)`; 10 executed successfully and one existing-session security test was skipped by its explicit opt-in guard. `adb reverse` was restored by the script.
- After retained fixtures, Reservations/ReservationTables/CustomerFavorites/CustomerReviews/SupportRequests/ReadyDishNotifications held 9/9/0/1/8/8 rows. Legacy `RESTAURANT` still held Customers 9, Branches 3, DiningTables 47, Orders 0 and OrderItems 1, unchanged from before. All six SQL databases remained ONLINE; all `:5100`–`:5105` health/ready checks were 200 and RabbitMQ ping passed. No pre-existing data, source table or volume was deleted/migrated and `.env` was not changed; the B7 CRUD test deleted only its newly created review through the public API.

If an undiscovered consumer fails, retain all data, stop affected Customers traffic, restore the bootstrap allowlist only for a reviewed rollback, and recreate **only the missing aliases from the exact inventory above** as `CREATE SYNONYM dbo.[Name] FOR [RESTAURANT].[dbo].[Name]` in `RESTAURANT_CUSTOMERS`. Investigate/refactor that actual consumer before retiring the alias again. Do not recreate aliases elsewhere or drop legacy source tables.

**Scoped internal database isolation: PASS.** This does not prove least-privilege SQL permissions, exhaustively observe arbitrary dynamic SQL, retire the legacy database, or satisfy production HTTPS/signing/external-provider readiness.
