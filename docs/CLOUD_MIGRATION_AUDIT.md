# VietTable Cloud Migration Audit

Ngày audit: 05/10/2026. Baseline: `40a0f59 fix: complete customer history and payment UX`.

Phạm vi **lượt audit ban đầu**: source/config và readiness local, không triển khai cloud. Lượt đó không sửa source/package, tạo migration, sửa database, export/import, stage, commit hoặc push. Kết quả implementation phase 1 sau audit được ghi riêng cuối tài liệu; hai thay đổi `.idea/misc.xml`, `.idea/vcs.xml` được giữ nguyên. Không đọc/in giá trị secret từ `.env`.

Kết luận: **chưa sẵn sàng cloud cutover nguyên trạng**. Có thể chuyển lên cloud, nhưng SQL Server → PostgreSQL là migration thực sự, không phải đổi connection string. Vercel có hỗ trợ .NET Docker và WebSocket hiện hành; vấn đề lớn của source là worker nền, session web trong RAM, filesystem và database/provider. Khuyến nghị Option A trước, Option B là phase riêng.

## Current architecture

### Project inventory và runtime

Các đường dẫn dưới đây tương đối với `D:\Quang\VietTable`. Backend root là `SelfRestaurant-master`.

| Project | Runtime / entrypoint | Local → container | Database | Calls / background / realtime |
|---|---|---|---|---|
| Gateway | ASP.NET Core `net8.0`, `src/Gateway/SelfRestaurant.Gateway.Api/Program.cs` | `5100 → 8080` | Không có | HTTP tới cả 5 service; hai SignalR hub; cookie session web và JWT mobile |
| Catalog | ASP.NET Core `net8.0`, `src/Services/SelfRestaurant.Catalog.Api/Program.cs` | `5101 → 8080` | `RESTAURANT_CATALOG` | Catalog/inventory/table APIs; `TableAutoReleaseService` |
| Orders | ASP.NET Core `net8.0`, `src/Services/SelfRestaurant.Orders.Api/Program.cs` | `5102 → 8080` | `RESTAURANT_ORDERS` | Catalog, Identity, Billing HTTP; Rabbit publisher; HTTP outbox consumer |
| Customers | ASP.NET Core `net8.0`, `src/Services/SelfRestaurant.Customers.Api/Program.cs` | `5103 → 8080` | `RESTAURANT_CUSTOMERS` | Orders HTTP; ready-notification projection worker; không có Rabbit client package |
| Identity | ASP.NET Core `net8.0`, `src/Services/SelfRestaurant.Identity.Api/Program.cs` | `5104 → 8080` | `RESTAURANT_IDENTITY` | Catalog/Orders/Billing HTTP; JWT, Google validation, SMTP |
| Billing | ASP.NET Core `net8.0`, `src/Services/SelfRestaurant.Billing.Api/Program.cs` | `5105 → 8080` | `RESTAURANT_BILLING` | Orders/Identity HTTP; Rabbit publisher; payment reconciliation worker |
| Customer Web | React 19.2, TS 5.9, Vite 7.1; `src/Frontend/selfrestaurant-customer-web` | dev `5173`, preview `4173` | Qua Gateway | Base `/`; cookie session; relative API; Google web client config |
| Chef Web | React 19.2, TS 5.9, Vite 7.1; `src/Frontend/selfrestaurant-chef-web` | dev `5174`, preview `4174` | Qua Gateway | Base `/app/chef/`; cookie staff session; React Query |
| Cashier Web | React 19.1, TS 5.9, Vite 7.1; `src/Frontend/selfrestaurant-cashier-web` | dev `5175`, preview `4175` | Qua Gateway | Base `/app/cashier/`; cookie staff session; SignalR branch notifications |
| Admin Web | React 19.2, TS 5.9, Vite 7.1; `src/Frontend/selfrestaurant-admin-web` | dev `5176`, preview `4176` | Qua Gateway | Base `/app/admin/`; cookie staff session; multipart uploads |

Frontend versions là ranges trong `package.json`, không phải kết quả xác minh phiên bản package đã cài. Cả bốn có `npm run build` = `tsc -b && vite build`, output `dist`. Local URLs/ports lấy từ `vite.config.ts`, không suy đoán. Gateway hiện phục vụ customer ở `/`, ba staff web ở `/app/{chef,cashier,admin}/`; React apps không phải bốn ASP.NET MVC service riêng.

Các csproj nằm cùng thư mục mỗi API, tên tương ứng `{Project}.csproj`. Dependency thực tế:

- Gateway: `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.22; SignalR/session từ ASP.NET shared framework; không EF/database package.
- Năm domain API: EF Core SqlServer và Design 8.0.11, OpenAPI 8.0.22, Swashbuckle 6.6.2.
- Orders/Billing: thêm RabbitMQ.Client 6.8.1; Billing thêm JwtBearer 8.0.22.
- Identity: Google.Apis.Auth 1.69.0 và JwtBearer 8.0.22.
- `src/Shared/SelfRestaurant.Database` còn EF SqlServer nhưng không có ProjectReference từ sáu API hiện tại. Không lấy legacy context/entity tồn tại trên disk làm bằng chứng runtime ownership.
- Backend contract tests và `tools/inspectgw` không phải production HTTP workloads.
- Android: Java 11, min 24/target 37; `ApiClient` → Gateway `/api/mobile/v1`; SignalR Java client; Credential Manager Google ID; AndroidKeyStore token storage.

### Container / authentication / boundary

Sáu `Dockerfile` hiện có dùng multi-stage SDK/ASP.NET 8.0, runtime `dotnet {Project}.dll`, `ASPNETCORE_URLS=http://+:8080`. Build context là backend root, không phải thư mục csproj. Billing copy thêm `sql/billing-owned-tables.sql` để embed resource. Gateway Dockerfile còn build/copy cả bốn SPA vào `wwwroot/*-app`; tách webs khỏi Gateway là một deployment change, không cần viết lại UI.

`docker-compose.yml` chạy SQL Server 2022, RabbitMQ 3-management, db-init và sáu API. `docker-compose.prod.yml` đóng downstream ports, thêm Caddy TLS, Rabbit persistence/restart và DataProtection volume. Caddy chỉ public Gateway và hai VNPAY callback tới Billing; chặn `/api/internal/*`, `/internal/*`, swagger, readyz. Caddy không phải workload có thể bê nguyên Compose sang Vercel.

Auth không đồng nhất ở mọi tầng:

- Mobile: Identity cấp JWT; Gateway xác thực issuer/audience/lifetime/role; hub mobile chỉ role `customer`.
- Web: Gateway session cookie; staff role/branch lấy từ session. `AddDistributedMemoryCache()` **vẫn là RAM process**, không phải distributed Redis.
- Identity có JWT và rate limiter trong process; Billing có signed `CashierService` JWT để authorize cashier/branch.
- Nhiều Catalog/Orders/Customers internal APIs tin Gateway và IDs/commands truyền vào, không có một JWT policy chung trên toàn bộ downstream. Ví dụ `StaffServiceRequestsController` kiểm tra branch của command nhưng không tự xác thực người gửi. **Không public downstream URLs trong migration**; private service binding/network và xác thực service-to-service phải được giữ/kiểm chứng.

Source-backed dependencies: Orders `Services:Identity` phục vụ loyalty dù class có tên `CustomersApiClient`; Billing cũng gọi Identity cho loyalty, không query Customers DB. Identity mới là owner customer profile/loyalty. Customers giữ reservation/engagement/notifications.

Read-only runtime tại lượt audit: tám container Gateway/Catalog/Orders/Customers/Identity/Billing/SQL/Rabbit đang chạy; `/readyz` trên 5100–5105 đều trả `ready`. Đây là **local readiness**, không phải cloud acceptance. Không chạy build/test tạo fixture hoặc SQL migration ở lượt này.

### Tài liệu không thay thế source

`MOBILE_API_V1_CONTRACT.md` còn một số B0 state/Planned text cũ; không dùng các trạng thái đó để tái tạo schema/business rules. Migration phải giữ implementation đã nghiệm thu, bao gồm `READY → SERVING`, service request `OPEN → ACKNOWLEDGED → COMPLETED`, payment/split/closed-history thực tế. `DATABASE_OWNERSHIP_AUDIT.md` cũ có findings synonym trước cleanup; cleanup docs và `docker/db-init.sh` hiện tại đã loại alias allowlist cho mọi owned service. Không phục hồi alias hoặc legacy shared DB vì tài liệu cũ.

## SQL Server -> PostgreSQL blockers

### A / B / C compatibility

