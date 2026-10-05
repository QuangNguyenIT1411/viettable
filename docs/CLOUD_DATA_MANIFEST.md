# Cloud data manifest — PostgreSQL phase 1

Ngày 05/10/2026. Manifest này không thực hiện export/import/delete. PostgreSQL verification dùng database disposable riêng, không dùng Supabase thật hay dữ liệu SQL Server demo.

## Policy

KEEP / MIGRATE nghĩa là bảo toàn dữ liệu nghiệp vụ đã được xác nhận, ownership, ID, money, timestamps và relations. Không đồng nghĩa migrate toàn bộ QA rows. SEED FRESH chỉ áp dụng reference/generated projection/schema metadata; không tự dựng lịch sử. DO NOT MIGRATE áp dụng secret/session artifacts và các **test rows được xác minh cụ thể**; không xóa một nhóm transactional table vì cho là test.

Một physical PostgreSQL database có năm schema. Mỗi runtime principal cần grant chỉ schema owned; hiện test dùng superuser disposable **không chứng minh DB-level least privilege**. External IDs không tạo cross-schema FK. Import phải set identity sequence sau explicit IDs, theo thứ tự FK local và freeze/high-watermark nhất quán.

## Catalog — 15 domain tables

| Table | Policy | Ghi chú |
|---|---|---|
| Restaurants | KEEP / MIGRATE | Root local FK của Branches; SQL context cũ bỏ mapping nhưng PG baseline khai báo owner Catalog |
| Branches | KEEP / MIGRATE | Không thay branch ID/image path |
| DiningTables | KEEP / MIGRATE | Table/QR/seats; occupancy phải đối chiếu active Orders trước mở traffic |
| TableStatus | SEED FRESH | AVAILABLE/OCCUPIED/RESERVED/INACTIVE; reconcile IDs khi import FK |
| Categories | KEEP / MIGRATE | Bao gồm active flags |
| Dishes | KEEP / MIGRATE | 52 ảnh hiện tại không phải demo PG seed mới |
| Menus | KEEP / MIGRATE | Branch và valid dates |
| MenuCategory | KEEP / MIGRATE | Local FK |
| CategoryDish | KEEP / MIGRATE | Menu association / availability |
| Ingredients | KEEP / MIGRATE | Stock numeric; không reset về demo stock |
| DishIngredients | KEEP / MIGRATE | Recipe quantities |
| IngredientBatches | KEEP / MIGRATE | PG application-generated concurrency token; không copy SQL rowversion semantics |
| IngredientStockMovements | KEEP / MIGRATE | Audit stock/consumption; không trừ kho lại khi replay |
| Units | KEEP / MIGRATE | Existing reference values/IDs, không có PG fixed seed mới |
| BusinessAuditLogs | KEEP / MIGRATE | Retention/redaction policy trước export |

## Orders — 16 domain tables

| Table | Policy | Ghi chú |
|---|---|---|
| OrderStatus | SEED FRESH | Exact status codes hiện tại; reconcile FK IDs |
| DiningSessions | KEEP / MIGRATE | Active/closed history theo cutover policy |
| DiningSessionTables | KEEP / MIGRATE | Session grouping/reference |
| Orders | KEEP / MIGRATE | Giữ multi-round, external IDs, idempotency |
| OrderItems | KEEP / MIGRATE | UnitPrice/LineTotal/status snapshot |
| OrderItemIngredients | KEEP / MIGRATE | Stock consumption associations |
| ReservationPreOrders | KEEP / MIGRATE | Conversion flag/order ID prevents double conversion |
| ReservationPreOrderItems | KEEP / MIGRATE | Immutable submitted snapshots |
| ServiceRequests | KEEP / MIGRATE | OPEN/ACKNOWLEDGED/COMPLETED/CANCELLED; no orphan active requests |
| SubmitCommands | KEEP / MIGRATE | Persisted idempotency/replay receipt |
| BusinessAuditLogs | KEEP / MIGRATE | Retention/redaction |
| OutboxEvents | KEEP / MIGRATE | Drain/freeze or migrate pending events + receipts together |
| InboxEvents | KEEP / MIGRATE | Preserve deduplication keys/retry/dead state |
| CatalogBranchSnapshots | SEED FRESH | Rehydrate through Catalog API; optional approved migrate for offline continuity |
| CatalogDishSnapshots | SEED FRESH | Not another owner's table; Orders-local projection |
| CatalogTableSnapshots | SEED FRESH | Revalidate occupancy/branch; not canonical |

## Customers — 8 domain tables

| Table | Policy | Ghi chú |
|---|---|---|
| Reservations | KEEP / MIGRATE | Customer/branch/table references and UTC slot |
| ReservationTables | KEEP / MIGRATE | Local relation |
| ReservationPreOrderItems | KEEP / MIGRATE | Customers-owned draft/submitted state |
| CustomerFavorites | KEEP / MIGRATE | Catalog IDs checked through API |
| CustomerReviews | KEEP / MIGRATE | Completed invoice eligibility references |
| SupportRequests | KEEP / MIGRATE | PII access/retention review |
| ReadyDishNotifications | KEEP / MIGRATE | Read/unread and customer ownership |
| InboxEvents | KEEP / MIGRATE | Retain event deduplication receipts |

