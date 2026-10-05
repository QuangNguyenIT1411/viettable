# Cloud staging — Phase 2

> Deployment target đã đổi sang Vercel + Supabase; phần Railway/Redis/broker bên dưới là preparation phương án cũ, không được tiếp tục deploy. Xem [VERCEL_SUPABASE_ARCHITECTURE.md](VERCEL_SUPABASE_ARCHITECTURE.md). Lượt đổi kiến trúc chỉ planning.

> **SUPABASE STAGING: PASS** — [SUPABASE_ACCEPTANCE.md](SUPABASE_ACCEPTANCE.md) là authoritative evidence: TLS VerifyFull; five migrations/history + physical parity; canonical bootstrap/idempotency (3branches/47tables/10categories/52dishes); five runtime-role logins và20cross-schema denials; two full live API/concurrency flows trên transaction pooler; Storage upload/read/persistence/anonymous-write denial/cleanup; contract141/141 real Redis local. Các đoạn readiness bên dưới là lịch sử preparation, không phải kết quả hiện tại. Không deploy Vercel/Railway; không đổi Android URL.

Ngày kiểm tra: 05/10/2026. Branch `cloud/postgres-migration`, base `d292288`.

**CLOUD PREPARATION: SUPABASE PASS; VERCEL NOT DEPLOYED.** Không deploy Railway compute/Redis/workers; không đổi Android URL hoặc SQL Server configuration. Target Vercel + Supabase vẫn cần CloudHost/auth/worker/realtime acceptance riêng.

## Historical preparation notes — superseded

Phần dưới lưu baseline phương án trước; các dòng chưa nghiệm thu không thay thế kết quả live trong SUPABASE_ACCEPTANCE.md. Redis/Railway là compatibility inventory, không phải cloud provisioning requirement hiện tại.

## Tài nguyên đã xác minh

| Provider | Resource | Trạng thái |
|---|---|---|
| Supabase | `viettable-staging`, ref `waxzqkuwuwubdkrzxbqu` | Project tồn tại, Free/Nano; region `ap-southeast-2` (Sydney) được user chấp nhận |
| Railway | `viettable-staging`, project `92e01207-9c6b-495b-b53f-8d7ae93f8b4a` | Project rỗng đã tạo; chưa deploy service |
| Railway environment | `staging`, ID `da5d61ba-8b2c-4873-807c-51543eedae7d` | Đổi tên environment mặc định do platform tạo từ `production` sang `staging`, trước mọi deploy |
| Railway plan | Trial | Dashboard hiển thị 30 ngày hoặc $5 credit còn lại; không upgrade/billing |

Database credential đã lưu DPAPI ngoài repository. Direct probe lỗi network; session pooler lỗi TLS ban đầu đã được xử lý bằng public provider CA. Retry VerifyFull PASS, migrations/history của cả năm owner hoàn tất và post-probe xác minh schema. Không in credential/connection string.

## Connection strategy

Dashboard Supabase cung cấp Direct connection (IPv6 mặc định), Session pooler (IPv4), Transaction pooler. Chưa chọn connection cuối cùng trước network probe của runtime/migration runner.

- Persistent ASP.NET/Npgsql: ưu tiên Direct nếu runtime có IPv6 hoạt động; dùng Session pooler nếu cần IPv4, không mua IPv4 add-on tự động.
- Migration runner: Direct nếu IPv6 hoạt động, hoặc Session pooler IPv4; không dùng Transaction pooler mặc định.
- Dùng TLS có xác minh certificate; không tắt certificate validation.
- Giới hạn application connection pool theo Nano connection budget thực tế cho tổng năm service và migration job; chưa nghiệm thu tải.

