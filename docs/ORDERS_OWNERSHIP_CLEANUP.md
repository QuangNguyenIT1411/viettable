# Orders database ownership cleanup — 2026-09-23

Baseline `296ca20`. Scope: remove Orders' unused compatibility synonyms only; no data copy, no public API change, no Customers alias change. The live starting point was `RESTAURANT_ORDERS` with 16 physical tables, 26 `dbo` synonyms, 0 cross-DB `sys.sql_expression_dependencies` rows and 0 cross-DB SQL module definitions. Orders/OrderItems/DiningSessions/ServiceRequests/ReservationPreOrders had 53/53/53/3/5 rows; Customers had 30 synonyms. Shared source `RESTAURANT` had 47 DiningTables and 3 Branches. No volume/database reset is authorized.

## Captured alias inventory and consumer decision

Every alias below has schema `dbo`, target database `RESTAURANT`, target schema `dbo` and a target object of the **same name**. Exact base-object syntax is `[RESTAURANT].[dbo].[Name]`. Each is classified `UNUSED` for production Orders business requests; its creation by `docker/db-init.sh` is `BOOTSTRAP_ONLY`. No alias is classified UNKNOWN or RUNTIME_CONSUMER_FOUND based on the inspected EF model, raw SQL, SQL object metadata and service paths. This is static/metadata evidence, not a claim that all possible ad-hoc SQL under the privileged dev login has been observed.

| Alias name | Runtime consumer | Provisioning |
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
| PasswordResetTokens | UNUSED | BOOTSTRAP_ONLY |
| PaymentMethod | UNUSED | BOOTSTRAP_ONLY |
| Payments | UNUSED | BOOTSTRAP_ONLY |
| PaymentStatus | UNUSED | BOOTSTRAP_ONLY |
| Reports | UNUSED | BOOTSTRAP_ONLY |
| Restaurants | UNUSED | BOOTSTRAP_ONLY |
| TableNumbers | UNUSED | BOOTSTRAP_ONLY |
| TableStatus | UNUSED | BOOTSTRAP_ONLY |

`OrdersDbContext` maps the 16 physical Orders-owned tables/snapshots, not these names. Production `OrdersController`, `DiningSessionsController`, hosted payment consumer and `CatalogApiClient` were inspected: Catalog dishes/prices/tables arrive via HTTP and local snapshot, Billing completion via HTTP/event; no three-part SQL or alternate connection exists. The raw-SQL verb/table search found no use of these 26 names. `OrdersDbBootstrapper` and an old EF migration mention `OrderItemIngredients` synonym **only** to replace that separate historical alias with the physical Orders-owned table; it was not among the 26 live aliases. Live Orders DB has no cross-DB view/procedure/function/trigger. A heavyweight SQL trace was not enabled; post-removal cold start and transaction regressions are the runtime observation.

## Cutover and rollback

1. Change `docker/db-init.sh` so `RESTAURANT_ORDERS` skips shell alias generation while Customers still uses its unchanged path. Owned-table materialization continues.
2. Preserve the exact inventory above. Run `sql/retire-orders-synonyms.sql` **only** against `RESTAURANT_ORDERS`. It checks the DB name, each expected alias/name/target and SQL dependencies; it drops aliases inside a transaction and is repeat-safe. No target data/table is dropped.
3. Restart only Orders with the updated image and verify `/healthz`, `/readyz`, ownership guard and business regression. Do not restart SQL, RabbitMQ or reset a volume.

If a hidden consumer fails after retirement, stop Orders traffic, retain data, revert the bootstrap change, and recreate **only the missing names in this inventory** as `CREATE SYNONYM dbo.[Name] FOR [RESTAURANT].[dbo].[Name]` in `RESTAURANT_ORDERS` after reviewing the actual object list. Do not recreate Customers or Billing aliases, and do not drop owned Orders tables. Investigate/migrate the actual consumer before a second retirement. Because this is metadata-only, no Bill/order ID or row reconciliation is needed for rollback.

## Acceptance evidence (completed 2026-09-23)

- `sql/retire-orders-synonyms.sql` returned 0 remaining aliases; its second execution also returned 0. The Orders-only guard confirms 0 synonyms, 0 cross-DB dependency/module references and correct fresh-bootstrap source. The global guard still exits 1 **only for Customers' 30 aliases**.
- Rebuilt and force-recreated **Orders only** without SQL/RabbitMQ restart. Orders `:5102` and Gateway `:5100` `/healthz` and `/readyz` returned HTTP 200. The provider regression wrapper later ran `db-init` twice; Orders still had 0 aliases afterward, which validates the updated bootstrap against the current environment without resetting it.
- B4 passed QR validation, mismatch, reservation check-in/pre-order conversion, owned active/detail session, walk-in retry and table conflict. B5 passed multi-round orders, stable retry, tracking, service request create/list/cancel and authenticated hub negotiation. Earlier B4 attempts hit retained-fixture 409 conflicts; Windows PowerShell 5 delivered an empty result from its old nested `docker exec -Q` query despite exit 0. The fixture now pipes read-only SQL into sqlcmd and searches branches plus reservation occupancy; business assertions were not relaxed.
- ONSITE non-split passed concurrent/duplicate cashier confirmation: Payment 2059 SUCCEEDED, Invoice 2024, Session 3030 CLOSED. Three-part ONSITE split passed intermediate `PAYMENT_REQUESTED` and final CLOSED: Payments 2060–2062, Invoice 2025, Session 3031. Internal signed-provider wrapper passed B6, all-online split, mixed ONLINE retry + ONSITE, and B7 completed-transaction review CRUD. This was local signing/state acceptance, not an external VNPAY transaction. Provider config was restored afterward.
- Android `:app:assembleDebug :app:testDebugUnitTest` passed; no Android production/API contract changes. Orders-owned Orders/OrderItems/Sessions rows were 53/53/53 before and 62/62/62 after retained regression fixtures, not deleted. ServiceRequests rose 3→4. Shared `RESTAURANT` Branches/DiningTables stayed 3/47; Customers' 30 aliases stayed untouched. No data migration, row delete, `.env` edit or volume reset occurred.

**Orders ownership: PASS within the inspected service/runtime scope.** Least-privilege SQL principals and the Customers alias cleanup remain separate work; overall backend ownership still fails because Customers retains 30 aliases.
