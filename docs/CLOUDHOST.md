# VietTable CloudHost — Phase B local acceptance

Acceptance: 2026-10-05–06. Branch: `cloud/postgres-migration`; base `8cb0322`.

Status: **CLOUDHOST LOCAL: PASS** for the Phase B local/API acceptance below.
This is not Vercel deployment, multi-instance or Samsung/browser visual acceptance.
No Vercel/Railway deployment or push.

## Composition and ownership

`SelfRestaurant-master/src/Backend/VietTable.CloudHost` is a .NET 8 executable.
One public Gateway listener defaults to port **5200**, binds `0.0.0.0`, honors `PORT`.
The five API assemblies each retain their own ASP.NET application, DI container,
MVC filters, transactions, authentication, exception mapping and DbContext.
Standalone API `Program.cs` entrypoints call the same reusable module factories.
No controllers or domain handlers are copied into CloudHost.

| Module | DbContext | Schema/runtime role ownership |
|---|---|---|
| Catalog | CatalogDbContext | catalog |
| Orders | OrdersDbContext | orders |
| Customers | CustomersDbContext | customers |
| Identity | IdentityDbContext | identity |
| Billing | BillingDbContext | billing |

Gateway owns no business database. Each owner receives its own connection/role.
The Identity-owned boundary exposes staff-version validation and encrypted key
repository operations; it does not expose its DbContext to Gateway.

## Internal calls

Existing typed API clients retain DTOs and error semantics. CloudHost replaces
their network transport using `ModuleClientFilter` and `ModuleServer`.
Requests dispatch directly through the receiving module's MVC pipeline and a
new request scope; no downstream socket, localhost port or self-HTTP request.
Synthetic `*.module.invalid` identifiers are routing keys, not DNS destinations.
Missing internal modules fail closed. External Google/payment/Storage calls
retain their genuine HTTP transport. Async execution contexts are isolated so
module disposal cannot erase Gateway's actor/correlation HttpContext.

Classification: owner/client calls = in-process adapter (A); unchanged standalone
client defaults = legacy HTTP (B); outbox/inbox recovery consumers = Phase C (C).

## Route compatibility

All public mobile and staff controller routes are mapped from the **same Gateway
assemblies**, not duplicated or renamed.

