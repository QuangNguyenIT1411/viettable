# Supabase staging acceptance

Ngày kiểm tra: 05/10/2026. Branch `cloud/postgres-migration`, base `d292288`.

**SUPABASE STAGING: PASS** cho DB + Storage acceptance bên dưới; không phải Vercel runtime/cutover acceptance. Không dùng local PostgreSQL/disposable tests thay cloud acceptance.

## Safety / resources

- Project `viettable-staging`, ref `waxzqkuwuwubdkrzxbqu`, Sydney `ap-southeast-2`.
- DatabasePassword có trong Windows current-user DPAPI ngoài repository. Loader chỉ truyền secret trong stdin tới runner; không plaintext file/argv/report.
- TLS `VerifyFull` với public CA của Supabase: **PASS live**.
- Không reset/drop database, không tác động Supabase system schemas hoặc SQL Server local; không deploy Vercel/Railway.
- `.idea/misc.xml` và staged `.idea/vcs.xml` giữ nguyên.

## Connection modes

Migration: **Session pooler, 5432**. Direct probe trước đó lỗi network; session pooler đã kết nối thành công sau provider CA configuration. Migrations không chạy qua transaction pooler.

Future Vercel runtime: **Transaction pooler, 6543 — PASS live compatibility** với client Pooling=true, MaxPoolSize2/owner, MinPoolSize0, MaxAutoPrepare0, Multiplexing=false, NoResetOnClose=true. EF SaveChanges/rollback, exact numeric35000.25, UTC microsecond round trip, transaction advisory-lock contention và connection reuse đã chạy thật. Source runtime không dùng prepared statements/session search_path/temp tables/LISTEN/session advisory lock; locks dùng SET LOCAL + transaction-scoped advisory lock. Migration job riêng mới dùng session search_path.