## Identity — 10 domain tables and one view

| Table/entity | Policy | Ghi chú |
|---|---|---|
| Customers | KEEP / MIGRATE | Password hash/provider subject/profile; no plaintext passwords |
| Employees | KEEP / MIGRATE | Authorized branch/role assignments; no default credentials |
| EmployeeRoles | SEED FRESH | ADMIN/MANAGER/CASHIER/WAITER/CHEF/KITCHEN_STAFF; reconcile FK IDs |
| LoyaltyCards | KEEP / MIGRATE | Balances/status/date; owner Identity |
| LoyaltySettlements | KEEP / MIGRATE | Idempotent point settlement receipts |
| CustomerSettings | KEEP / MIGRATE | Catalog branch ID external reference |
| BusinessAuditLogs | KEEP / MIGRATE | Retention/redaction |
| CatalogBranchSnapshots | SEED FRESH | Refresh via Catalog API |
| MobileRefreshTokens | DO NOT MIGRATE by default | Proposed cutover relogin/revoke policy requires approval; never dump raw tokens |
| PasswordResetTokens | DO NOT MIGRATE by default | Expire/reissue through legitimate flow; policy needs approval |
| CustomerLoyalty view | SEED FRESH | Recreated migration, reads **identity only**, not exported rows |

Before citext import: find case-insensitive duplicate Username/Email and resolve explicitly. No auto deletion/merge of accounts.

## Billing — 11 domain tables

| Table | Policy | Ghi chú |
|---|---|---|
| Bills | KEEP / MIGRATE | Legacy cashier/billing records |
| CheckoutQuotes | KEEP / MIGRATE | Immutable prices/discounts/snapshot/expiration |
| CheckoutCommands | KEEP / MIGRATE | Replay/command receipts |
| MobilePayments | KEEP / MIGRATE | Provider reference/idempotency/PENDING/SUCCEEDED; reconcile pending before cutover |
| Invoices | KEEP / MIGRATE | One final invoice/session; settlement work item retained |
| SplitBills | KEEP / MIGRATE | Total/status |
| SplitBillParts | KEEP / MIGRATE | Exact sums, paid part/payment link |
| Vouchers | KEEP / MIGRATE | Approved voucher configuration; PG phase does not fabricate grants |
| OrderContextSnapshots | SEED FRESH | Billing-local projection; migrate approved immutable evidence if legacy bill requires it |
| OutboxEvents | KEEP / MIGRATE | Drain or coordinate delivery + receipts; do not replay all paid events |
| BusinessAuditLogs | KEEP / MIGRATE | Financial retention policy |

## Metadata / artifacts

Five `schema.__EFMigrationsHistory`: SEED FRESH through PG migrations; **never copy SQL Server migration history**. Quoted existing PascalCase table/column names remain. Binary/media objects migrate separately with checksum/path manifest, not DB dump. ASP.NET Session RAM, diagnostic JSONL, local screenshots/logs, `.env`, signing keys: DO NOT MIGRATE as business records; provision secrets/storage through infrastructure.

QA usernames/temporary categories/orders/payments are excluded only by a reviewed marker/ID allowlist with owner approval. Financial/history rows are not globally discarded. This phase imported **zero** SQL Server rows and performed no cleanup of user data.

## Cutover decision still required

### Staging reference bootstrap đã nghiệm thu 05/10/2026

Allowlist owner export từ local Catalog: Restaurants, TableStatus, Branches, Categories, Units, Ingredients, Dishes, DiningTables, Menus, MenuCategory, CategoryDish, DishIngredients. Giữ IDs/QR/recipes/reference configuration; fresh staging không có local sessions nên không import occupancy/CurrentOrderID. Orders statuses và Identity roles seed fresh, không account/password seed. Không import audit/stock-movement/batch records hoặc transactional/account tables trong reference bootstrap này.

Canonical checks:3branches/47tables/10categories/52dishes; first insert649 reference rows, second insert0. Explicit source instant policy: current Catalog + SQL Server Docker clocks UTC và existing mobile offsetless-UTC contract; bootstrap đổi bằng UTC timezone, không device timezone. Initial Vietnam-zone conversion bug đã được CAS-correct reference-only819fields, second correction0; không overwrite legitimate API edits. Không dùng policy này để tự gắn UTC cho transactional lịch sử chưa xác minh nguồn. Xem [SUPABASE_ACCEPTANCE.md](SUPABASE_ACCEPTANCE.md).

Freeze mutations, settle/drain pending payments and outboxes, or migrate a consistent graph of active reservations/sessions/orders/payments with deduplication receipts. Specify timezone of legacy SQL timestamps before conversion; do not label arbitrary Unspecified time as UTC. Backup/restore rehearsal and row-count/FK/amount/event reconciliation are mandatory next-phase acceptance.
