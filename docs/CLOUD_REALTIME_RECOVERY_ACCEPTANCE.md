# Cloud realtime và durable recovery — 09/10/2026

## Phạm vi và deployment

Tiếp tục từ `8b6c078`, branch `cloud/postgres-migration`. Chỉ CloudHost API
Preview; không đổi Android, business API, service/database ownership hoặc staff
frontend. Không Production deployment, paid add-on, Railway, reset DB hay push.

- Deployment: `dpl_2n6LXbAyfocDMgG5DdTid2u6N5wK`.
- [Preview thật](https://viettable-6ezq8ysww-hienkhanhvu981-7145.vercel.app).
- `vercel inspect --json`: target `preview`, READY, runtime `container`.
- Remote Docker .NET 8 publish/build/OCI push: PASS.
- `/healthz` và `/readyz`: HTTP200 trên nhiều instance thực tế.
- Public container vẫn Production mode; không có public fault/debug endpoint.
- Production bootstrap `dpl_HtbM5wEHbj4HcfQRW4RJCya6JPV5` giữ nguyên.

Lệnh thực thi tại `SelfRestaurant-master`:

```powershell
npx.cmd --yes vercel@62.4.0 env add Realtime__Provider preview --value SupabasePrivate --type config --yes
npx.cmd --yes vercel@62.4.0 deploy . --target preview --yes
npx.cmd --yes vercel@62.4.0 inspect dpl_2n6LXbAyfocDMgG5DdTid2u6N5wK --json
```

Chỉ thêm một **Config không secret, Preview-only**. Chín application Secrets
không đổi. Deployment Protection giữ nguyên; verifier dùng OIDC ngắn hạn trong
RAM, tách biệt Customer JWT và staff cookie. Không `env pull`/plaintext credential.

## SignalR: chẩn đoán trước khi sửa

Trace Gateway thực tế: Chef commit qua Orders API, đọc REST canonical, rồi
`SendAsync("orderUpdated", ...)` tới authorized customer group. Cashier commit
Service Request trước khi phát `serviceRequestUpdated`. Tên event và DTO camelCase
đã đúng; vấn đề không phải thay API hay dựng state client giả.

Thử nghiệm diagnostic-only, trước backplane:

- Hai customer WSS trên instance `c1dad509bfd449adb0e83cfe2c783abc` và
  `eb3c05fd2cdd44c9bc518f92b633d6cf`, mở trước nghiệp vụ.
- Chef START order30 chạy ở instance đầu. PREPARING dự kiến `1/1/0`
  (hai socket sở hữu / một customer khác); thực tế **`1/0/0`** trong 20 giây.
- Cùng-instance event delivery có hoạt động ở lượt trước. Đây là bằng chứng
  `DefaultHubLifetimeManager`/group trong RAM không fan-out qua instance khác,
  không chỉ phỏng đoán từ timeout hay handshake.
- Một lượt sau gặp Deployment Protection401 do OIDC verifier hết hạn. Không
  coi lỗi nền tảng đó là lỗi role/business; verifier đã renew OIDC trong RAM.

## Fix server-only và ranh giới bảo mật

CloudHost đăng ký `DiagnosticHubLifetimeManager` và Supabase private backplane.
Gateway authorization/group membership tiếp tục là front door. Android/browser
vẫn kết nối SignalR Gateway, không được cấp Supabase key hoặc token mới.

- Trước implement đã nghiệm thu private `phx_join` bằng server API-key header,
  rồi private REST broadcast → WSS roundtrip trên Supabase staging thật.
- Metadata read-only: `realtime.messages` có RLS bật và **zero allow policies**.
  Không tạo anon/authenticated read policy hay nới RLS. Server-only service key
  là privileged RLS bypass **có chủ đích**, không phải Customer token.
- Topic phân tách environment namespace và loại Hub bằng HMAC; group reference
  cũng HMAC. Broker transport chỉ nhận known event methods và envelope hợp lệ.
- Instance chỉ dispatch vào group đã được Gateway authorize/join ở local Hub.
  Không dùng payload để cho phép client tự chọn customer/branch.
- API key chỉ nằm trong HTTP/WSS header, không query string, log hay source.
- Mỗi Hub/instance chỉ giữ private WSS khi có client SignalR. Last disconnect
  hủy heartbeat/socket; HTTP publisher không cần giữ worker/socket thường trực.
- Heartbeat transport 20 giây, reconnect backoff hữu hạn 1–10 giây. Không thêm
  Android polling, background business worker, SDK upgrade hay dịch vụ trả phí.
- Default không bật provider: local Docker tiếp tục in-memory behavior/rollback.
- Trace chỉ gồm opaque instance/event ID, request ID, HMAC correlation/group,
  group type, stage, local recipient count và UTC. Không log identity/raw group
  hoặc payload. Broker accepted **không** được coi là client delivered.

## WSS acceptance thực tế sau fix

Ba WSS giữ mở trước thao tác REST; hai cùng Customer, một Customer khác.
Verifier có SignalR heartbeat, bounded 20 giây, và dispatch barrier sau handshake
để đảm bảo `OnConnected`/group join đã hoàn tất, không suy luận từ handshake.

Lượt session16 / order34 / Service Request17 / table36 / branch3:

| Event/state | Expected (own1/own2/other) | Observed | Result |
| --- | --- | --- | --- |
| `orderUpdated` PREPARING | 1/1/0 | 1/1/0 | PASS |
| `orderUpdated` READY | 1/1/0 | 1/1/0 | PASS |
| `serviceRequestUpdated` ACKNOWLEDGED | 1/1/0 | 1/1/0 | PASS |
| `serviceRequestUpdated` COMPLETED | 1/1/0 | 1/1/0 | PASS |

**Multi-instance: PASS**, không phải affinity assumption:

- Own socket1: `da9bab88bb0e4ad7a8496825ab8c062b`.
- Own socket2: `8bfbea001415416d8561a9865dd90dc3`.
- Chef START/READY: `c057317bc4374129974b172c2d83146c`.
- Cashier ACK/COMPLETE: `112e9e6ca3e241668af9b84c353dce4d`.
- Cả hai authorized socket vẫn nhận event dù publisher ở instance khác, kể cả
  publisher không có các customer socket trong RAM.
- Ngắt own socket1, reconnect tại instance `112e9...`, REST item READY: PASS.
  REST là canonical; SignalR là invalidation/resync signal.
- Other Customer nhận 0 event; GET order34 của Customer khác403.
- Staff WSS own branch subscription accepted, wrong branch rejected.
- Wrong-branch Cashier mutation Service Request403; Customer staff mutation401.
- Không có WSS reader failure trong lượt này.

Lặp lại với session17 / order35 / request18: assertion count ngay sau mỗi
transition, thêm 2 giây quiet observation và kiểm tra lại toàn bộ counts cuối
payment flow **PASS** (PREPARING/READY/ACKNOWLEDGED/COMPLETED vẫn `1/1/0`, không
late duplicate). Ba initial socket cùng instance `02acfd04cb2c41d5b81061da488c7818`;
Cashier publisher/reconnected customer ở `8e42f51c5a3b4b8aae09e555f88cb5ad`.
Đây là thêm same-instance + cross-instance Service Request evidence, không thay
thế lượt hai customer socket trên hai instance khác nhau ở trên. Payment17 /
invoice17 total5.000VND, session17 CLOSED; no socket failure. Independent read-only
owner check sau flow: table36 AVAILABLE/currentOrder null, một SUCCEEDED payment,
một settled invoice, zero pending READY và đúng một notification. Một inspection
session16 được chạy khi session17 đang dùng lại bàn36 nên lúc đó table OCCUPIED;
không suy luận từ trạng thái bàn thời điểm khác rằng close/release16 bị lỗi.

Một lượt diagnostic trước đó đếm hai READY snapshots. Không dùng lượt đó làm
exact-count acceptance; không suy luận duplicate financial write từ snapshot.
Lượt mới ở trên quan sát đủ trạng thái và đúng counts. Deduplication theo event
ID có bounded window4096, **không phải cam kết exactly-once hoặc durable realtime**.
Transport outage vẫn có thể làm mất notification; reconnect phải REST resync.
Private broker outage/resubscription riêng chưa được fault-inject, không báo PASS
cho kịch bản đó. Đây không thay thế durable business recovery bên dưới.

## Durable recovery: fault ngoài public Preview

`CloudRecoveryPrepare` chạy CloudHost con trong Docker test harness private,
Development **chỉ ở harness**, không publish port. Dùng các owner credentials
đúng schema qua RAM và staging QA entities hợp lệ; không SQL sửa state/reset.
Test-only flags existing vẫn không chạy trong public Production-mode Preview.

Case cô lập session15, orders31/32, table37, branch3, payment14, invoice14,
total70.000VND:

1. READY commit thành công nhưng Customers projection fail: outbox PENDING,
   chưa có notification.
2. Payment commit SUCCEEDED rồi fault sau commit: session chưa CLOSED.
3. Restart harness; table release fault và projection fault vẫn bật. Protected
   recovery bị gián đoạn HTTP500 **sau partial effects**. Process private dừng.
4. Read-only inspector dùng từng runtime owner chứng minh PostgreSQL còn:
   session CLOSED, table OCCUPIED, một invoice chưa settled, một SUCCEEDED
   payment, một pending READY event, zero notification, recovery attempt1.
5. Deploy CloudHost Preview mới ở trên. Pending state vẫn tồn tại sau actual
   redeploy, không chỉ restart verifier.
6. Protected remote recovery concurrent calls200/202 (one lease busy), repeated
   calls200; canonical notification/invoice/session/table kiểm tra qua API thật.
7. Independent owner-scoped read-only postcheck:

| Durable field | Before redeploy/recovery | After remote recovery |
| --- | --- | --- |
| Session15 | CLOSED | CLOSED |
| Catalog table37 | OCCUPIED | AVAILABLE, currentOrder null |
| Payment14 | SUCCEEDED, count1 | SUCCEEDED, count1 |
| Invoice14 | count1, 70.000, unsettled | count1, 70.000, settled |
| Recovery attempts | 1 | 2 |
| Pending READY | 1 | 0 |
| READY events total | 1 | 1 |
| Projected notification | 0 | 1 |

**Manual recovery after redeploy: PASS.** Concurrent/repeated recovery không
duplicate invoice/payment/notification; table release đúng một lần về business
effect, không tuyên bố chỉ có một physical HTTP request trong distributed retry.
Anonymous và Customer JWT tới protected recovery401. Cron authentication không
nới; không thêm public fault endpoint hoặc application Development config.

**Automatic unattended scheduling: UNVERIFIED.** Không tạo Preview Cron hay
schedule; operator vẫn phải gọi protected recovery. Không suy luận automatic
recovery PASS từ manual endpoint success.

Operator command examples (public metadata only; secrets được helper đọc DPAPI):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-staging-database.ps1 -ProjectRef waxzqkuwuwubdkrzxbqu -DatabaseHost aws-0-ap-southeast-2.pooler.supabase.com -DatabaseUser postgres.waxzqkuwuwubdkrzxbqu -Action RealtimePolicyProbe
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-staging-database.ps1 -ProjectRef waxzqkuwuwubdkrzxbqu -DatabaseHost aws-0-ap-southeast-2.pooler.supabase.com -DatabaseUser postgres.waxzqkuwuwubdkrzxbqu -Action CloudRecoveryInspect -RecoverySessionId 15
```

`CloudRecoveryPrepare` là **mutation/fault test**, không tự chạy lại khi chỉ cần
inspect. Lượt đầu cố ý giữ pending state sau HTTP500; helper đã được chỉnh để
chấp nhận interruption đó trong Prepare mode. Chỉnh verifier này được build,
không tuyên bố đã chạy lại Prepare full sau chỉnh; runtime acceptance dựa vào
fault thực tế, independent before/after reads và remote protected recovery.

## Regression, tests và secret review

- Customer login/JWT và Chef/Cashier/Admin cookie login/session roles/branches
  PASS trên deployment mới. Không bypass staff bằng Admin.
- Cloud session16: real Catalog dish, PREPARING → READY → Customer SERVING,
  Service Request ACK/COMPLETE, ONSITE payment16, invoice16 total5.000VND,
  session CLOSED; API không dùng mock/local fallback.
- Supabase verified-TLS owner connectivity PASS; không đổi pooler/DB secrets,
  migrations/bootstrap hoặc Storage implementation. Storage evidence trước đó
  trong `VERCEL_API_STAGING.md` reused, không claim upload test mới lượt này.
- Docker SDK8 CloudHost build và CloudHost tests **21/21 PASS**.
- Backend contract tests **141 PASS, 1 existing skip, 0 fail**; configured
  CloudSessionTests skip không được coi là runtime cookie continuity PASS.
- CloudPreparation build PASS, 0 warnings/errors. SDK NuGet cache container
  thiếu ở một no-restore attempt; restored named cache và subsequent build PASS.
- Remote Docker publish/build PASS. Android/web source không đổi, không build
  lại hoặc deploy staff frontends.
- Bounded runtime-log secret/JWT scan trên diagnostic và fixed deployment:
  **97 returned entries PASS**, actual11DPAPI values và credential patterns;
  không emit/persist raw logs, không claim đã đọc mọi historical log.
- Temporary WSS/OIDC verifiers/log buffers ở ngoài Git. No credentials/tokens,
  logs/screenshots/test artifacts hoặc `.idea` được đưa vào commit.
- Final source/docs secret scan: 11 candidate files, 11 actual DPAPI values,
  no JWT/secret match; `git diff --check` PASS. Ngày 10/10/2026 user yêu cầu chốt
  backend: review lại transport, authorization, last-client cleanup và toàn bộ
  diff; chạy lại CloudHost21/21 và backend141 PASS/1 existing skip trước commit.
  Runtime CloudHost source không đổi so với deployment đã nghiệm thu; harness/
  tests/docs không được upload qua API-only allowlist. Hai `.idea` changes có
  trước (bao gồm `vcs.xml` đã staged) giữ nguyên, loại khỏi scoped commit; không push.

Primary platform references: [Vercel WSS instance lifecycle](https://vercel.com/docs/functions/websockets),
[Supabase private Realtime/RLS](https://supabase.com/docs/guides/realtime/authorization),
[Realtime protocol](https://supabase.com/docs/guides/realtime/protocol),
[Private Broadcast API](https://supabase.com/docs/guides/realtime/broadcast).

## Acceptance

`REALTIME + RECOVERY: PASS` cho actual multi-instance event delivery,
Customer/branch isolation, client reconnect + REST resync và **manual durable
recovery after redeploy**. Automatic scheduling và private-broker outage fault
acceptance vẫn UNVERIFIED; không suy diễn thành production operational SLA.