Controlled diagnosis: reset-on-close mặc định gặp timeout trước advisory lock; tắt client pooling qua lock nhưng menu API503. Bật pooling và tắt client reset theo transaction-pooler compatibility cho full workflow PASS. [Npgsql compatibility](https://www.npgsql.org/doc/compatibility.html) mô tả reset logic không phù hợp transaction pooling. Đây là cấu hình riêng runtime pooler, không áp dụng chung direct/session connection. Fixed owner credentials tách client pools; không dùng dynamic SET ROLE, session variables hoặc session-state security. Không cho bật NoReset như workaround tùy tiện nếu sau này thêm session state; cần regression lại.

## Pre-migration inventory

Live probe trước migrations:

| Schema | Exists | Base tables |
|---|---|---:|
| catalog | false | 0 |
| orders | false | 0 |
| customers | false | 0 |
| identity | false | 0 |
| billing | false | 0 |

Không cleanup dữ liệu vì không có owner schemas cũ.

## Migrations

Runner gọi EF PostgreSQL migrations trên năm owner DbContexts, mỗi owner history `__EFMigrationsHistory` riêng. Verify applied count bằng source migration count và không pending migrations. Không import SQL Server dump.

**PASS live:** job hoàn tất exit0, mỗi owner không pending migrations và history count đúng source. Post-migration probe xác minh năm schema tồn tại:

| Owner | Applied migrations | Base tables (bao gồm history) |
|---|---:|---:|
| Catalog | 1 | 16 |
| Orders | 1 | 17 |
| Customers | 1 | 9 |
| Identity | 2 | 11 |
| Billing | 1 | 12 |

**PASS live physical verification:** relational model được đối chiếu column types/nullability và named PK/FK/unique constraints/valid indexes trong PostgreSQL. Các typmod numeric precision, UUID và timestamp mappings nằm trong column parity check. Catalog15tables/140columns; Orders16/165; Customers8/99; Identity10/91; Billing11/159. Đây không phải chỉ table-count check.

## Bootstrap

**PASS live canonical bootstrap + second run.** Owner export job chỉ đọc local `RESTAURANT_CATALOG` reference graph; credential cấu hình được truyền trong memory/stdin, không ghi dump. Import 12 reference tables theo thứ tự FK: Restaurants, TableStatus, Branches, Categories, Units, Ingredients, Dishes, DiningTables, Menus, MenuCategory, CategoryDish, DishIngredients. Không import batches/stock movements/audit hoặc identity/transaction history. Required Orders statuses và Identity roles seed fresh theo convention hiện có, không account/password seed.

Branches **3**, Tables **47**, Categories **10**, MenuItems **52**. First run inserted649 Catalog reference rows; second run inserted0 và counts không đổi. Preserve IDs/QR/recipe/stock configuration; reset identity sequences theo max imported IDs. Refuse QA TEMP names/canonical inventory mismatch; không overwrite existing rows. Fresh staging table occupancy AVAILABLE (active tables), CurrentOrderID null vì không migrate local active sessions.

Review đã phát hiện conversion zone bootstrap ban đầu không đúng: source Catalog/SQL containers UTC+0000 và current contract xử lý offsetless timestamps như UTC, không phải device timezone. Runner đã sửa source-zone UTC, kiểm tra SQL SYSDATETIMEOFFSET và Catalog container clock. CorrectReferenceTime chỉ CAS reference fields còn đúng old-import value; corrected819 fields, second run corrected0, preserve các fields đã đổi qua API. Không import hoặc chỉnh transactional history timestamps; không hardcode +7. Đây không phải approval để migrate toàn bộ lịch sử SQL Server có nguồn timezone chưa rõ.

## Runtime role isolation

**PASS live:** `viettable_catalog`, `viettable_orders`, `viettable_customers`, `viettable_identity`, `viettable_billing`. Generated secrets được lưu DPAPI ngoài repository, không in. Login qua transaction pooler6543 bằng từng role thật; owned-schema SELECT thành công; mỗi role đọc bốn schema khác bị SQLSTATE42501 (**20/20 denied**). Roles không có superuser/createDB/createRole/replication/bypassRLS hoặc membership; owned schema USAGE/DML/sequence usage, migration history read-only. Không claim đây là complete security-definer-function audit. Gateway không cấp DB role/credential.

## Cloud DB contracts / concurrency

**PASS live** bằng sáu local processes trong container không publish port, five owner role credentials trên pooler6543; Gateway không DB credential. No Demo seed, migrations auto-off, Rabbit disabled, diagnostic JSONL disabled. Application business logic/Session/workers không refactor.

- Identity: legitimate Register, password Login case-insensitive, Refresh; customer JWT sử dụng profile endpoint thành công.
- Catalog: branches, physical tables, menu với categories/dishes thật từ canonical bootstrap.
- Customers: authenticated profile; hai reservation requests cùng table/slot, khác idempotency keys → một success và một409, không500; detail, cancel qua API, history read.
- Orders: concurrent same-key walk-in, concurrent order submit/replay, hai order rounds riêng, Chef start/ready, Customer confirm received, WATER create/cancel.
- Billing: checkout total bằng hai unit prices Catalog; ONSITE payment creation và same-key replay; concurrent **domain** ConfirmAsync bằng hai BillingDbContexts/runtime role, real Orders/Identity downstream APIs; SUCCEEDED, đúng một invoice với tổng chính xác, session CLOSED và table available.
- ConfirmAsync domain test không phải Cashier UI/auth acceptance; không claim VNPAY external, Android, SignalR hoặc frontend E2E ở phase này.

Critical concurrency: reservation/table allocation, check-in, submit order, payment finalization **PASS trên Supabase**, không500 trong các requests test. Không phải load/stress benchmark hoặc Vercel cold-start acceptance.

Remaining staging test records sau hai full flow PASS: customer IDs1 (partial diagnostic run),2,3; reservations1,3 CANCELLED; sessions1,2 CLOSED, branch3/table36 released; payments1,2 SUCCEEDED, một invoice/session và hai rounds/session. Không import local QA accounts/history. Live QA entities được tạo qua legitimate APIs; financial/history được giữ đúng business behavior, không SQL DELETE. Password/tokens generated chỉ memory, không report/commit. Round-trip quote test rollback, chỉ sequence gaps có thể còn. Refresh-token/account receipts của test identities còn trong Identity; không SQL-delete/revoke tùy tiện ngoài supported auth flow.

## Storage

Bucket `viettable-staging-media`: public read live PASS; no anonymous write PASS bằng actual anon-role INSERT denial và policy audit, không dựa riêng HTTP thiếu apikey.

StorageServiceKey đã có DPAPI. **PASS live:** upload PNG tạm qua `IDishMediaStorage`/`SupabaseDishMediaStorage`, public read bytes khớp; process/container uploader exit; fresh verifier đọc cùng object thành công; xóa đúng object qua authenticated Storage API. Receipt public path nằm ngoài repository. Không bật anonymous write. Unauthenticated HTTP upload nhận400 (không phải anon-key RLS test); bổ sung actual `SET LOCAL ROLE anon` INSERT vào storage.objects bị42501, luôn rollback; no permissive INSERT/ALL policies cho anon/PUBLIC trong policy audit. Không dùng fake HTTP response cho live acceptance.

Adapter/config switch `MediaStorage:Provider=Supabase` versus Local giữ nguyên, không persistent cloud local-file dependency. Live acceptance gọi production abstraction adapter; object persistence được kiểm tra sau uploader container exit, không claim full Gateway staff restart. Authenticated temporary-object cleanup PASS.

## Session baseline / tests

- **Rerun lượt này: backend contract suite141/141 PASS, skipped0**, real isolated local Redis; có host replacement/concurrent hosts/DataProtection/environment separation. Không phải full Gateway staff login/container restart acceptance; không provision Redis cloud, không refactor Session.
- Lượt này: runner build PASS, 0 warnings/errors sau restore. Attempt `--no-restore` trong fresh SDK container fail vì NuGet packages không có; rerun normal build thành công.
- Lượt này: live TLS probe + pre-migration five-schema inventory PASS.
- Full Gateway restart: NOT RUN; không refactor Session/workers trong lượt này.

## Repro commands / security review

`scripts/test-staging-database.ps1` actions Probe/Migrate/Verify/Bootstrap/Roles/Workflow/StorageUpload/StorageVerify dùng explicit staging ProjectRef/Host/User, secrets từ DPAPI/stdin. Workflow default transaction-pooler compatibility on; diagnostic opt-out phải chọn rõ, không dùng làm production default. CorrectReferenceTime là scoped repair riêng cho import timezone cũ, không generic data repair.

`scripts/test-staging-secret-safety.ps1`: actual DPAPI values7 checked, no actual secret/key/JWT match trong candidate files; `.idea` excluded. `git diff --check` PASS. No dumps/screenshots/test logs/DPAPI artifacts trong repository. Final commit chỉ sau live acceptance + review, không push.

## Next gate

Credential gate đã giải quyết; DB/Storage evidence phía trên PASS. Vercel CloudHost/workers/session auth refactor/SignalR multi-instance POC vẫn là task tương lai, không blocker cho Supabase DB/Storage acceptance. Chưa migrate media library52ảnh hoặc provision staff demo accounts; không claim đã hoàn tất cloud demo deployment. Local SQL Server demo không cutover.
