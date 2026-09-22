# Migration Gap Matrix (Legacy MVC vs Microservices)

## Current Snapshot

- `Gateway.Api` is the active web entry point.
- Core flows currently verified:
  - `customer`: pass
  - `chef`: pass
  - `cashier`: pass
  - `admin`: pass
- Main architectural cleanup already done:
  - customer order history moved from `Customers.Api` ownership to `Orders.Api`
  - bill read path reduced coupling to `Orders.Api` via billing snapshots
  - admin customer management moved to `Identity.Api`
  - duplicate admin customer endpoints removed from `Customers.Api`
  - checkout no longer uses direct sync `Billing -> Orders complete` command; `Orders.Api` now reconciles via `payment.completed.v1`
  - broad EF mappings already narrowed in `Billing.Api`, `Orders.Api`, and `Catalog.Api`
  - `Identity.Api` no longer depends on `Branches` in its database; it now uses local `CatalogBranchSnapshots`
  - `Orders.Api` now uses explicit catalog snapshot refresh rules for branch/table/dish reads
  - `Billing.Api` now persists `OrderContextSnapshots` to reduce repeat fallback reads to `Orders.Api`
  - regression cleanup now resets service test data across `Catalog`, `Identity`, `Orders`, and `Billing`

## Legend

- `Done`: running and aligned with current microservice path
- `Partial`: running but still has architectural debt or not fully parity-clean
- `Missing`: not migrated yet

## Customer/Public

| Legacy MVC Module | New Target | Status | Notes |
|---|---|---|---|
| Home (branches/tables) | `Gateway HomeController` + `Catalog API` | Done | Running |
| QR -> Menu | `Gateway MenuController` + `Catalog API` | Done | Running |
| Add/update/remove order items | `Gateway OrderController` + `Orders API` | Done | Running |
| Send order to kitchen | `Gateway Menu/Order` + `Orders API` | Done | Batch submit path in `Orders.Api` |
| Customer login/register | `Gateway CustomerController` + `Identity API` | Done | Identity owns auth |
| Customer profile | `Gateway CustomerController` + `Identity API` | Done | Identity owns customer account/profile data |
| Customer order history | `Gateway CustomerController` + `Customers API -> Orders API` | Done | Read ownership moved to Orders |
| Forgot/reset password | `Gateway CustomerController` + `Identity API` | Done | Running |
| Ready notifications | `Gateway MenuController` + `Customers API` | Done | Running |

## Staff

| Legacy MVC Module | New Target | Status | Notes |
|---|---|---|---|
| Staff login | `Gateway Staff Account` + `Identity API` | Done | Running |
| Staff forgot/reset password | `Gateway Staff Account` + `Identity API` | Done | Running |
| Chef queue + transitions | `Gateway Staff/Chef` + `Orders API` | Done | Running |
| Chef dish ingredient/toggle actions | `Gateway Staff/Chef` + `Catalog API` | Done | Running |
| Cashier queue | `Gateway Staff/Cashier` + `Billing API` | Done | Billing now filters already-billed orders locally |
| Cashier checkout | `Gateway Staff/Cashier` + `Billing API` | Done | Orders completion now happens via event reconciliation |
| Cashier history/report | `Gateway Staff/Cashier` + `Billing API` | Done | Running |
| Staff profile update | `Gateway Staff` + `Identity API` | Done | Running |

## Admin

| Legacy MVC Module | New Target | Status | Notes |
|---|---|---|---|
| Dashboard | `Gateway Admin` + `Identity/Orders APIs` | Done | Running |
| Categories | `Gateway Admin` + `Catalog API` | Done | CRUD running |
| Dishes | `Gateway Admin` + `Catalog API` | Done | CRUD running |
| Ingredients | `Gateway Admin` + `Catalog API` | Done | CRUD running |
| Employees | `Gateway Admin` + `Identity API` | Done | CRUD + history running |
| Customers | `Gateway Admin` + `Identity API` | Done | Runtime path moved to Identity |
| Revenue report | `Gateway Admin` + `Orders/Billing APIs` | Done | Running |
| Top dishes report | `Gateway Admin` + `Orders API` | Done | Running |
| Settings/profile/password | `Gateway Admin` + `Identity/Catalog APIs` | Done | Running |
| Tables/QR | `Gateway Admin` + `Catalog API` | Done | Running |

## Ownership Matrix

### Service Ownership Rules

| Service | Owns write model for | Should expose to others via |
|---|---|---|
| `Catalog.Api` | branch/table/menu/catalog data | internal read APIs |
| `Orders.Api` | order lifecycle and kitchen state | internal read APIs + integration events |
| `Billing.Api` | bill/payment records and cashier reporting | billing APIs + payment events |
| `Identity.Api` | authentication, passwords, employees, customer account/profile/loyalty, admin customer management | auth/admin/customer APIs |
| `Customers.Api` | ready notifications and customer-facing read models not owned elsewhere | customer APIs + internal read APIs |
| `Gateway.Api` | no domain data ownership | orchestration/UI only |

### Table-Level Ownership