| Class | Construct/source finding | Migration treatment |
|---|---|---|
| A — logic tương thích | LINQ CRUD, local FK/PK, transactions, DTO/business state machines, integer external IDs, persisted idempotency keys/hash, decimal money | Giữ logic/contract; retest SQL translation và isolation với PostgreSQL |
| A/B | `decimal(18,2)`, `decimal(18,3)`, `DateOnly`, varchar lengths | Numeric/date có tương đương; giữ precision/scale, VND rounding và Unicode |
| B — mapping cần chỉnh | Năm `UseSqlServer`, EF SqlServer 8.0.11 packages | Npgsql EF provider tương thích EF major; `UseNpgsql`; connection strings PostgreSQL/TLS; không tự upgrade hàng loạt |
| B | `datetime`, `datetime2`, `nvarchar(max)`, `bit`, identity annotations | Timestamp policy, text, boolean, PostgreSQL identity/sequence; bỏ SQL Server store types/annotations |
| B | `HasDefaultValueSql("(getdate())")`, `SYSUTCDATETIME()` | PG defaults thích hợp theo UTC/local semantics; không thay mọi field thành timestamp giống nhau |
| B | Filtered indexes `[IdempotencyKey] IS NOT NULL`, `[EndedAtUtc] IS NULL`, `[IsActive] = 1` | PG partial indexes với identifier quoting/boolean đúng; giữ ràng buộc một active session/bàn, một final invoice/session |
| B | SQL Server case-insensitive lookup/uniqueness thường gặp vs PostgreSQL mặc định | Xác minh collation thực tế, username/email/code semantics; normalization hoặc collation/extension đã review; không làm khách cũ đăng nhập sai hoặc cho duplicate khác hoa/thường |
| C — viết lại | T-SQL bootstrappers của cả 5 service: `OBJECT_ID`, `COL_LENGTH`, `sys.*`, `IF/BEGIN`, dynamic `EXEC`, `dbo`, `MERGE`, `DATEADD`, seed/backfill | PostgreSQL baseline + controlled deployment migration; không chạy DDL/demo repairs ở mỗi cold start |
| C | Runtime raw SQL Inventory/Catalog: `UPDATE dbo.Ingredients...`; Orders runtime ingredient-schema repair | Sửa schema/case/SQL dialect; đưa request-time DDL ra deploy migration; giữ atomic stock decrement |
| C | `sys.sp_getapplock` trong Orders, Cashier, Billing finalization và Identity loyalty settlement | Thiết kế PG transaction-scoped advisory lock/row-lock tương đương, key/timeout/retry rõ; concurrency test bắt buộc |
| C | SQL duplicate detection `2601`/`2627` trong Orders/Cashier | PG SQLSTATE/constraint-aware mapping; preserve conflict/idempotency behavior |
| C | Catalog `IngredientBatches.RowVersion` là `byte[]` + `.IsRowVersion()`, DDL `ROWVERSION` | PostgreSQL không có SQL Server rowversion; chọn explicit concurrency token hoặc PG `xmin` với CLR mapping khác; không import byte[] cũ như auto-updating column |
| C | Identity keyless `ToView("CustomerLoyalty")` join local Customers/LoyaltyCards | Tạo view trong `identity`; dịch T-SQL local-owned view, không cross-schema join |
| C | owned-table/sql/db-init ownership conversion scripts với legacy DB copies/synonyms | Không replay các script SQL Server trên Supabase; tạo schema từ final owned model, không từ monolith |