| Current route family | CloudHost route | Compatibility |
|---|---|---|
| /api/mobile/v1/auth/* | unchanged | same controller/DTO |
| /api/mobile/v1/branches/* | unchanged | same controller/DTO |
| /api/mobile/v1/me*, reservations*, favorites*, notifications* | unchanged | same controller/DTO |
| /api/mobile/v1/qr*, check-ins*, dining-sessions*, orders*, service-requests* | unchanged | same controller/DTO |
| /api/mobile/v1/checkout*, payments*, invoices*, split-bills* | unchanged | same controller/DTO |
| /api/gateway/staff/* | unchanged | cookie-backed identity in CloudHost |
| /api/gateway/staff/cashier/* | unchanged | cookie-backed identity in CloudHost |
| /api/gateway/admin/* | unchanged | cookie-backed identity in CloudHost |
| /hubs/mobile-notifications | unchanged | bearer auth / existing SignalR protocol |
| /hubs/customer-notifications | same path for existing Cashier client | CloudHost requires staff cookie and own-branch subscription; legacy table subscriptions disabled |
| /app/chef/*, /app/cashier/*, /app/admin/* | unchanged | existing builds |
| /healthz, /readyz | unchanged | Gateway + owner readiness |
| downstream private routes | not exposed | internal module pipeline only |
| legacy Customer Web | not deployed | source and six-service mode preserved |

This inventory is a mapping guarantee, **not** evidence that every individual
endpoint has been runtime-tested. Samsung's current port5100 URL is unchanged.

## Auth and durable keys

Customer JWT/refresh/Google contract uses existing Identity/Gateway services.
Staff login uses protected HttpOnly cookie claims, not ASP.NET Session storage.
`StaffIdentity()` supplies a request-local compatibility facade for existing
staff controllers. It is reconstructed from validated claims every request.
Role, branch, active status, password and identity version are checked against
Identity; inactive/changed employees lose their cookie authorization.

Cookie lifetime8h, no sliding refresh, SameSite Lax, Secure outside Development.
Unsafe staff requests reject cross-site Origin/Sec-Fetch-Site. Mobile bearer
routes are unaffected. No staff JWT is added to browser localStorage.

DataProtection XML is stored only by Identity in `CloudDataProtectionKeys`.
An externally supplied private certificate encrypts key material; raw key XML
is rejected. Environment application namespace separates cloud environments.
Signing key/certificate must be stable across instances/restarts and supplied
securely; never regenerate at startup, never persist private key to container disk.

CloudHost staff recovery tokens use Identity `CloudStaffResetTokens`, SHA256
hash-only storage, expiry/used state and transaction-scoped lock for redemption.
Legacy RAM-token mode remains outside CloudHost. Both Identity migrations have
been applied to staging. Runtime validation/redemption after an actual process
restart, one-time use and stale-cookie revocation passed.

## Workers and request-time limits

CloudHost explicitly disables `Runtime:LegacyPersistentWorkers`, RabbitMQ and
diagnostic JSONL. Original six-service defaults continue to enable existing workers.

| Disabled worker | Remaining dependency / Phase C requirement |
|---|---|
| TableAutoReleaseService | autonomous stale-table cleanup |
| OrderReadyConsumerService | Customers notification-inbox projection from Orders outbox |
| PaymentCompletedConsumerService | legacy payment event completion/release recovery |
| PaymentReconciliationWorker | retry after synchronous settlement/downstream failure |

SignalR notifications do not replace durable notification inbox projection.
Billing has synchronous finalization, but failure recovery still requires Phase C.
No Queue/Workflow replacement is implemented or claimed here.

## Process-local state audit

| State | Source | Safe to lose? | Treatment |
|---|---|---|---|
| staff Session identity | legacy Gateway | no | CloudHost cookie claims + Identity validation |
| DataProtection keys | ASP.NET defaults | no | encrypted Identity repository |
| staff reset tokens | legacy Identity static dictionary | no | CloudHost hash-only DB path |
| module registry | ModuleRouter | yes | immutable startup topology |
| request identity facade | StaffCookieState | yes | reconstruct every request |
| SignalR connections/groups | Gateway hub | reconnect required | existing local protocol; multi-instance fan-out not accepted |
| rate limiting/cache | existing modules | local protection/cache only | not a distributed security SLA |
| order/payment/idempotency state | owner databases | no | existing owner persistence/transactions retained |

## Supabase configuration and execution

Runtime: transaction pooler6543, TLS verified with provider CA, five owner logins,
AutoPrepare0, small client pools and accepted no-reset compatibility configuration.
Migration: session pooler5432 via explicit tool, never startup admin credentials.
MediaStorage provider Supabase reuses existing accepted storage implementation;
no persistent `/uploads` requirement. Local provider remains available in legacy mode.

Build with .NET8 SDK (this Windows host currently uses a scoped Docker SDK):

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master
dotnet build src/Backend/VietTable.CloudHost
dotnet test tests/VietTable.CloudHost.Tests
dotnet publish src/Backend/VietTable.CloudHost -c Release
# For an interactive host, first supply the secure environment settings below.
dotnet run --project src/Backend/VietTable.CloudHost --no-launch-profile
```

Secure local fixture preparation/acceptance from workspace:

```powershell
.\scripts\initialize-cloudhost-secrets.ps1
.\scripts\test-staging-database.ps1 -ProjectRef <project-ref> -DatabaseHost <pooler-host> -DatabaseUser <admin-pooler-user> -Action CloudHost
.\scripts\test-staging-database.ps1 -ProjectRef <project-ref> -DatabaseHost <pooler-host> -DatabaseUser <admin-pooler-user> -Action CloudHostAuth
```

Helpers decrypt current-user DPAPI only in memory/stdin. Acceptance deliberately
creates stable QA identities via existing management/register API pipelines;
these are staging fixtures, never production startup seed or fallback data.
Password resets/role changes are not implicit fixture repair.
The acceptance runner starts and stops its actual CloudHost child automatically;
its port5200 is inside the isolated tooling container, not a persistent Windows
listener. Run the host explicitly with secure settings for interactive clients.

Required secret configuration: `MobileJwt:SigningKey`,
`CloudHost:DataProtectionCertificate`, five `ConnectionStrings:*Db`, and
`MediaStorage:Supabase:ServiceKey`. Public configuration: environment namespace,
Storage project/bucket, provider/CA, PORT and frontend build paths. When enabling
Google auth, explicitly supply `Authentication:Google:ClientId` (mobile audience)
and `Authentication:Google:WebClientId` separately; do not rely on excluded
appsettings. Google contract tests are offline; CloudHost live OAuth is not claimed.
Publish excludes referenced appsettings to avoid colliding defaults; environment
configuration is mandatory. Production startup must not use Development cookie policy.

## Evidence and acceptance scope

- CloudHost build: PASS; transport/context isolation, cookie roundtrip and
  PostgreSQL query/Identity reset-token ownership and staff hub branch-guard
  regressions plus encoded route-key transport regression:5/5 PASS.
  Full solution build:0 warnings,0 errors.
- Existing backend contracts:141 PASS,1 Redis opt-in SKIP (not cloud dependency).
- Actual Supabase runtime: one public listener/five module readiness PASS.
- Customer register/login/refresh/profile, branches/menu/layout/history:PASS.
- Admin, Chef, Cashier and other-branch Cashier login/cookie/dashboard:PASS.
- PostgreSQL query regressions found in employee sort and report date boundaries;
  fixed explicit timestamp kinds without weakening strict instant conversion.
- Chef/Cashier/Admin frontend builds:PASS. Admin lockfile audit reports existing
  dependency vulnerabilities; no blanket upgrade performed in this phase.
- Submit/replay/two distinct rounds:PASS after latency fix; measured5–7.4s.
- Chef PREPARING/READY and authenticated SignalR `orderUpdated`:PASS.
  Chef event uses `statusCode`; mobile-submit event uses `status`. The probe now
  respects both existing shapes and filters for the expected state, not a stale
  submit notification. A test-helper JSON parsing error on a successful empty
  receive-item response was fixed; production response shape was not changed.
- Identity migrations/history:4 applied, including hash-only staff reset state.
- Five transaction-pooler owner logins and20/20 foreign-schema SELECT denials:PASS.
- Supabase Storage fresh upload/read/checksum, persistence in a new process,
  unauthenticated write rejection, exact temporary object cleanup:PASS.
  A first verify attempt used an already-cleaned earlier receipt and failed;
  a new upload/verify pair passed. No production object was deleted.
- `Dockerfile.vercel` local image build:PASS, three staff builds only, non-root
  runtime, environment-only settings, no Customer Web or embedded credentials.
- E2E on staging session3/payment3, repeated on fresh session4/payment4 after
  staff hub hardening and session5/payment5 after encoded-path fix: two rounds,
  Chef PREPARING/READY, customer receive SERVING,
  WATER acknowledge/complete, ONSITE SUCCEEDED, exact invoice total, session
  CLOSED and table released:PASS. Customer payment mutation401 and wrong-branch
  Cashier mutation403 leave payment pending until authorized confirmation.
- Staff SPA HTML entrypoints and their actual cookie-authenticated API workflows:
  PASS. Admin category CREATE/READ/UPDATE/DELETE persisted, exact temporary category
  removed, non-admin mutation401. No browser clicks or Samsung test are claimed;
  Phase B exercised Android-compatible HTTP/WebSocket clients, not device UI.
- Staff hub anonymous negotiation401/customer bearer403, own-branch subscribe
  accepted/foreign-branch denied:PASS. Existing Cashier SignalR client is retained.
- Actual CloudHost process killed/restarted: same customer JWT and four staff
  cookies restored, mobile SignalR reconnected:PASS. Durable staff reset token
  survived restart, redeemed once, second use400, old staff cookie rejected:PASS.
- Customer logout/refresh revocation through CloudHost:PASS (old refresh401).
- Reservations through CloudHost: create/read/automatic-table idempotent replay,
  changed-party-size replay409 and customer cancel:PASS. No extra dining session,
  order or payment was created by this auth/reservation-only probe.
- Reservation testing found two independent replay issues: the in-process adapter
  passed escaped `%3A` in the request path instead of ASP.NET's decoded path, and
  Gateway compared an automatically assigned table to the omitted request table.
  Both were corrected without changing routes/DTOs. The adapter now uses ASP.NET
  `PathString.FromUriComponent`, with colon/Unicode/encoded-slash regressions.
  See the [ASP.NET 8 decoder source](https://github.com/dotnet/aspnetcore/blob/v8.0.0/src/Http/Http.Abstractions/src/PathString.cs).
  Exact QA reservations from interrupted probes are cancelled via their owner API,
  not deleted or repaired with SQL.
- The measured order response timeout in
  `DiningSessionsController.MapOrder` (HTTP503) occurred after11.5s with the
  inherited10s timeout; duplicate Catalog refreshes consumed about5s. New-order
  response now reuses its validated Catalog snapshot/session/status; CloudHost
  timeout is configurable with a30s default (legacy10s default unchanged).
  Actual retests passed; histories can still take about18s over Sydney DB
  round trips. Performance is not a production latency SLA.
- Staging QA accounts and closed sessions from acceptance/retries are retained;
  no reset/SQL cleanup or production fixtures were used. Secrets stayed in DPAPI
  outside Git. Candidate scan checked10 secret values with no matches;
  `git diff --check`:PASS. The two pre-existing .idea changes remain excluded.

## Limits before cloud deployment

- Phase C worker replacements and settlement/notification failure recovery remain
  required. A synchronous happy-path PASS does not prove asynchronous recovery.
- SignalR shared fan-out, load-balancer routing, scale-to-zero and multi-instance
  acceptance remain unverified; existing groups live in the current process.
- Staff browser/Samsung cloud acceptance and trusted reverse-proxy HTTPS/WSS
  forwarding must be tested when deployment is authorized.
- PostgreSQL staging migrations are accepted. CloudHost SQL Server key/reset table
  provisioning is not accepted; unchanged standalone SQL Server mode still builds.
- DataProtection certificate/key backup/rotation is a deployment responsibility.
  Keep old decryption certificates when rotating; do not regenerate on cold start.

## Future deployment

No deployment is authorized in Phase B. Follow current official
[Vercel Docker guide](https://vercel.com/kb/guide/docker), rather than outdated
Services examples. Container preparation requires PORT binding, no local secret
files/dev certificate, graceful shutdown and same-origin staff builds.
Local SignalR success cannot prove cross-instance routing/fan-out or scale-to-zero
reconnect. Phase C recovery and multi-instance POC remain separate gates.