| Table / View | Owner Service | Other Services That Still Read It | Target State |
|---|---|---|---|
| `Branches` | `Catalog.Api` | `Orders.Api`, `Gateway.Api` indirectly | read only via Catalog |
| `DiningTables` | `Catalog.Api` | `Orders.Api`, `Billing.Api` indirectly | read/write table state via Catalog or events |
| `Menus` | `Catalog.Api` | `Gateway.Api` indirectly | read only via Catalog |
| `MenuCategory` | `Catalog.Api` | `Gateway.Api` indirectly | read only via Catalog |
| `CategoryDish` | `Catalog.Api` | `Gateway.Api` indirectly | read only via Catalog |
| `Categories` | `Catalog.Api` | `Gateway.Api` indirectly | read only via Catalog |
| `Dishes` | `Catalog.Api` | `Orders.Api` indirectly | read only via Catalog internal APIs |
| `DishIngredients` | `Catalog.Api` | `Gateway.Api` indirectly | read only via Catalog |
| `Ingredients` | `Catalog.Api` | `Gateway.Api` indirectly | read only via Catalog |
| `Orders` | `Orders.Api` | `Billing.Api` via internal context API | write only in Orders |
| `OrderItems` | `Orders.Api` | `Billing.Api` via checkout context / cashier aggregates | write only in Orders |
| `OrderStatus` | `Orders.Api` | `Billing.Api` indirectly | write only in Orders |
| `Bills` | `Billing.Api` | `Orders.Api` via payment-completed consumer does not need direct table read | write only in Billing |
| `Payments` | `Billing.Api` | `Billing.Api` | write only in Billing |
| `Customers` | `Identity.Api` | `Gateway.Api`, `Billing.Api`, `Orders.Api` indirectly through Identity APIs | ownership moved to Identity |
| `PasswordResetTokens` | `Identity.Api` | none | keep in Identity |
| `Employees` | `Identity.Api` | none outside Identity runtime path | keep in Identity |
| `EmployeeRoles` | `Identity.Api` | none outside Identity runtime path | keep in Identity |
| `CatalogBranchSnapshots` in `Identity` DB | `Identity.Api` | none | local read model sourced from `Catalog.Api` |
| `CatalogBranchSnapshots` in `Orders` DB | `Orders.Api` | none | local read model sourced from `Catalog.Api` |
| `CatalogTableSnapshots` in `Orders` DB | `Orders.Api` | none | local read model sourced from `Catalog.Api` |
| `CatalogDishSnapshots` in `Orders` DB | `Orders.Api` | none | local read model sourced from `Catalog.Api` |
| `OrderContextSnapshots` in `Billing` DB | `Billing.Api` | none | local read model sourced from `Orders.Api` |
| `ReadyDishNotifications` | `Customers.Api` | `Gateway.Api` indirectly | keep in Customers |
| `CustomerLoyalty` view | `Identity.Api` | `Orders.Api` indirectly through Identity internal API | keep in Identity read model |
| `OutboxEvents` in Billing DB | `Billing.Api` | none directly | keep in Billing |
| `InboxEvents` in Orders DB | `Orders.Api` | none | keep in Orders |
| `OutboxEvents` in Orders DB | `Orders.Api` | none | keep in Orders |
| `InboxEvents` in Customers DB | `Customers.Api` | none | keep in Customers |

## Biggest Remaining DB-Split Risks

| Risk | Why it matters before database-per-service |
|---|---|
| some clients/classes still keep legacy naming like `CustomersApiClient` while now pointing at `Identity.Api` | naming debt can cause confusion during DB split work |
| `Orders.Api` still has legacy entity files for old shared-schema objects even though `OrdersDbContext` is already narrowed | codebase noise can mislead future split work unless cleaned or moved aside |
| `Billing.Api` still depends on `Orders` for live cashier queue and checkout context | safer than before, but still a synchronous dependency for realtime flows |
| `Orders.Api` is the current transactional center for customer -> chef -> cashier handoff | any DB cutover here needs stronger regression coverage than `Catalog`/`Identity` did |
| some staff regression scripts depend on branch-specific seed/menu availability | test failures can come from seed drift instead of architectural regressions |

## Cutover Decision

### Recommended Next Deep DB Cutover: `Orders.Api`

`Orders.Api` should be the next service to cut over more deeply before `Billing.Api`.

Why:

1. `Orders.Api` is already closer to a clean owned store than `Billing.Api`.
2. Its external reads from catalog are now funneled through explicit local snapshot tables with refresh rules.
3. `Billing.Api` still cannot be split as deeply without `Orders` first becoming the stable source of truth for live checkout and order state.
4. Once `Orders` is cleaner, `Billing` can be reduced to a thinner consumer of order context plus bill-owned reporting data.

### Orders Cutover Goal

- keep `Orders`, `OrderItems`, `OrderStatus`, `InboxEvents`, `OutboxEvents` as hard-owned tables
- treat catalog data only as read-model snapshot tables
- remove residual legacy shared-schema entity noise from the `Orders.Api` project
- make `Orders.Api` the unambiguous source of truth for active order state and kitchen transitions

### Billing After Orders

After `Orders.Api` is stabilized as the next deep cutover:

- keep `Bills`, `OutboxEvents`, `OrderContextSnapshots` in `Billing.Api`
- continue shrinking live sync reads to `Orders.Api`
- keep billing history/report paths fully local wherever possible

## Recommended Next Implementation Order

1. Freeze `Catalog.Api` as the first database-per-service candidate and migrate its tables out first.
2. Freeze `Identity.Api` as the second completed split-prep candidate and keep `Branches` out permanently.
3. Cut `Orders.Api` deeper next and clean the remaining transactional ownership boundary.
4. Split `Billing.Api` after `Orders.Api` stabilizes as the source of truth for live order state.
5. Leave `Customers.Api` read-model storage last.