Tham chiếu: [Supabase connection modes](https://supabase.com/docs/guides/database/connecting-to-postgres), [pooling and limits](https://supabase.com/docs/guides/database/connecting-to-postgres/pooling-and-limits).

## Kiến trúc cần deploy — chưa chạy

Internet → HTTPS Gateway → private Catalog/Orders/Customers/Identity/Billing. Gateway không nhận DB credential. Không tạo public domain cho năm API nội bộ.

| Owner | Schema / history | Runtime connection configuration name |
|---|---|---|
| Catalog | `catalog.__EFMigrationsHistory` | `ConnectionStrings__CatalogDb` |
| Orders | `orders.__EFMigrationsHistory` | `ConnectionStrings__OrdersDb` |
| Customers | `customers.__EFMigrationsHistory` | `ConnectionStrings__CustomersDb` |
| Identity | `identity.__EFMigrationsHistory` | `ConnectionStrings__IdentityDb` |
| Billing | `billing.__EFMigrationsHistory` | `ConnectionStrings__BillingDb` |

Migration credential tách runtime credential. Proposed runtime roles `viettable_catalog`, `viettable_orders`, `viettable_customers`, `viettable_identity`, `viettable_billing`: chỉ quyền DML/sequence/schema owned; không cross-schema grant. Chưa tạo roles hoặc apply migration trên cloud. Bootstrap theo `CLOUD_DATA_MANIFEST.md`, không copy QA transactional data từ local.

Actual URL configuration: `Services__Catalog`, `Services__Orders`, `Services__Customers`, `Services__Identity`, `Services__Billing`; giá trị lấy từ Railway private networking, không bake vào image. `PORT` đã được hỗ trợ trong Phase 1. Mỗi API có `/healthz` và `/readyz`; chưa có URL runtime cloud.

Các hosted workers giữ lifetime của owning process: Catalog table release, Customers ready consumer, Orders payment-completed consumer, Billing payment reconciliation. HTTP outbox polling không được mô tả thành RabbitMQ consumer.

## Session / DataProtection / storage / broker

- PREPARED: `PersistentSessionConfiguration` cung cấp Redis distributed cache và shared DataProtection repository. Staging không cho fallback RAM; local Compose vẫn giữ behavior cũ.
- RUNTIME VERIFIED (local test host): hai instance dùng Redis thật; cookie session và DataProtection sống qua dispose/recreate ASP.NET host. Chưa chứng minh staff login của full Gateway qua OS/container restart; không đánh đồng với acceptance đó.
- PREPARED: `SupabaseDishMediaStorage` upload bằng backend credential, không redirect credential, key ảnh immutable, lỗi không chứa response/secret. Local storage vẫn là default cho local demo. Chưa có live upload/read test.
- DEPLOYED (Supabase resource only): bucket `viettable-staging-media`, PUBLIC read, 5 MB, MIME JPEG/PNG/WebP, 0 policies tại thời điểm kiểm tra. Không tạo anonymous-write policy. Bucket chỉ dành cho ảnh công khai, không chứa tài liệu riêng tư.
- Managed RabbitMQ chưa provision và không provision paid broker. `RabbitMq__Uri` hỗ trợ AMQPS nhưng chưa có TLS live evidence.
- Staging phải đặt `IntegrationEvents__WriteDiagnosticFiles=false`; outbox/inbox/business audits vẫn giữ.
- Signing key/config credentials chỉ qua secure platform environment. Không ghi giá trị secret vào tài liệu.

## Acceptance evidence

| Hạng mục | Result |
|---|---|
| Supabase project / region | VERIFIED trên dashboard; SQL acceptance chưa chạy |
| Railway project / staging environment | VERIFIED; chưa deploy |
| Empty DB migrations / 5 schemas / bootstrap | NOT RUN |
| Runtime least privilege | NOT RUN |
| Distributed Session / DataProtection host replacement | PASS local Redis thật, concurrent hosts; full Gateway login/container restart chưa chạy |
| Supabase Storage adapter / bucket | Source build + adapter tests PASS; bucket created; cloud upload/read/persistence NOT RUN |
| Managed AMQPS / reconnect | NOT RUN |
| Six cloud APIs health / ready | NOT RUN |
| Four persistent workers / restart recovery | NOT RUN |
| Public HTTPS API E2E / SignalR WSS | NOT RUN |

## Cấu hình chuẩn bị — không deploy

Gateway staging cần `SessionStore__Provider=Redis`, `SessionStore__RedisConfiguration` (secret), `SessionStore__ApplicationName` (stable namespace riêng staging). Redis cần private access/ACL, persistence AOF và volume bền vững; môi trường ngoài private network cần TLS. Cookie staging bật Secure/HttpOnly/SameSite Lax; HTTPS và trusted proxy phải được kiểm tra khi deploy thật.

DataProtection key ring nằm trong Redis, không trong writable container filesystem. Hiện repository này chưa mã hóa key XML at-rest bằng certificate; cần bảo vệ Redis và quyết định certificate/encryption trước production. Không dùng cache eviction làm mất key/session.

Media staging cần `MediaStorage__Provider=Supabase`, `MediaStorage__Supabase__ProjectUrl`, `MediaStorage__Supabase__Bucket`, `MediaStorage__Supabase__ServiceKey` (backend secret; không Android). Ảnh cũ chưa tự xóa trước Catalog commit để tránh mất ảnh nếu transaction thất bại; retention/garbage collection riêng chưa triển khai.

Sáu config ở `SelfRestaurant-master/deploy/railway/*.toml` đã PREPARED: Dockerfile theo owner, readiness `/readyz`, restart ON_FAILURE. Build root là `SelfRestaurant-master`; chọn config tương ứng service. Không có deployment nào từ những config này. Runtime URLs dùng `Services__*` private DNS; không localhost/public microservice. PORT binding hiện có là `0.0.0.0`.

Broker procedure khi được phép triển khai: chọn managed AMQPS, tạo vhost/credential riêng staging qua provider, đặt `RabbitMq__Uri` bằng secret manager, xác minh certificate/hostname và reconnect worker. Không tắt TLS validation; chưa nghiệm thu managed broker. Hosted workers nằm trong owning API, không tạo service worker riêng. Tắt diagnostic filesystem bằng `IntegrationEvents__WriteDiagnosticFiles=false`.

## Kiểm thử đã chạy

- `scripts/test-cloud-session.ps1`: **141/141 PASS**, skipped 0 với Redis disposable thật. Redis/network/container test đã dọn, không đụng local database volume. Bao gồm adapter contract và Session/DataProtection host replacement; mock HTTP chỉ dùng trong unit test Storage, không phải cloud acceptance.
- Cloud preparation runner build: **PASS**, 0 warnings/errors. Runner chỉ hỗ trợ Probe/Migrate; bootstrap/least-privilege/cloud workflow acceptance chưa hoàn tất.
- Chưa chạy cloud PostgreSQL contracts: probe chưa vượt TLS; không reuse disposable PostgreSQL suite để giả lập kết quả Supabase.

## Gates còn lại

Railway deploy hiện bị user cấm; chỉ lên kế hoạch gần ngày báo cáo theo `CLOUD_COST_PLAN.md`. Không upgrade, push, connect remote repo hoặc tạo compute để thử.

DatabasePassword đã có trong kho DPAPI local, mã hóa theo Windows user ngoài repository. `scripts/test-staging-database.ps1` truyền credential qua stdin tới runner, VerifyFull TLS và CA mount read-only; Probe đã thử nhưng chưa thành công trước khi bổ sung CA. Migrate phải chọn rõ staging ProjectRef/host/user. Migration dùng session pooler port5432 hoặc direct phù hợp; runtime autoscaling target mới đánh giá transaction pooler theo tài liệu kiến trúc. Không gửi password/chat/argv.

Còn phải làm: live migrations/5 histories, reference bootstrap, owner runtime roles/grants/cross-schema denial, cloud database workflow tests, live Storage upload/read/restart persistence, full Gateway restart/login verification. Khi cần Storage service key, user nhập bằng helper cùng cơ chế; không đọc/in key từ dashboard. Chưa commit vì cloud preparation chưa hoàn chỉnh.
