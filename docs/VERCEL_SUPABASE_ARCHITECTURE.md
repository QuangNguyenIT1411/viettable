# VietTable — Vercel + Supabase architecture decision

Ngày: 05/10/2026. Base: `cloud/postgres-migration`, `d292288`.

## Decision / evidence status

**Recommendation: YES — chọn Vercel + Supabase làm target, chưa nghiệm thu khả năng triển khai.** Không deploy hoặc refactor trong lượt này. Không provision Railway compute, Redis hoặc dùng Trial credit.

Vercel hiện có Container Images và WebSockets Beta, nên “ASP.NET/SignalR không được hỗ trợ trên Vercel” không còn là kết luận đúng. Tuy nhiên hỗ trợ nền tảng không chứng minh hub VietTable, multi-instance fan-out hoặc recovery đã hoạt động. Bốn gate bắt buộc: consolidated host không đổi behavior; cookie auth không phụ thuộc RAM; durable event processing; SignalR cross-instance POC.

Nguồn hiện hành: [ASP.NET container guide](https://vercel.com/kb/guide/dot-net-asp-net-on-vercel-with-docker), [Container Images](https://vercel.com/docs/functions/container-images), [WebSockets](https://vercel.com/docs/functions/websockets). Một số bundled skill dùng configuration/limits cũ; khi khác nhau phải dùng tài liệu chính thức hiện hành và xác minh bằng POC, không copy cấu hình cũ.

## Client Scope

- Customer Android: giữ API `/api/mobile/v1`, JWT, REST canonical và SignalR notification.
- Chef Web, Cashier Web, Admin/Manager Web: giữ UI, business authorization và branch scope hiện có. Manager chỉ sử dụng chức năng được policy hiện tại cho phép.
- Customer Web: **NOT DEPLOYED**; không xóa source/local functionality.
- Client không nhận database password, runtime DB credentials hoặc Supabase service-role key.

## Components removed from deployment

Target không có Customer Web, Railway, Railway Redis hoặc managed RabbitMQ. Không xóa implementation local tương ứng. Chỉ bỏ broker/worker loop khỏi cloud sau khi replacement được nghiệm thu; không disable để build/deploy pass.

Các file Railway preparation trong working tree là phương án cũ, không phải deployment authorization.

## Backend consolidation plan

Hiện có sáu ASP.NET entry points: Gateway, Catalog, Orders, Customers, Identity, Billing. Gateway gọi năm typed HTTP clients; Gateway không sở hữu business database. Mỗi service có composition root và DbContext riêng.

| Option | Deployment | Trade-off |
|---|---|---|
| 1 | Sáu Vercel backend services | Ít thay đổi transport, nhưng nhiều cold starts, credentials/config, network hops; hosted workers vẫn phải thay |
| 2 — recommended | Một ASP.NET CloudHost container với năm owner modules và public Gateway facade | Đơn giản vận hành; cần refactor composition/transport và regression có kiểm soát |

Tạo composition root CloudHost riêng; giữ .NET 8 trước, không upgrade toàn bộ chỉ vì ví dụ provider dùng phiên bản mới. Extract registration/application handlers để tái sử dụng, không khởi chạy sáu process bằng supervisor, không gom mọi source file hoặc controller tự động.

Giữ năm DbContexts, migrations/history và schema `catalog`, `orders`, `customers`, `identity`, `billing`. Module chỉ truy cập schema của mình bằng runtime role riêng. Không tạo DbContext tổng, cross-schema join/FK hoặc transaction tổng xuyên domain. Identity vẫn sở hữu identity/loyalty; Customers không trở thành owner của loyalty.

Thay HTTP loopback nội bộ bằng module facades có DTO/contract rõ ràng. Business handlers giữ nguyên; propagated actor phải từ server-validated principal, không tin `X-Actor-*` do client gửi. Module boundary vẫn enforce ownership/authorization.

Giữ public route contracts hoạt động, đặc biệt mobile và `/api/gateway/staff/...`, `/api/gateway/admin/...`. Các prefix `/api/catalog`, `/api/orders`… là conceptual namespaces, không tự công khai toàn bộ downstream APIs. Source có routes trùng giữa controllers (tables/internal audit); `AddApplicationPart` tất cả assemblies mà không route/policy audit là không an toàn. Internal/dev endpoints không được trở thành public chỉ vì consolidation.

Dockerfile Gateway hiện chỉ publish Gateway và build/embed bốn web apps; **không phải consolidated backend**. CloudHost cần Dockerfile riêng, bind `0.0.0.0:$PORT`, health/readiness riêng; không build Customer Web. Container filesystem chỉ ephemeral.

## Worker migration plan

Inventory dựa trên `Program.cs` và implementation thực tế, không phải bốn worker giả định từ tên service:

| Worker / owner | Purpose, trigger và interval hiện tại | Durable state | Replacement |
|---|---|---|---|
| `TableAutoReleaseService` / Catalog | Scan mỗi **60 giây**, giải phóng OCCUPIED idle không CurrentOrderID; idle default **15 phút** | Catalog table status, UpdatedAt, order link | Delayed Queue task khi occupy; recheck/cas ở execution. Availability/QR request-driven cleanup bounded là hỗ trợ, không thay autonomous recovery |
| `OrderReadyConsumerService` / Customers | HTTP poll Orders ready-event outbox mỗi **2 giây**, default batch20 | Customers inbox, ready-dish notifications; source ack, retry/dead | Queue dispatch từ Orders outbox → Customers handler; projection + inbox commit rồi ack, duplicate không tạo notification mới |
| `PaymentCompletedConsumerService` / Orders | HTTP poll Billing `payment.completed` mỗi **5 giây**, default batch20 | Orders inbox, order completion, Catalog release, source ack | Queue → Orders completion handler → Catalog facade; persist/retry release receipt trước khi coi toàn flow hoàn tất |
| `PaymentReconciliationWorker` / Billing | Scan invoice chưa `SettlementCompletedAtUtc` mỗi **5 giây**, batch50 | Billing invoice/quote/split state, settlement receipts | Giữ synchronous fast path; Queue/Workflow recovery cho Orders completion và loyalty settlement; bounded scheduled scan safety |

Source paths:

- `src/Services/SelfRestaurant.Catalog.Api/Infrastructure/Tables/TableAutoReleaseService.cs`
- `src/Services/SelfRestaurant.Customers.Api/Infrastructure/OrderReadyConsumerService.cs`
- `src/Services/SelfRestaurant.Orders.Api/Infrastructure/Eventing/PaymentCompletedConsumerService.cs`
- `src/Services/SelfRestaurant.Billing.Api/Infrastructure/Payments/PaymentFinalizationService.cs`

Cho từng worker:

1. **Table auto-release:** có thể request-driven một phần; Queue delayed task phù hợp; Workflow timer có thể dùng nhưng chưa cần; scheduled recovery cần để bàn không treo khi không có request. Chỉ bỏ vòng lặp, không bỏ purpose. Source dùng `DateTime.Now`: cần timezone regression trên PostgreSQL trước cutover.
2. **Order ready:** request tạo ready event có thể dispatch ngay sau commit, không chờ HTTP poll. Queue phù hợp; Workflow không cần cho projection đơn bước. Recovery scan outbox/inbox bắt buộc. Chỉ bỏ poller sau tests ordering/retry/dedup/ack.
3. **Payment completed:** synchronous module call hỗ trợ mobile fast path nhưng chưa thay được toàn legacy bill/event flow. Queue phù hợp; Workflow hữu ích nếu nhiều step. Recovery phải giữ retry table release; hiện consumer có nhánh catch release lỗi rồi tiếp tục, cần audit receipt trước bỏ poller. Không đọc Catalog DB từ Orders.
4. **Payment reconciliation:** request-driven fast path đã có. Queue hoặc Workflow xử lý retry đa bước; scheduled scan là last resort. Không thể bỏ purpose: payment đã SUCCEEDED nhưng downstream fail vẫn có invoice unfinished để recovery. Không dùng background `Task.Run` làm guarantee.

Không worker nào được xóa an toàn ngay. Lifecycle loop có thể thay; transaction/handler/recovery behavior phải được giữ.

### Durable processing contract

Business transaction → owner outbox trong **cùng transaction** → post-commit enqueue → authenticated consumer → owner inbox/receipt + effect → ack. Enqueue fail/crash sau commit được sweeper đọc lại outbox; crash sau enqueue trước marking dispatched phải retry an toàn. Message chỉ chứa event ID/version/owner/entity reference, không token/credential. Unique event/operation key và DB receipts là authority; Queue dedup không thay business idempotency.

Hiện `FileIntegrationEventPublisher` có DB outbox và optional Rabbit publish, nhưng catch lỗi và transaction boundaries chưa đồng nhất ở mọi call site. **Không được tuyên bố atomic outbox đã hoàn tất**. Phải audit call sites và bổ sung transaction coverage trước migration.

Queues có HTTP API; chưa xác minh C# autonomous trigger/consumer phù hợp với CloudHost. Nếu cần thin Node Queue adapter, adapter chỉ verify delivery và gọi authenticated .NET handler, không sở hữu database/business logic. Một API project có adapter function **không đồng nghĩa đúng một executable artifact**. Nếu user yêu cầu nghiêm ngặt chỉ một ASP.NET artifact, direct C# trigger/receive integration là gate, không coi sẵn có SDK .NET.

Workflow chỉ orchestration; không rewrite domain sang JS. Queue/Workflow service là managed dependency dù không có Railway. At-least-once delivery yêu cầu dedup, retries có giới hạn, dead-letter/failed receipt và replay procedure. Queue retention hữu hạn không thay persistent outbox.

Nguồn: [Queues](https://vercel.com/docs/queues), [Queue HTTP API](https://vercel.com/docs/queues/api), [Workflows](https://vercel.com/docs/workflows).

Hobby Cron không thay được worker 2/5/60 giây: hiện lịch Hobby chỉ daily, không có near-real-time SLA. Nếu delayed tasks/push delivery không bảo đảm wake-up thì daily scan không đủ cho payment/demo. Phải chọn event-triggered recovery đã chứng minh hoặc đánh dấu blocker; không hứa miễn phí/minute recovery. [Cron pricing/limits](https://vercel.com/docs/cron-jobs/usage-and-pricing).

### Payment correctness

Giữ `PaymentFinalizationService.ConfirmAsync` cho Cashier ONSITE và webhook hợp lệ: branch/channel/customer/session/amount/currency validation, quote transaction lock, SUCCEEDED replay idempotent. Split chỉ tạo final invoice khi mọi part PAID, không close session sau part đầu.

Sau Billing commit, synchronous reconciliation hiện gọi Orders completion và loyalty settlement bằng stable invoice key; lỗi giữ unfinished invoice. Target giữ fast path có deadline, thêm durable recovery work để retry sau cold start/crash. Không trả giả SUCCEEDED, không enqueue thay cho validation. SUCCEEDED là payment canonical, không tự chứng minh mọi downstream settlement đã hoàn tất.

Acceptance gồm duplicate confirm/webhook, crash trước/sau enqueue, duplicate delivery, part1 session active, final part một invoice, Orders COMPLETED/session CLOSED/table released, loyalty settle một lần.

## Session migration plan

StaffChef/StaffCashier/Admin Gateway controllers hiện đọc/ghi `HttpContext.Session`: Employee ID, role, branch và display profile. `ActorContextHandler` lấy actor từ server session. Frontend gọi `/api/gateway/staff/auth/login` với credentials; không phải staff JWT flow. Android JWT không cần server Session.

Target: signed/encrypted **Secure HttpOnly SameSite cookie**, claims employeeId/role/branch/authVersion/expiry; staff scheme tách mobile audience. Giữ session DTO routes cho frontend compatibility nhưng hydrate từ validated principal/Identity, không distributed Session cache. Persist revocation/authVersion/refresh theo Identity ownership; verify active account và role/branch changes với bounded caching/revalidation. Stateless cookie không có nghĩa logout/revocation không cần durable state.

Profile display lấy Identity khi cần; không đưa mutable business state vào cookie. Customer Web CurrentTable/SavedCustomerTableContexts không nằm cloud scope. Session nghiệp vụ Android nằm REST/domain DB như hiện tại.

Key material/DataProtection rotation phải persistent, versioned, shared giữa replicas, không local container disk. Nếu cần key-ring persistence, chọn protected Identity-owned storage/managed secret mechanism đã nghiệm thu. Cookie auth cần CSRF token/origin checks, trusted proxy, expiry/replay/revocation tests; không chuyển token vào localStorage.

**Redis required: NO cho target này.** Redis session adapter đang chuẩn bị là compatibility phương án cũ, chưa được dùng làm cloud requirement. Không xóa adapter/local support trong planning turn.

## RabbitMQ replacement plan

**Remove from CLOUD sau acceptance**, giữ local compatibility. Source consumers chính đang HTTP-poll owner outboxes, không phải đều consume Rabbit subscriptions. Broker hiện optional; bỏ broker đơn thuần chưa giải quyết polling.

Inventory mọi publisher/event/legacy bill consumer → transactional outbox → Queue handlers/inboxes → recovery/replay. Chỉ cutover sau event parity, failure injection, no lost settlement/ready notification. Queue không phải SignalR cross-instance backplane.

## SignalR/WebSocket plan

Giữ `/hubs/mobile-notifications` và Android SignalR protocol. Hub hiện authorize role `customer`, group theo subject customer ID. **Chef hiện không kết nối hub này**; Chef dashboard dùng polling. Cashier service-request panel cũng polling 5 giây. Không cho Chef giả customer JWT để đạt POC.

Vercel native WebSockets có giới hạn lifetime và không bảo đảm instance affinity giữa request/connection. Gateway hiện dùng local `IHubContext` không có backplane: Chef action trên instance B không chắc tới Android socket instance A. Container/WebSocket support không tự giải quyết vấn đề này. [WebSocket behavior](https://vercel.com/docs/functions/websockets).

POC bắt buộc khi user cho phép staging deploy:

1. ASP.NET container upgrade handshake + Android JWT auth, reject invalid/expired tokens.
2. Authorized staff hub/channel với role/branch claims để Chef connect; không thay mobile customer policy.
3. `orderUpdated`, `serviceRequestUpdated`, payment events → client REST resync; REST canonical.
4. Android/Chef reconnect sau lifetime/redeploy/cold start, rejoin authorized groups và catch-up missed events.
5. Cross-instance action/socket khác instance; customer isolation và wrong-branch rejection.
6. Real Chef UI + Samsung E2E, measured latency, no manual refresh; polling fallback không được report realtime PASS.

Không Redis target vẫn cần shared fan-out. Candidate nghiên cứu: Supabase **server-only Broadcast backplane** / custom HubLifetimeManager, giữ SignalR clients, credentials chỉ backend. Đây là custom integration chưa implemented/verified, cần reliable subscriptions, reconnect, dedup và event catch-up. Không coi PostgreSQL transaction-pooler LISTEN là backplane.

Nếu ASP.NET SignalR hoặc fan-out POC fail, báo exact blocker trước. Chỉ đề xuất client Supabase Realtime như secondary option sau quyết định riêng; không tự rewrite Android. Channel authorization và REST canonical vẫn bắt buộc.

## Supabase connection plan

Giữ `viettable-staging`, Sydney `ap-southeast-2`; năm owner schemas. Runtime autoscaling ưu tiên Supavisor **transaction pooler 6543**, migration dùng direct hoặc session pooler **5432**, tùy IPv6 khả dụng. Runtime user từng schema least privilege; migration credential riêng, không dùng owner/admin credential cho request.

Npgsql: `Max Auto Prepare=0`; không explicit Prepare/session state/temp tables/LISTEN/session advisory locks trong runtime hiện tại. Source locks dùng SET LOCAL + transaction-scoped advisory lock trong explicit transaction. Live Supabase acceptance đã chứng minh pooling=true/MaxPoolSize2/MinPoolSize0/Multiplexing=false/**NoResetOnClose=true** chạy full workflow; default reset timeout, pooling=false menu503. Điều này cập nhật khuyến nghị planning trước: [Npgsql compatibility](https://www.npgsql.org/doc/compatibility.html) yêu cầu tránh reset logic trong transaction pooling. Chỉ dùng cho transaction-pooler runtime với fixed per-owner credentials và không session state; không bật như workaround chung cho direct/session connections. Xem [SUPABASE_ACCEPTANCE.md](SUPABASE_ACCEPTANCE.md).

Giới hạn pool mỗi owner và tổng replicas theo Supabase connection budget; không để default100 ×5 ×replicas. MinPool0, connection acquisition/command timeouts và retry transient có giới hạn, không tự replay mutation thiếu idempotency. Schema-qualified SQL/history không phụ thuộc session search_path.

TLS VerifyFull + provider CA; không tắt validation. **Live TLS, năm migrations/history, physical schema parity, canonical bootstrap/idempotency, owner-role login/20cross-schema denials, API workflows/concurrency và Storage PASS**; xem [SUPABASE_ACCEPTANCE.md](SUPABASE_ACCEPTANCE.md). Không suy ra Vercel runtime PASS từ DB/Storage acceptance; direct IPv6 network vẫn chưa dùng.

Nguồn: [Supabase connections](https://supabase.com/docs/guides/database/connecting-to-postgres), [Npgsql parameters](https://www.npgsql.org/doc/connection-string-parameters.html).

## Supabase Storage

Giữ abstraction `SupabaseDishMediaStorage`; immutable object keys, backend authorized upload, public image read nếu bucket policy cho phép. Không persistent local uploads trên Vercel, không đưa service key vào frontend. Catalog commit/reference và orphan cleanup phải failure-safe.

Bucket staging đã tạo; live adapter upload/read/restart acceptance chưa chạy. Kiểm tra request body limits thực tế trên container/proxy bằng file hợp lệ: tài liệu ASP.NET guide và một số bundled limits khác nhau, không mặc định ảnh 5MB đi qua backend sẽ được chấp nhận. Nếu cần signed upload phải là backend-issued short-lived scoped authorization, không expose service credential, và là implementation task riêng.

## Vercel deployments

| Project | Artifact / routing |
|---|---|
| `viettable-api` | CloudHost ASP.NET container; optional minimal Queue adapter function nếu POC yêu cầu |
| `viettable-chef` | Existing Chef Vite static build |
| `viettable-cashier` | Existing Cashier Vite static build |
| `viettable-admin` | Existing Admin/Manager Vite static build |

Vite base hiện `/app/chef/`, `/app/cashier/`, `/app/admin/`; dev ports5174/5175/5176, preview4174/4175/4176. `VITE_GATEWAY_URL` phục vụ dev proxy; production JS dùng relative API + credentials. Chỉ set biến này không tạo production reverse proxy.

Thiết kế mỗi web same-origin `/api` proxy tới API, SPA deep-link/base rewrites đúng, staff hub WSS proxy khi cần. Cookie host/path, Set-Cookie forwarding, CSRF và logout giữa apps phải được test; không tùy tiện shared wildcard cookie. Android gọi HTTPS API domain trực tiếp khi cloud acceptance/cutover được cho phép; local ADB reverse không đổi trong preparation.

## Cost / simplicity comparison

| Dimension | A: Vercel + Supabase | B: Vercel + Railway + Supabase |
|---|---|---|
| Deployables | Bốn Vercel projects; một backend container, có thể thêm adapter function | Ba staff web projects + sáu backend services và Redis nếu giữ session cũ; consolidated Railway host là một phương án khác chưa nghiệm thu |
| Paid resources | Không có Railway/broker/Redis target; compute, memory, Queue/Workflow, DB/storage/egress vẫn có usage/plan limits | Thêm persistent compute/cache/broker theo architecture lựa chọn |
| Code changes | Cao hơn: composition, session, workers, cross-instance push | Thấp hơn nếu giữ processes/hosted workers/backplane phù hợp |
| Background | Durable triggers + bounded recovery, không continuous loop | Continuous workers dễ giữ behavior cũ |
| Realtime | WebSockets Beta; lifetime/reconnect/fan-out POC | Persistent ASP.NET dễ hơn nhưng vẫn cần multi-instance backplane |
| Demo operation | Ít provider, scale-to-zero nhưng có cold start; correctness gates chưa PASS | Quen mô hình hiện tại nhưng thêm vận hành/chi phí |

Không cam kết zero-cost hoặc “mở Android dùng ngay” khi chưa đo cold start/quotas và load. Container/WS/Queues Beta cần kiểm tra limits/availability lại trước báo cáo tháng11. Chọn A làm target; nếu gates chưa đạt gần freeze thì báo user quyết định rollback/fallback, không tự provision Railway.

## Risks

- Consolidation route collisions hoặc mất backend authorization/owner isolation.
- Cookie/key rotation/logout không survive replicas/restart; browser proxy CSRF/cookie bugs.
- Atomic outbox chưa đồng nhất; Queue .NET trigger chưa chứng minh; daily recovery không đủ payment SLA.
- Multi-instance SignalR event loss; staff hub chưa tồn tại; không gọi đây là completed realtime migration.
- Pooler transaction semantics/connection exhaustion; owner roles và TLS chưa cloud acceptance.
- Cold starts, finite WS lifetime, Beta changes, upload limits và tháng11 plan/usage.
- Removing Customer Web deployment không được làm mất handlers đang cần cho Android/staff workflows.

## Migration sequence

1. **Lượt này:** document decision/source audit; không deploy/refactor/migrate.
2. Supabase prep task riêng: retry TLS probe, migrations5schemas, bootstrap, least-privilege roles/cross-schema denial, Storage live tests; không reset local data.
3. CloudHost composition POC local: preserve public contracts/authorization, compare six-host responses, ownership tests. Container build không gọi cloud acceptance PASS.
4. Staff cookie migration + durable key/revocation tests; real browser login/reload/host replacement.
5. Extract four handlers; transactional outbox/inbox + Queue trigger integration POC; payment/split/retry/cleanup fault-injection tests. Remove loops cloud-only sau parity.
6. Khi user cho phép staging runtime: minimal Vercel container/SignalR/Queue POCs, cross-instance/reconnect/branch auth. Không production deploy hoặc billing upgrade tự động.
7. Deploy ba staff builds sau proxy/cookie/route acceptance; Customer Web excluded.
8. Samsung + Chef + Cashier + Admin final flow: service requests, READY→SERVING, realtime, ONSITE auto-success, split, invoice, CLOSED/table release; restart recovery và histories.
9. Measured budget/cold start và demo readiness review trước cloud cutover; secrets platform-only, no client DB access.

## Final target diagram

```text
Customer Android ── HTTPS/WSS ──────────────────────────┐
Chef / Cashier / Admin Web ── same-origin API/WSS proxy ─┤
                                                       ▼
                Vercel viettable-api: ASP.NET CloudHost
                Gateway facade + five owner modules
                          │                  │
                owner-scoped DB roles       Supabase Storage
                          ▼
          Supabase PostgreSQL: catalog/orders/customers/identity/billing
                          │
                 durable outbox / inbox / receipts
                          ↕
            Vercel Queue / optional Workflow + recovery triggers

SignalR shared fan-out: mandatory POC, not yet selected/verified.
No Customer Web deploy. No Railway / Redis / RabbitMQ cloud target.
```

**Current status: architecture plan only; cloud runtime acceptance NOT RUN.**