[Npgsql EF provider](https://www.npgsql.org/efcore/) hỗ trợ PostgreSQL; [timestamp semantics](https://www.npgsql.org/efcore/release-notes/6.0.html) yêu cầu UTC cho `timestamptz` và offset 0 cho DateTimeOffset tương ứng. [Npgsql concurrency](https://www.npgsql.org/efcore/modeling/concurrency.html?tabs=data-annotations) mô tả `xmin` thay cho rowversion. Các mapping này không phải drop-in connection-string change.

### Runtime SQL đặc thù phải sửa ở phase migration

- `src/Services/*/Persistence/Context/*DbContext.cs`: store types/default SQL/index filters; hiện chưa dùng `HasDefaultSchema`.
- `src/Services/*/Persistence/Bootstrap/*DbBootstrapper.cs`: startup chờ DB rồi thực thi T-SQL. Chỉ đổi provider vẫn fail startup. Catalog còn recipe/menu/image/reference repair; Billing embed T-SQL owned Bills resource. Cloud cold start không nên tự mutate dữ liệu đã import.
- `OrdersController.cs`: `EnsureOrderItemIngredientOverrideSchemaAsync`, ADO.NET applock, SQL Server error-number detection.
- `CashierController.cs`: ADO.NET applock và duplicate detection.
- `PaymentFinalizationService.cs`: lock quote trong DB transaction, invoice reconciliation receipts. Phải giữ lock trong cùng transaction/connection; không dùng session-level locks tùy tiện qua transaction pooler.
- `IdentityController.cs`: loyalty settlement applock, persisted settlement key.
- `CatalogController.cs`, `AdminCatalogController.cs`: conditional stock SQL phải giữ affected-row semantics, đúng schema và identifier case.
- `sql/identity-owned-tables.sql`: view `CustomerLoyalty`; `sql/billing-owned-tables.sql`, `sql/catalog-owned-tables.sql`, `sql/orders-owned-tables.sql`, retirement scripts, `docker/db-init.sh`: SQL Server provisioning, không cloud migrations.

Audit active source không thấy trực tiếp `new SqlConnection`; ADO.NET dùng `db.Database.GetDbConnection()`, **không có nghĩa SQL portable** vì CommandText vẫn T-SQL. Không tìm thấy active computed-column mapping (`HasComputedColumnSql`); `OrderItems.LineTotal` là decimal lưu sẵn, không được tự invent generated column. Không thấy application-defined stored procedure/trigger tạo bởi active API/migration đã kiểm tra; dependency procedure thật là SQL Server system `sp_getapplock`. Legacy schema có views `ActiveOrders`, `DishDetails`, `BranchRevenue`, `CustomerLoyalty`, `TableNumbers`; không mặc định đưa tất cả legacy views lên PG, chỉ view thực sự được runtime map/use.

Không tìm thấy active `NEWID()`/`GETUTCDATE()`/uniqueidentifier SQL mapping trong scoped service search; GUID business codes/family IDs chủ yếu tạo bằng `Guid.NewGuid()` và lưu string. Không chuyển các mã `INV-*`, `PAY-*` hoặc API IDs sang UUID chỉ vì PostgreSQL có UUID type. `SYSUTCDATETIME()` và `GETDATE()` có thật và phải xử lý.

### Complete EF migration file inventory

| Service | File trong `Migrations/` | Assessment |
|---|---|---|
| Catalog | `20260520002000_AddIngredientIssueMethod.cs` | AddColumn portable concept; dbo/N-prefixed data update phải dịch |
| Orders | `20260515000000_AddOrderItemIngredients.cs` | CreateTable concept; SqlServer Identity/types/default đổi |
| Orders | `20260517000000_AddOrderItemIngredientSnapshots.cs` | Raw `sys.foreign_keys`/COL_LENGTH repair: rewrite |
| Orders | `20260517001000_ReplaceOrderItemIngredientsSynonym.cs` | Raw synonym/physical table repair: SQL Server historical only; không replay |
| Orders | `20260520001000_AddDiningSessionTables.cs` | CreateTable/index đổi; T-SQL backfill/bit/SYSUTCDATETIME rewrite |
| Customers | `20260519000000_AddReservations.cs` | Identity/types/defaults/filtered index đổi |
| Customers | `20260519001000_AddReservationCheckInState.cs` | AddColumn concept; datetime2/store types đổi |
| Customers | `20260520000000_AddReservationTables.cs` | CreateTable/index đổi; dbo backfill rewrite |
| Identity | Không tìm thấy EF migration file | Schema hiện do owned SQL + startup bootstrap, cần PG baseline riêng |
| Billing | Không tìm thấy EF migration file | Schema hiện do embedded owned SQL + startup bootstrap, cần PG baseline riêng |

Tổng 8 source migrations. Không tìm thấy current model snapshot/Designer metadata hoặc `Database.Migrate[Async]` trong active source; schema hiện tại không được tái dựng đầy đủ chỉ từ tám file này. Không claim EF migration discovery/chain PASS vì chưa chạy provider migration thử. Baseline PG phải bao gồm tất cả final mapped + manually-owned tables/view, không chỉ các file có chữ Migration.

### Timezone, identifiers và monetary acceptance

Source trộn legacy `DateTime.Now`/`GETDATE` với mobile `UtcNow`/`...AtUtc`. SQL Server datetime không lưu timezone; không thể tự coi tất cả legacy giá trị là UTC hay giờ Việt Nam. Phải lập mapping từng field trước import, đối chiếu với history đã PASS; UI vẫn Asia/Ho_Chi_Minh, REST timestamp canonical. Reservation slot/business date cần semantics riêng; batch ExpiryDate/ReceivedDate là DateOnly, không shift timezone. PostgreSQL `now()` là transaction timestamp, không mặc định cùng hành vi clock function SQL Server.

Giữ IDs và mọi logical cross-domain reference; restore sequence sau import. Quyết định giữ PascalCase có quoting hay đổi physical names thống nhất trước generate PG migrations; raw SQL phải theo cùng lựa chọn. Không tạo cross-schema FK chỉ vì 5 schemas nằm chung database. Giữ chính xác totals/discount/split sums và invoice ItemsJson; JSON lưu text có thể giữ text giai đoạn đầu, chưa cần đổi toàn bộ sang jsonb.

## Database ownership mapping

One Supabase project / 5 schemas **phù hợp logical source boundaries**, nhưng không tương đương năm physical databases về blast radius, resource quota, backup hoặc privileged admin access. Schema isolation cần enforce bằng grants, không chỉ connection string/search_path.

| Existing DB | Target schema | Runtime-owned objects chính |
|---|---|---|
| `RESTAURANT_CATALOG` | `catalog` | Restaurants, Branches, DiningTables, TableStatus, Dishes, Categories, Menus, MenuCategory, CategoryDish, Ingredients, Units, DishIngredients, IngredientBatches, IngredientStockMovements, audit logs |
| `RESTAURANT_ORDERS` | `orders` | Orders, OrderItems, OrderItemIngredients, OrderStatus, DiningSessions, DiningSessionTables, ServiceRequests, ReservationPreOrders/Items, SubmitCommands, Inbox/Outbox, Catalog*Snapshots, audit logs |
| `RESTAURANT_CUSTOMERS` | `customers` | Reservations, ReservationTables, legacy reservation preorder items, favorites, reviews, support, ReadyDishNotifications, Inbox |
| `RESTAURANT_IDENTITY` | `identity` | Customers/profile, Employees, EmployeeRoles, LoyaltyCards + CustomerLoyalty view, LoyaltySettlements, MobileRefreshTokens, PasswordResetTokens, CustomerSettings, CatalogBranchSnapshots, audit logs |
| `RESTAURANT_BILLING` | `billing` | Bills, CheckoutCommands, OrderContextSnapshots, Outbox, CheckoutQuotes, Vouchers, MobilePayments, Invoices, SplitBills/Parts, audit logs; ownership receipt table nếu cần cho migration history |

FAQ hiện trả static backend response trong Customers, không có FAQ table bắt buộc import. Model entity files legacy không nằm trong active DbContext không được coi là tables phải migrate. Billing `Bills` hiện explicit schema `billing`, các mobile Billing tables còn default schema; phase PG phải map toàn bộ Billing vào `billing`, không để một nửa rơi vào public.

Required future design (chưa triển khai):

1. `HasDefaultSchema` mỗi context + explicit view/table mappings phù hợp. `__EFMigrationsHistory` riêng trong schema tương ứng (provider options history table), không chung public.
2. Runtime login role riêng cho từng service; role migrator/owner riêng. Runtime chỉ USAGE schema mình + SELECT/INSERT/UPDATE/DELETE tables, sequence privileges và view permissions cần thiết; không CREATE/DDL, không superuser/BYPASSRLS, không role membership grant chéo.
3. Không cấp SELECT schemas khác cho service; revoke/kiểm tra PUBLIC default privileges và public schema CREATE. `search_path` không phải security boundary. Default privileges phải thuộc đúng role thực sự tạo objects, kể cả objects tạo sau này.
4. Không expose 5 business schemas qua Supabase Data API cho anon/authenticated để Android bypass Gateway. Supabase Auth/service_role key không thay Identity JWT. Domain service kết nối PostgreSQL bằng least-privilege DB role; shared project admin chỉ dùng provisioning.
5. API/event orchestration giữ nguyên. Catalog snapshots ở Orders/Identity, OrderContextSnapshots ở Billing là local read models hợp lệ, không biến thành PG joins qua schemas.
6. Migration acceptance: role orders query catalog trực tiếp phải bị permission denied; normal Orders→Catalog HTTP vẫn PASS. Tương tự 5 roles, write/read/sequence/view, no-cross-schema permissions tests.

[Supabase PostgreSQL roles](https://supabase.com/docs/guides/database/postgres/roles) và [custom schemas](https://supabase.com/docs/guides/api/using-custom-schemas) xác nhận nền tảng hỗ trợ roles/schemas; hướng dẫn expose schema cho Data API không phải kiến trúc client của VietTable.

Persistent containers có thể dùng direct PostgreSQL hoặc session pooler tùy IPv6/IPv4. Vercel autoscaling cần connection-budget/session-vs-transaction pooler review. Shared pooler hỗ trợ custom role username theo role/project ref; transaction pooler không mặc định giữ session state/prepared statements. Không dùng pooler transaction để chạy session-level lock/search_path assumption. Bound `MaxPoolSize` theo **tổng 5 services × replica count + workers/migrators**, không mỗi API dùng mặc định lớn. Nguồn: [Supabase connections](https://supabase.com/docs/guides/database/connecting-to-postgres), [prepared statement guidance](https://supabase.com/docs/guides/troubleshooting/disabling-prepared-statements-qL8lEL).

### Data migration policy

Đây là phân loại dựa ownership/source/acceptance docs, **không phải live row-by-row inventory**. Không export và không đọc customer credential/PII rows. Không tự xóa local data hoặc kết luận mọi row cũ là TEST dựa tên/ID.

| Data | Class | Khuyến nghị |
|---|---|---|
| Role/status reference, Units, category definitions | REFERENCE DATA | Migrate verified definitions; preserve exact codes/case và role/branch semantics |
| 3 demo branches, table layout/QR codes, approved menu/dish/category/menu links, recipes, approved image paths/assets | DEMO + REFERENCE | Mang curated demo catalog, không mang temporary QA objects; stock/batch expiry phải được review riêng |
| Employees/admin/chef/cashier demo + branch assignments | DEMO / identity | Chỉ accounts được duyệt; giữ hash format và roles/branch, rotate mật khẩu demo yếu trước public exposure; không commit password |
| Customer profiles/Google subject/settings/loyalty | IDENTITY + TRANSACTIONAL | Migrate authorized users và related receipts consistently, hoặc fresh QA users qua legit Register; không clone mọi test customer mặc định |
| Reservation / ReservationTables / preorder | TRANSACTIONAL hoặc TEST theo provenance | Giữ approved history và full related rows; không copy expired active state như reservation tương lai; bảo toàn IDs/logical refs |
| DiningSession / orders / items / ingredient snapshots / service request | TRANSACTIONAL hoặc TEST | Giữ approved closed demo history nguyên nhóm/round order; quyết định active sessions riêng trước cutover, không silently replay |
| Quotes / payments / invoices / split parts / settlement receipts | TRANSACTIONAL | Migrate một consistent closed aggregate nếu cần history; totals/items/paid part/final invoice phải match; không discard settled records tùy tiện |
| Favorites/reviews/notifications/support | TRANSACTIONAL hoặc TEST | Migrate approved customer-scoped records và referenced entities; FAQ không có DB export |
| Refresh tokens / reset tokens / web session | SECURITY TRANSIENT | Khuyến nghị revoke/relogin lúc cutover thay vì copy live auth state; user identities/hash khác với session/token |
| Submit/Checkout command receipts, Inbox/Outbox, audit trail | TRANSACTIONAL / operational | Retain dedupe/receipts cần cho records giữ lại; drain/quarantine pending work và preserve processed state, không import PENDING rồi replay duplicate |
| Generated instrumentation fixtures / QA TEMP records / failure logs / stale caches | TEST / operational | Không chọn import mặc định; chỉ loại khỏi migration manifest sau provenance review, không xóa source DB |

Hai bộ dữ liệu có thể chọn sau audit: (a) curated demo reference + authorized demo identities, không mang QA transactions; (b) reference + approved complete transaction history để chứng minh history. Không trộn cách (a) cho Orders và (b) cho Billing/Customers. `RESTAURANT` legacy không phải source of truth cho domain dữ liệu mới.

## RabbitMQ / worker blockers

### Actual messaging, không suy đoán từ class name

Orders/Billing `Infrastructure/Eventing/IntegrationEventing.cs` có `FileIntegrationEventPublisher`: lưu DB Outbox nếu event nằm trong configured tracked events → append JSONL file → best-effort Rabbit publish. Connection/channel mở và dispose **mỗi publish**. Exchange topic durable `selfrestaurant.events`; routing key `selfrestaurant.{eventName.Replace('.', '_')}`; config đổi prefix/exchange được.

Events source tìm thấy: `order.submitted.v1`, `order.received-confirmed.v1`, `order.cancelled.v1`, `order.status-ready.v1`, `payment.completed.v1`. Envelope chứa EventName/OccurredAtUtc/Source/CorrelationId/Payload. Rabbit routing key ví dụ `selfrestaurant.order_status-ready_v1`, không phải tên event nguyên dấu chấm. Consumer HTTP giữ eventName nguyên bản.

**Không tìm thấy QueueDeclare/QueueBind/BasicConsume hay AMQP queue subscription trong active API source.** Hai service có Rabbit package là publishers. Không có queues/bindings do source thiết lập để liệt kê; không claim broker live không có queue vì chưa query broker inventory.

| Hosted service | Actual source path / trigger | Interval / durability | Deployment verdict |
|---|---|---|---|
| Orders `PaymentCompletedConsumerService` | BillingEventsClient GET `/api/internal/outbox/pending?eventName=payment.completed.v1`, then POST ack | Default 5s, Inbox dedupe `(Source,SourceEventId)`, 3 retries rồi DEAD, retry backoff tới 60s | **VERCEL SCALE-TO-ZERO BLOCKER** unchanged |
| Customers `OrderReadyConsumerService` | OrdersEventsClient GET pending `order.status-ready.v1`, then POST ack | Default 2s, local notification projection + Inbox, same retry/dead pattern | **VERCEL SCALE-TO-ZERO BLOCKER** unchanged |
| Billing `PaymentReconciliationWorker` | Local invoices chưa `SettlementCompletedAtUtc` → Orders CompletePayment / Identity SettleLoyalty | 5s, max 50/iteration, invoice là durable work item, retries later | **VERCEL SCALE-TO-ZERO BLOCKER** unchanged |
| Catalog `TableAutoReleaseService` | Local expired OCCUPIED/no-current-order tables | Config default 60s; idle timeout15min, DateTime.Now | **VERCEL SCALE-TO-ZERO BLOCKER** when enabled; verify actual selected config |

Tất cả được `AddHostedService` từ Program.cs, chạy khi API process sống. Không phải subscription startup tự đăng ký queue. Broker hosted cloud không giữ API processes alive. Đặc biệt Billing synchronous reconciliation cố chạy sau payment commit nhưng catch lỗi và để worker retry: nếu worker không chạy lúc không có traffic, payment SUCCEEDED nhưng session/loyalty/table finalization có thể bị treo.

Rabbit publish không có publisher confirms, mandatory routing/returned-message handling hoặc persisted Rabbit delivery retry trong code đã inspect; `BasicPublish` success không chứng minh downstream delivery. Persistence flag/exchange durable **không thay queue binding hoặc ack**. Publish failures log/catch; lỗi filesystem nằm trước Rabbit publish trong cùng try nên có thể bỏ qua Rabbit send dù outbox đã lưu. DB outbox HTTP pipeline là luồng nghiệp vụ thật cần giữ, không replace nhầm bằng broker-only.

Vercel current container docs xác nhận background loop trong web container có thể bị dừng khi scale down; phải chuyển thành bounded invoked jobs hoặc external persistent workers. Không dùng keepalive ping để giả always-on. Nguồn: [Vercel Docker workers](https://vercel.com/kb/guide/docker-monolith-workers-vercel).

### Managed RabbitMQ compatibility

**Split host/port/user/password/vhost config có thể thay bằng cấu hình cloud cho AMQP không TLS**, nếu provider cho phép. Nhưng source chưa đọc `RABBITMQ_URL`/`RabbitMq:Url`, chưa set factory.Uri/Ssl. Vì vậy **không đạt “chỉ đặt AMQPS URL là xong”**. Port5671 một mình không bật TLS.

Future minimum change: hai publishers cùng đọc URI cấu hình hoặc explicit TLS settings; certificate/hostname validation đúng, không bỏ verify; credentials/vhost từ secret store, heartbeat/timeouts/reconnect policy phù hợp; bounded connection lifecycle/resource reuse tùy workload. Không đổi event contracts/ownership vì đổi broker. [CloudAMQP .NET](https://www.cloudamqp.com/docs/dotnet.html) dùng ConnectionFactory.Uri; [TLS FAQ](https://www.cloudamqp.com/docs/faq.html) mô tả amqps scheme. Chưa tạo cloud broker/account hoặc test AMQPS tại lượt này.

Managed Rabbit giữ được Rabbit protocol ở phase đầu, nhưng không chuyển HTTP outbox consumers thành AMQP consumers. Với Option A giữ worker HTTP unchanged về kiến trúc; hardening publish reliability là work riêng có targeted tests, không tự claim được giải quyết bởi provider.

## SignalR

Gateway Program.cs map hai hub:

- `/hubs/mobile-notifications`: `MobileNotificationsHub`, `[Authorize(Roles="customer")]`; JWT sub → `mobile-customer:{id}`. Query access_token chỉ nhận ở đường hub này. Cần redact query tokens trong proxy/platform logs.
- `/hubs/customer-notifications`: `CustomerNotificationsHub`; table/branch subscribe methods, **không có Authorize trên hub**. Cashier Dashboard dùng branch subscription. Public cloud cần review unauthorized subscriptions/payload disclosure; không mặc định safe chỉ vì REST role QA đã PASS.

Gateway controllers trực tiếp gọi `IHubContext.SendAsync`, không consume Rabbit để forward hub events: MobileDiningOperations gửi `orderUpdated`, `serviceRequestUpdated`; Chef gửi ready/table notification và mobile `orderUpdated`; Cashier service-request action gửi mobile event; MobileBilling gửi `paymentUpdated` cho một số actions. Android `SignalRManager` hiện **chỉ register `orderUpdated` và `serviceRequestUpdated`**, không register `paymentUpdated`; payment waiting automatic success là polling canonical, không được claim SignalR payment E2E.

Android URL từ `ApiClient.absoluteUrl`, access-token provider lấy token hiện hành, connection/session generation guard, reconnect backoff, auth refresh và REST resync khi connected; lifecycle stop/start đã tồn tại. Không rewrite Supabase Realtime. Mọi notification vẫn phải REST resync; event không authoritative hoặc guaranteed replay.

| Scenario | Assessment |
|---|---|
| Một persistent Gateway instance | ASP.NET SignalR giữ được implementation; TLS/WSS/reconnect cloud acceptance vẫn phải chạy |
| Nhiều Gateway replicas | Cần backplane/managed SignalR để SendAsync tại replica A tới connection ở B; shared web session và DataProtection keys riêng biệt với hub backplane |
| Sticky only | Không đủ broadcast cross-instance; chưa giải quyết staff session durability hoặc scale-down |
| Stateless enough? | REST/business DB phần lớn shared canonical; hub connection/group và browser session vẫn process-local, nên **không** hoàn toàn stateless |
| Vercel HTTP/Docker | WebSocket beta hiện có; .NET SignalR negotiate + transport + affinity + maxDuration/reconnect cần POC, không được coi unchanged PASS |

Theo [Microsoft SignalR scaling](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale?view=aspnetcore-8.0), Redis backplane không tự loại yêu cầu sticky sessions trong mọi transport scenario. Source Android không set SkipNegotiation; không giả định client là WebSocket-only bỏ negotiate. Vercel WebSocket hỗ trợ là khả năng platform, không bảo đảm SignalR negotiation requests vào đúng replica hoặc cross-instance groups.

[Vercel Docker/WebSocket](https://vercel.com/kb/guide/docker-on-vercel-vs-render) hiện xác nhận container functions có thể phục vụ WebSocket beta, connection pinned và chịu function duration; [reconnect guidance](https://vercel.com/docs/functions/websockets) yêu cầu reconnect/resubscribe/state reload. Một tài liệu limits cũ nói không support WebSocket không được dùng để phủ nhận capability mới. Cần POC thực tế trước quyết định Gateway Vercel; chưa có cloud deployment E2E ở lượt audit.

## Vercel compatibility matrix

Đánh giá **current whole deployment unit**, không chỉ có build được container. Tất cả API có Dockerfile thường, chưa có `Dockerfile.vercel`, `vercel.json` service routing/bindings. `.NET not supported` không phải finding: [official ASP.NET Docker guide](https://vercel.com/kb/guide/dot-net-asp-net-on-vercel-with-docker) chứng minh deployability của HTTP .NET. Các current Dockerfiles bind8080; phải cấu hình matching PORT/runtime routing, không chỉ rename file. Build contexts/resource copies cũng phải đúng.

| HTTP project | Current verdict | HTTP-only feasibility / remaining changes |
|---|---|---|
| Gateway | NEEDS CHANGES | Docker HTTP feasible; shared session/DataProtection, uploads, proxy trust, SPA path routing, SignalR scaling/POC, private backend bindings |
| Catalog | NEEDS CHANGES | HTTP feasible với Dockerfile.vercel; PG conversion, startup DDL/repair, remove/relocate table scheduler nếu enabled |
| Orders | NEEDS CHANGES | HTTP feasible với Dockerfile.vercel; PG + lock/error mapping + outbox worker separation + JSONL/AMQPS |
| Customers | NEEDS CHANGES | HTTP feasible với Dockerfile.vercel; PG startup + HTTP ready consumer separation |
| Identity | NEEDS CHANGES | HTTP feasible với Dockerfile.vercel; PG startup/view/lock; config JWT/SMTP/OAuth; rate limit/IP/proxy distribution |
| Billing | NEEDS CHANGES | HTTP feasible với Dockerfile.vercel; PG embedded SQL/locks; reconciliation worker separation; JSONL; provider callback routing |
| Customer Web | NEEDS CHANGES (deployment config) | Static Vite hosting ready in principle; same-origin API/images rewrites or explicit Gateway origin; deep-link fallback; Google web origin config |
| Chef Web | NEEDS CHANGES (deployment config) | Static Vite hosting ready in principle; `/app/chef/` base/Router paths, Gateway cookie API, cross-app redirects |
| Cashier Web | NEEDS CHANGES (deployment config) | Static Vite hosting ready in principle; `/app/cashier/`, API and `/hubs/customer-notifications` routing/origin; cookie session |
| Admin Web | NEEDS CHANGES (deployment config) | Static Vite hosting ready in principle; `/app/admin/`, Gateway multipart upload/media routes, session |

“VERCEL READY WITH DOCKERFILE.VERCEL” chỉ phù hợp cho **HTTP-only phần đã xử lý những blockers**; chưa project API nào đạt nguyên trạng với target Supabase. Các worker ở bảng trước: **NOT SUITABLE unchanged on scale-to-zero**, không gán NOT SUITABLE cho cả .NET HTTP language/runtime. Không web nào cần Docker chỉ để host static dist.

[Vercel Services hiện hành](https://vercel.com/kb/guide/vercel-services) dùng service roots, public rewrites và explicit private bindings; request path giữ nguyên theo tài liệu mới. Không bê syntax skill beta cũ `experimentalServices`/strip-prefix vào config future. Bindings cho reachability không tự authenticate service; dependency graph có cả Identity↔Billing/Orders phải declare đủ và giữ service authorization.

### Filesystem và process-state blockers

| State | Source / path | Cloud treatment |
|---|---|---|
| Uploaded dish image | Gateway `AdminGatewayController` và `StaffChefGatewayController`, SaveDishImageAsync → `wwwroot/images/dishes`; delete/write slug files | Object storage/CDN hoặc persistent host volume phase đầu; immutable packaged assets khác với mutable uploads |
| Upload volume | Compose mount `/app/wwwroot/Images/uploads` | Không bao phủ actual lowercase `/images/dishes` writes; Linux case/path mapping phải kiểm chứng, không claim volume hiện tại bảo vệ dish uploads |
| Approved dish/branch assets | Gateway `wwwroot` static + Catalog ImagePath | Có thể package immutable hoặc move storage với URL manifest; đảm bảo 52 dish/3 branch acceptance và relative URL routing còn đúng |
| Integration JSONL | Orders/Billing `App_Data/integration-events/{service}-events-{date}.jsonl` | Console/central logs hoặc durable log storage; không source of truth thay DB Outbox; filesystem failure đang ảnh hưởng publish sequence |
| Browser sessions | Gateway AddDistributedMemoryCache/AddSession | Redis/distributed session backend hoặc persistent single instance với restart limitation; Vercel scale-out không thể dùng memory session như hiện tại |
| DataProtection | ASP.NET default + prod Gateway keys volume | Shared durable protected keys across replicas/redeploy; session storage alone không đủ |
| Cache/rate limiter | Gateway recommendation cache, Orders memory cache, Identity memory/rate limiter | Không giữ business truth trong RAM; distributed limit hoặc edge security khi scale; cold start mất cache là expected |
| SQL/broker volumes | Compose mssql/rabbit data | Managed services, không mount/replay laptop volumes trên Vercel |
| TLS/certificates | Caddy persistent data/config; dev localhost HTTPS launch profiles | Managed public TLS/host proxy; không copy dev cert/private keys vào image/repo |

Scoped active runtime search không thấy SQLite database, local invoice PDF generation hoặc app-specific certificate file loading. Không coi mọi logs/temp/screenshot/dev SQL script là production dependency. `PhysicalFileProvider`, frontend dist search paths và packaged files là read-only assets khả thi; upload writes và JSONL là state khác cần thay.

## Android cloud cutover

Android **gần ready ở tầng configuration**, chưa HTTPS/WSS cloud accepted. Không cần đổi package/networking stack hoặc cho app gọi Supabase/domain services.

| File | Current behavior | Next phase action |
|---|---|---|
| `app/build.gradle` | Debug default emulator URL; Gradle property `VIETTABLE_API_BASE_URL`; release validation HTTPS origin, không local/reserved host/path/custom port | Set real Gateway HTTPS origin ở build config/CI; thường không cần sửa source này; không dùng script local để build cloud APK |
| `network/ApiClient.java` | REST URL từ BuildConfig; authenticated request chỉ same protocol/authority; release HTTPS enforced, redirects disabled | Giữ URL/contract; verify public host cert/status/error/correlation/idempotency/refresh; không rely redirect từ HTTP sang HTTPS |
| `network/SignalRManager.java` | Same Gateway base `/hubs/mobile-notifications`, token provider/reconnect/resync | URL tự theo BuildConfig; WSS/negotiate cloud test; chỉ sửa nếu POC phát hiện incompatibility thật |
| `data/MobileRepository.java` | Menu/branch image dùng `api.absoluteUrl`; API qua ApiClient | Verify returned media paths resolve đúng public origin; không reintroduce mock |
| `util/ImageLoader.java` | HttpURLConnection URL từ mapped data, follows redirects | Verify HTTPS image URLs/cert/CDN; no credentials forwarded by this loader; storage URL policy khi cutover |
| `ui/auth/LoginFragment.java`, `GoogleSignInCoordinator.java`, `GoogleSignInConfig.java` | BuildConfig.GOOGLE_WEB_CLIENT_ID → Credential Manager → Gateway Identity | Giữ Android audience đúng Identity ClientId; release SHA/package OAuth registration riêng; không Client Secret Android |
| `app/src/main/AndroidManifest.xml` + main network security config | INTERNET, cleartext false, system CAs; explicit local cleartext denied | Giữ production policy; không thêm trust-all/self-signed bypass |
| `app/src/debug/res/xml/network_security_config.xml` | HTTP chỉ local loopback/emulator exceptions | Giữ cho local dev; cloud debug dùng HTTPS, không cần adb reverse |
| `scripts/connect-phone.ps1`, `test-integration.ps1`, `test-android-instrumentation.ps1`, demo scripts | Local URLs/ADB reverse/Docker/table lookup | Giữ local workflow; future cloud acceptance script/config riêng hoặc tham số hóa, không làm mất local baseline |

Phân biệt **file cần cấu hình** và **file cần sửa**: build property/API deployment settings đủ cho Android base/hub URL; source ApiClient/SignalR không cần rewrite chỉ để đổi host. `VIETTABLE_GOOGLE_WEB_CLIENT_ID` là public OAuth client identifier, không secret, nhưng phải match `Authentication:Google:ClientId` mobile. Google web dùng `Authentication:Google:WebClientId`/`VITE_GOOGLE_CLIENT_ID` riêng; không merge audience.

Cloud acceptance sau migration: Samsung Wi-Fi/mobile data **không USB reverse**, password/Google auth, reconnect/background/cold launch, QR/order/kitchen/realtime, service requests, ONSITE auto-success, split finalization, invoice/grouped history/timezone và ảnh; Gateway unavailable phải show error thật. Các mục này chưa chạy ở cloud, không tận dụng local PASS để đánh cloud PASS.

## Secret/config changes

### Names only

Không report giá trị. Existing names cần map vào host secret/config store (ASP.NET dùng `__` cho section delimiter):

| Area | Current config/environment names |
|---|---|
| DB | `ConnectionStrings:CatalogDb`, `ConnectionStrings:IdentityDb`, `ConnectionStrings:RestaurantDb`, `MSSQL_SA_PASSWORD` (local SQL only) |
| JWT | `MobileJwt:SigningKey`, `MobileJwt:Issuer`, `MobileJwt:Audience`, `MobileJwt:AccessTokenMinutes`, `MobileJwt:RefreshTokenDays`, `MOBILE_JWT_SIGNING_KEY` Compose input |
| Internal URLs | `Services:Catalog`, `Services:Orders`, `Services:Customers`, `Services:Identity`, `Services:Billing`, `Services:TimeoutSeconds` |
| Rabbit | `RabbitMq:Enabled`, `RabbitMq:Host`, `RabbitMq:Port`, `RabbitMq:Username`, `RabbitMq:Password`, `RabbitMq:VirtualHost`, `RabbitMq:Exchange`, `RabbitMq:RoutingKeyPrefix`; prod Compose `RABBITMQ_USER`, `RABBITMQ_PASSWORD` |
| Proposed broker setting | `RABBITMQ_URL` / `RabbitMq:Url`: **chưa được source đọc**, cần minimal implementation nếu chọn URI |
| Google mobile/web | `Authentication:Google:ClientId`, `Authentication:Google:WebClientId`, `SELFRESTAURANT_GOOGLE_CLIENT_ID`, `SELFRESTAURANT_GOOGLE_WEB_CLIENT_ID`, Android `VIETTABLE_GOOGLE_WEB_CLIENT_ID`, web `VITE_GOOGLE_CLIENT_ID` |
| SMTP | `Smtp:Enabled`, `Host`, `Port`, `UseSsl`, `Username`, `Password`, `FromEmail`, `FromName`, `TimeoutMs`; `SELFRESTAURANT_SMTP_PASSWORD`; `PasswordReset:PublicBaseUrl` |
| VNPAY | `Payments:VnPay:Enabled`, `TmnCode`, `HashSecret`, `PaymentUrl`, `ReturnUrl`, `ExpiryMinutes`; `VNPAY_ENABLED`, `VNPAY_TMN_CODE`, `VNPAY_HASH_SECRET`, `VNPAY_PAYMENT_URL`, `VNPAY_RETURN_URL`, `VNPAY_EXPIRY_MINUTES` |
| Gemini optional | `GEMINI_API_KEY`; `Gemini:BaseUrl`, `Model`, recommendation timeout/cache/candidate configs |
| Proxy/TLS | `ReverseProxy:KnownProxy`, `ReverseProxy:PublicHost`, `Security:EnableHsts`, `HttpsRedirection:Enabled`, `PUBLIC_HOST`, `VIETTABLE_PROXY_IP`, `VIETTABLE_DOCKER_SUBNET`, `ENABLE_HSTS` |
| Host/frontend | `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`, Vercel `PORT` matching container; `Frontend:CustomerDistPath/ChefDistPath/CashierDistPath/AdminDistPath`, `VITE_GATEWAY_URL`, Android `VIETTABLE_API_BASE_URL` |
| Worker/event config | `IntegrationEvents:Enabled/Directory/OutboxTrackedEvents`, `IntegrationEvents:Consumers:OrderReady:*`, `...:PaymentCompleted:*`, `TableAutoRelease:*` |

DB passwords, signing keys, SMTP password, broker URI credentials, VNPAY HashSecret, Gemini key là secrets. OAuth client IDs/model/host names không là password nhưng vẫn phải cấu hình theo environment. Không phát hiện nhu cầu Google Client Secret cho Android ID-token flow; không thêm vào APK. Không copy `.env`, `.env.local`, localhost cert/private key hay QA credentials lên Git/cloud image. `.env.example` chỉ dùng inventory variable names.

### Local endpoint classification

| Occurrence family | Classification | Treatment |
|---|---|---|
| `Properties/launchSettings.json`, Vite dev/preview ports, Android debug default/cleartext exceptions, scripts test/demo/connect-phone | LOCAL DEV ONLY | Giữ local; không mang làm runtime cloud config |
| Appsettings service URLs `localhost:5101–5105`, Compose names `catalog:8080` etc. | PRODUCTION MUST CHANGE; safe local defaults nếu overridden đúng | Secret/config bindings/private cloud URLs; startup config validation để không fallback local ở production |
| SQL local connection values/1433, Rabbit host/5672, provider return public-placeholder, password-reset public base | PRODUCTION MUST CHANGE | PG endpoint/TLS và broker AMQPS; actual public callbacks/reset origin |
| Android release BuildConfig URL | PRODUCTION MUST CONFIGURE, **không hardcoded runtime bug** | Guard đã cấm local/HTTP; cần supply real HTTPS origin |
| Vite `VITE_GATEWAY_URL` | SAFE DEV DEFAULT ONLY | Chỉ dùng trong `server.proxy`; **không** tự thay relative fetch trong production dist |
| Frontend relative `/api`, cashier relative `/hubs`, image `/images`, cross-app nextPath | NEEDS PRODUCTION ROUTING | Same-origin routing hoặc explicit trusted Gateway origin + credentialed CORS; không invent direct downstream access |
| Container8080 vs Vercel routing PORT | DEPLOYMENT MISMATCH nếu bê file nguyên trạng | Configure matching bind/routed port; `EXPOSE` không sửa bind |
| Gateway known proxy requirement | PRODUCTION MUST REVIEW | Current code yêu cầu một known IP và DNS host; managed edge trust khác Caddy fixed IP, không tắt trust checks hàng loạt |

Không tìm thấy hardcoded local Gateway URL trong Android production network methods; local URLs ở build/debug/scripts là intentional. Tuy nhiên chỉ đặt `VITE_GATEWAY_URL` cho build và mong prod API đổi host là assumption sai với source hiện tại. Không sửa ở audit này.

## Option A

**Low-risk trong hai target đang xét; vẫn có DB migration đáng kể.**

- Bốn static web trên Vercel, giữ route base hiện tại và deep links.
- Gateway + 5 .NET API trên persistent cloud container host/private network; workers vẫn chạy trong owning service để ít rewrite nhất. Đầu tiên một Gateway instance, không autoscale cho tới session/backplane đã sẵn sàng.
- Supabase PostgreSQL một project, năm least-privilege schemas/roles/migration histories.
- Managed RabbitMQ qua AMQPS; giữ HTTP outbox projection/reconciliation đúng source, không giả broker replacement thay workers.
- Public HTTPS Gateway/WSS; Android chỉ gọi origin này.
- Mutable images external object storage/CDN; central console logs. Durable DataProtection/session storage khi cần redeploy/scale; single persistent instance chỉ tránh scale-out, không tránh session mất khi restart.

Web/API routing phải được chọn trước triển khai: hiện relative fetch/cookie/same-origin. Có thể giữ same-origin REST bằng edge rewrites đã test, nhưng không giả ngoại proxy `/hubs` luôn hỗ trợ WSS. Phương án rõ ràng hơn khi tách origins: bốn web dưới một public web origin, Gateway origin HTTPS riêng, cập nhật web API/hub base nhỏ và Gateway allowlist credentialed CORS, cookie Secure/SameSite theo site/domain thực tế, cross-app redirects/Google origins; test CSRF/session policies. **Không dùng wildcard credentialed CORS**. Nếu dùng subdomains cùng site, không tự đặt SameSite=None khi Lax đủ; nếu khác site cần phân tích third-party cookie restrictions.

Deployment khó hơn static-only vì private networking/process uptime, nhưng lifecycle worker/SignalR gần với local. Vercel webs không buộc .NET chạy trên Vercel. Chọn host có always-on/private services, TLS/WSS và resource budget thực tế; không chọn free sleeping service cho mục tiêu “mọi lúc”. Chưa chọn/mua/tạo tài khoản host ở audit.

## Option B

**Vercel-first cho HTTP; không deploy nguyên trạng rồi chờ workers chạy.**

- .NET HTTP services qua `Dockerfile.vercel` + Vercel Services; public rewrites chỉ Gateway/media/provider callbacks cần thiết, internal services private bindings.
- Supabase PG mapping/security như Option A.
- Loại `AddHostedService` loops khỏi HTTP units hoặc tách worker host riêng (nếu chọn tách host thì là hybrid, không Vercel-only).
- Redesign HTTP outbox consumers/settlement/table scheduler thành bounded invoked jobs với persisted receipts, delivery retries, visibility/dedupe và scheduling. Existing Rabbit publisher không thể wake Vercel function nếu không có queue consumer/bridge được thiết kế.
- Evaluate Vercel Queues **không phải AMQP URL replacement**: hiện docs có HTTP API/JS/Python SDK, durable topics và at-least-once delivery. .NET integration/trigger authentication bridge, retry/dead/replay cần POC; giữ event contract và cross-service ownership. Không rewrite vào audit.
- Gateway session/key store ngoài RAM/files; SignalR Redis/backplane hoặc managed SignalR theo compatible POC, forced reconnect và REST hydration. WebSocket beta không tự làm ASP.NET hubs stateless.
- Startup schema/seed/repair chuyển deploy migration pipeline; pooled PG budgets cho autoscale/cold starts; JSONL/upload externalized.

Nguồn: [Vercel Queues](https://vercel.com/docs/queues) và [Docker workers guide](https://vercel.com/kb/guide/docker-monolith-workers-vercel). Queues available không bảo đảm .NET callback SDK/worker parity hoặc ordering nghiệp vụ cũ; new delivery semantics là thay đổi lớn cần acceptance.

### Comparison và monthly/free-tier feasibility

| Dimension | Option A | Option B |
|---|---|---|
| Source changes | PG migration + cloud config/media/session; giữ workers/hubs model | Toàn bộ Option A + worker invocation/broker bridge, stateless multi-instance Gateway, SignalR POC |
| Deployment difficulty | Persistent private network/uptime + database cutover | Bindings/routing/runtime limits + autoscale/queue triggers + DB connection budget |
| Reliability với current source | Thấp rủi ro hơn; workers luôn có process; cần monitoring/backups | Không đảm bảo cho tới test idle/scale-down, redelivery, cold start, affinity và finalization |
| Demo suitability | Ưu tiên; gần luồng đã PASS | Chưa nên dùng làm migration đầu tiên trước bảo vệ/demo |
| Monthly cost | Có fixed always-on compute, DB/broker/storage/network tùy plan | Usage compute/service requests/queue/realtime/PG; không mặc định rẻ hơn khi polling và nhiều internal calls |
| Free tier | Web/demo reference có thể vừa quota; always-on target không được hứa $0 | HTTP beta/free quota có thể đủ thử nghiệm; không biến workers thành free reliable daemon |

Ngày audit, [Supabase pricing](https://supabase.com/pricing) Free có 500MB database, 1GB file storage và pause sau một tuần inactivity; không đáp ứng lời hứa uninterrupted “mọi lúc” nếu để inactive. [CloudAMQP plans](https://www.cloudamqp.com/plans.html) Little Lemur là shared development Rabbit, free, 20 connections/1M messages per month/queue quotas; current per-publish connection pattern cần xem burst. [Vercel Hobby](https://vercel.com/docs/plans/hobby) dành non-commercial personal use và có usage limits. Không cộng thành monthly total hoặc quote paid host khi chưa chọn replica sizes/region/traffic/storage; không đảm bảo free tiers phù hợp production restaurant.

## Recommended architecture

**Chọn Option A cho lần chuyển cloud đầu tiên.**

```text
Android ── HTTPS/WSS ── Public Gateway (persistent .NET)
Vercel Customer/Chef/Cashier/Admin ── authenticated API/hub ──┘
                              │ private HTTP network
                    Catalog / Orders / Customers / Identity / Billing
                              │ own DB role only
                    Supabase PostgreSQL: 5 owned schemas
                    Managed RabbitMQ: AMQPS publishers
                    Persistent owning-service workers: HTTP outbox + reconciliation
                    Object storage/CDN + durable session/keys + central logs
```

Lý do source-backed: hai HTTP consumers, payment settlement retry, table scheduler và Gateway in-memory session/SignalR đều dựa lifetime process; Option A giữ mô hình đó trong khi giải quyết PG/provider/media/config. Managed Rabbit là bước infrastructure riêng, không thay execution model. REST/Gateway/API contract giữ nguyên; Android không trực tiếp Supabase, Rabbit hoặc private API.

Nếu mục tiêu trước mắt chỉ là bỏ laptop nhanh nhất, giữ SQL Server trên persistent cloud host/managed SQL Server sẽ ít DB rewrite hơn, nhưng **đó không phải target Supabase PG đang yêu cầu**, nên chỉ là phương án staging/rollback cần quyết định riêng, không âm thầm đổi recommendation. SQL Server local và volumes hiện tại tiếp tục giữ nguyên cho baseline/rollback; không migrate lúc audit.

Hướng dẫn Vercel Services đã được dùng để định hướng đánh giá; khi khác với guide beta cũ, báo cáo ưu tiên tài liệu official hiện hành về Docker, bindings, request path và WebSocket. Không có cloud runtime nghiệm thu nào được suy ra chỉ từ tài liệu platform.

## Migration order

1. **Phase 1 — Decision/config/data manifest:** chọn host/region/domain/plan, public routing/site cookie strategy, exact table/schema/timezone mapping, reference vs history import manifest, secret inventory, preview isolation, backup/rollback plan. Không chạm local demo baseline.
2. **Phase 2 — PostgreSQL isolated conversion:** năm provider/contexts/baselines/view/partial indexes/rowversion/locks/error mapping; test migrations trên DB riêng, service-role isolation, idempotency/concurrency/stock/settlement. Chưa cutover Android.
3. **Phase 3 — Data rehearsal:** approved data copy trong môi trường riêng theo owner, preserve IDs/sequence/hash/snapshots/timestamps/split/receipt; compare totals/history/images; drain/quarantine pending events và active sessions. Không import credential plaintext hoặc replay paid events.
4. **Phase 4 — Persistent cloud runtime:** private six services, TLS/WSS Gateway, AMQPS publishers, workers, media/session/keys/logs/health probes; block internal routes/downstream exposure. Idle-worker/reconciliation failures và restart recovery acceptance.
5. **Phase 5 — Web/Android cutover acceptance:** Vercel four webs/routing/origin/cookies, build HTTPS Android origin, Google audience/release SHA, Samsung không adb reverse; full cross-role order/service/payment/split/history/timezone/images/security.
6. **Phase 6 — Controlled launch:** cutover window, monitored read/write/worker backlog, backups, rollback drill, plan quotas/uptime. Cloud PASS chỉ sau E2E và no-laptop acceptance.
7. **Phase 7 — Optional Vercel-first POC:** HTTP containers/private bindings + bounded jobs/Queues + SignalR scaling/affinity-duration tests, không gộp với initial PG migration.

## Risk register

| ID | Severity | Actual risk / evidence | Release gate |
|---|---|---|---|
| R01 | BLOCKER | Provider + T-SQL bootstrappers/runtime incompatible PG | Five PG bootstraps/migrations ready; startup không SQL Server syntax |
| R02 | BLOCKER | Quote/loyalty/order lock parity, rowversion/index uniqueness | Concurrent double submit/confirm/split/stock tests PASS, no duplicate invoice |
| R03 | BLOCKER for Option B | Four BackgroundService execution models | No-traffic pending work still completes; no hidden keepalive dependency |
| R04 | HIGH | Gateway sessions RAM + local DataProtection | Login survives replica routing/redeploy as designed; roles/branch preserved |
| R05 | HIGH | Direct private APIs trust Gateway commands/IDs | No public downstream mutation; authenticated/private service graph |
| R06 | HIGH | Web hub unauthenticated table/branch subscribe | Cloud authorization/payload review and unauthorized-subscription tests |
| R07 | HIGH | Hub connection/groups per process; Vercel affinity/max-duration | Multi-instance send/reconnect/resync POC; REST fallback canonical |
| R08 | HIGH | Local mutable image writes; volume target path mismatch | Upload survives redeploy; assets paths/case/cdn and all approved images PASS |
| R09 | HIGH | Mixed local/UTC time + PG timestamp semantics | History/reservation/batch dates Việt Nam đúng, no double timezone shift |
| R10 | HIGH | Case/identifier/sequence/import consistency | Identity lookup/uniqueness and cross-domain reference integrity PASS |
| R11 | HIGH | Rabbit optional send + no confirms/bindings + JSONL before send | Managed TLS/connect/publish/failed-write behavior verified; DB outbox preserved |
| R12 | HIGH | Paid invoice reconciliation worker required after external failure | SUCCEEDED → final invoice/session/table/loyalty converges after interruption |
| R13 | HIGH | Production relative API/hub paths and cookie origins | Four web auth/deep-link/API/media/realtime acceptance, no wildcard CORS |
| R14 | MEDIUM/HIGH | Autoscale PG pools/free quota/IPv4 vs direct IPv6 | Five roles reachable + bounded connection budgets and selected uptime plan |
| R15 | HIGH | Pending outbox/old tokens/test fixtures carried blindly | Approved data manifest, drain/dedupe/security relogin, no duplicate business action |
| R16 | MEDIUM | Startup repairs/seeds in every replica/cold start | Deploy-controlled migration/seed, runtime DML least privilege |
| R17 | MEDIUM | Proxy trust fixed IP / rate limiter remote IP/process-local | Correct forwarded host/proto/IP, security limits in cloud topology |
| R18 | MEDIUM | Vercel .NET port/build context/resource/runtime limits | Dockerfile.vercel POC actual deploy, no assumed rename-only compatibility |

### Files inspected / evidence scope

- Solution/csproj: `SelfRestaurant.Microservices.sln`, six API csproj, Shared csproj; 6 Program.cs, Dockerfiles, appsettings key inventory, launchSettings.
- `AGENTS.md`, mobile contract ownership/transport, frontend integration docs; ownership audit/cleanup documents, compared to current source rather than reused as live cloud proof.
- Five `Persistence/Context/*DbContext.cs`; bootstrap entrypoints and SQL constructs; all 8 EF migration files; IngredientBatches entity/concurrency mapping; active raw SQL/lock/error paths in Orders/Identity/Catalog/AdminCatalog/Cashier/PaymentFinalization.
- SQL owned-table/view/provisioning/retirement constructs, `sql/sqlchuan.cleaned.sql` schema object search, `docker/db-init.sh`, `docker-compose.yml`, prod overrides, `deploy/Caddyfile`; no scripts executed for migration and no secret/identity row dump.
- Both event publishers, both HTTP consumers/event clients, Catalog scheduler, Billing reconciliation worker; Gateway two hubs and notification controller call sites.
- Four `package.json`, `vite.config.ts`, `src/lib/api.ts` integration patterns, Cashier Dashboard hub usage, Admin QR-origin/upload flow; no cloud frontend deployed.
- Android `app/build.gradle`, manifest/main/debug network configs, ApiClient, SignalRManager reconnect/token/resync, MobileRepository image mapping, ImageLoader, Google config/call sites; local connection/demo/test script endpoint search.
- Official current Vercel Docker/.NET/Services/workers/WebSocket/Queues/Hobby, Supabase provider connectivity/roles/schema/pricing, Npgsql provider/timestamp/concurrency and CloudAMQP .NET/TLS/plans references linked beside relevant findings.

Verification performed: source searches/reads, `git status/log/diff names`, `docker ps`, six read-only readyz requests, current primary documentation lookup. **Không chạy compile/full regression suites trong audit không sửa source**; không claim PostgreSQL/cloud tests PASS. Row-by-row production data inventory, cloud costs/account eligibility and actual provider deployment limits remain future validation.

Git outcome: baseline HEAD giữ `40a0f59`; source unchanged; report mới untracked; `.idea/misc.xml` unstaged và `.idea/vcs.xml` staged từ trước vẫn nguyên. **NO commit. NO push.**

---

## Phase 1 implementation — 05/10/2026

Target chốt theo yêu cầu mới: webs Vercel, APIs/Gateway và workers persistent Railway, Supabase PostgreSQL năm owned schemas, managed RabbitMQ; Android chỉ HTTPS Gateway. Không deploy/cutover/provision cloud trong lượt này.

### Thay đổi

- Dual provider năm DbContext; default SqlServer. Npgsql/EF 8.0.11, named connection riêng, quoted original mappings, migrations history từng schema.
- PostgreSQL baselines/snapshots mới; Identity local CustomerLoyalty view + citext migration. SQL Server bootstrap/historical artifacts được giữ, không chạy PG migrations vào local SQL Server.
- UTC timestamptz/value converter reject Unspecified; numeric money/stock, partial unique indexes và PG concurrency token thay SQL rowversion riêng provider.
- Reference/opt-in Demo seeds tách khỏi test/transactional data; không seed password/account. Một branch/table/menu/dish/recipe demo nhỏ, không copy 52 món hoặc QA fixtures sang cloud.
- Atomic EF inventory updates; Orders request-time schema repair bỏ qua trên PG; transaction advisory locks thay sp_getapplock trên PG, SQL path giữ nguyên.
- Reproduced reservation commit SQLSTATE 40001 → giữ SERIALIZABLE và trả 409, không rollback lần hai transaction đã completed. PG concurrency filter chỉ ánh xạ serialization/deadlock và exact snapshot PK races; không nuốt mọi DB error.
- Reproduced cold snapshot race bị biến thành dish_unavailable; propagate conflict để client resync/retry idempotent. Competing order/walk-in/payments được test.
- Gateway media abstraction với Local implementation, safe keys/staged same-key replacement; controllers không giữ duplicate filesystem upload helpers. Object storage adapter chưa có.
- JSONL diagnostic optional; production stdout metadata, DB outbox/audits retained. Rabbit URI amqp/amqps supported; không tắt certificate validation. Bốn domain workers vẫn persistent và reconciliation không bị disable.
- PORT binding sáu API; Gateway readyz kiểm tra năm downstream bounded/parallel; mỗi domain readyz đọc DB connectivity.

### Verification và phạm vi bằng chứng

| Check | Kết quả / giới hạn |
|---|---|
| Empty PostgreSQL 16 → migrations | PASS: 5 schemas, 60 domain tables + 5 history tables, local Identity view; migrate twice idempotent; baseline + citext applied from empty |
| Reference/Demo → API startup | PASS: 6 isolated APIs, Gateway readyz + 5 DB readiness; no public test ports |
| Contract tests | PASS; latest count in final execution below (full existing suite + media/Rabbit/readiness regression) |
| PostgreSQL backend workflow | PASS in isolated harness: auth/uppercase login/refresh, profile/reservation list, branch/menu, concurrent check-in/order, 2 rounds, kitchen/received state, service create/cancel, quote/ONSITE/payment retry/concurrent finalize, one invoice, CLOSED |
| Reservation race | PASS: one created reservation, one HTTP 409; exactly one persisted row |
| Money/time/locks | PASS: exact decimal, UTC roundtrip/reject ambiguous Kind, advisory lock release/serialization; no +7 |
| SQL Server current compiled EF runtime | PASS 7/7: all five owned models materialize real existing records; stock decrement rolled back with original stock restored; payment transaction lock retained. No migrations/seed/reset/delete |
| Existing SQL Server B1/B2 runtime scripts | PASS both; running local demo images unchanged. Three generated test customer fixtures retained; no test session/payment/reservation or user data cleanup from these scripts |
| SQL Server full Samsung/Chef/Cashier E2E | Not rerun in this database phase; earlier UX acceptance not presented as new cloud proof |
| Live managed AMQPS / Google / VNPAY | Not run; URI configuration/offline existing contracts only |
| Supabase least privilege / real import | Not run; test superuser is not production schema grants evidence |

Test runner: `SelfRestaurant-master/scripts/test-postgres-compatibility.ps1`. Builds before launching processes; unique labelled SDK/database/network; empty PG tmpfs, unpublished trust only for this test network; generated credentials not stored in Git. Default cleanup removes only its own labelled disposable test resources. No SQL Server volume reset, no `.env` change. Early failed disposable runs retained for diagnosis; no test garbage was exported/imported into Supabase.

### Remaining cutover blockers (outside phase 1)

Object storage cloud adapter and approved image URL/path migration; persistent distributed Gateway web session/DataProtection keys and scale/backplane plan; per-schema roles/grants, Supabase extension/search_path/pool/TLS verification; real data freeze/export/import/sequence/reconciliation; managed Rabbit live TLS acceptance; Railway/Vercel deployment/routing. See CLOUD_SESSION_STATE.md, CLOUD_DATA_MANIFEST.md and POSTGRES_MIGRATION.md.

### Final acceptance

**POSTGRES COMPATIBILITY: PASS** — phase 1 local compatibility only, not production/cloud E2E.

Final clean isolated run: solution build 0 warnings/errors, contract **128/128**, PostgreSQL **21/21**, including empty baseline migration sub-run **7/7**. Script exits 0 and label-checked cleanup succeeds on Windows PowerShell 5. SQL current-source regression **7/7**, existing local B1/B2 both PASS; local readyz **6/6** after tests.

Reviewed generated owner/schema mappings, source diff and staged secret/artifact scan (0 credential literal hits); diff --check PASS. No `.env`, dumps, credentials, JSONL/logs/screenshots or `.idea` included in the compatibility commit. Earlier test-only containers/databases were retained during diagnosis then removed after acceptance; SQL Server/Rabbit/demo service containers and volumes remain untouched. Three B1/B2 customer fixtures remain in local SQL Server; no cleanup/reset of user data.

Commit message: `feat: add PostgreSQL cloud database support` on `cloud/postgres-migration`. No amend, no push. Two pre-existing `.idea` changes remain outside commit, including the originally staged vcs.xml.
