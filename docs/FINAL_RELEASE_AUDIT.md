# Final VietTable release audit and acceptance

## Subsequent acceptance: ONSITE F01 closed

2026-09-23: [ONSITE payment acceptance](ONSITE_PAYMENT_ACCEPTANCE.md) supersedes the F01/payment and associated regression gaps below: real Cashier and Samsung non-split/split settlement, review CRUD, concurrency and outage retry passed. **Database ownership F02 and deployment blockers remain.** The original audit below is retained as historical evidence, not the current ONSITE verdict.

Audit date: 2026-09-23, Asia/Saigon (started 01:26 local time).
Audited code commit: `550c273 feat: polish Android UI and release readiness`.
Baseline `20a32b9` is preserved. Working tree was clean before this audit; no remote was configured.

## Verdict

- **Code baseline: NOT_READY_FOR_RELEASE_CANDIDATE.** Android regression passes, but mobile ONSITE settlement has no completion path and database isolation is incomplete. Several backend acceptance suites also cannot finish in the current environment.
- **Deployment: BLOCKED.** Production HTTPS/proxy configuration, release signing and production infrastructure/secret provisioning are not accepted.
- This is an audit result, not a successful full-system acceptance certificate. No application source, API, database schema, environment file or provider configuration was changed. The defects found require coordinated billing/ownership work rather than a small presentation/configuration correction.

## Architecture and environment

Android `com.restaurant.viettable` uses `ApiClient` and `MobileRepository` through Gateway `:5100`, with mobile prefix `/api/mobile/v1`. Searches of `app/src/main` found no direct `5101–5105` calls. SignalR uses the same Gateway origin at `/hubs/mobile-notifications`. Web applications use Gateway, but browser UI acceptance was not rerun in this Android-focused regression pass.

Configured service databases are Catalog `RESTAURANT_CATALOG`, Orders `RESTAURANT_ORDERS`, Customers `RESTAURANT_CUSTOMERS`, Identity `RESTAURANT_IDENTITY`, Billing `RESTAURANT_BILLING`. Gateway has no DbContext/database ownership.

**Important exception:** runtime SQL metadata contains synonyms back to shared `RESTAURANT` in Orders, Customers and Billing. Merely searching C# for qualified database names misses these. `BillingDbContext.Bills` uses `dbo.Bills`, which is a runtime synonym for `[RESTAURANT].[dbo].[Bills]`; `CashierController` actively reads/writes that DbSet. Complete physical ownership is therefore **not established**. Existence of other synonyms does not alone prove their use by a service.

Environment: Windows, JDK `D:\Quang\Android1\jbr`, Android minSdk 24/targetSdk 37, Samsung SM-A528B (`R5CRC14LFYD`), USB reverse `tcp:5100 tcp:5100`. Eight application/infrastructure containers were running. SQL Server reports all six RESTAURANT databases ONLINE. RabbitMQ diagnostics ping succeeded. Windows `C:\Program Files\dotnet` has runtime directories but no installed SDK (`dotnet --list-sdks` returned no entries).

## Automated results

| Check | Result | Evidence / limits |
|---|---|---|
| Android debug build | PASS | `:app:assembleDebug`; APK 39,700,132 bytes / 37.86 MiB |
| Android unit tests | PASS | `:app:testDebugUnitTest`; XML totals 31 tests, 31 pass, 0 failure/error/skip |
| Android lint | PASS | `:app:lintDebug`; 0 errors, 1,188 warnings |
| Device regression | PASS | `scripts/test-android-instrumentation.ps1 -Tier All -RunExistingSessionSecurity`; 11 reported, 11 executed/pass, 0 skipped/fail |
| Release build/lint | PASS | `:app:assembleRelease :app:lintRelease -PVIETTABLE_API_BASE_URL=https://gateway.release-check.invalid`; unsigned APK 34,791,878 bytes / 33.18 MiB |
| Negative HTTPS gate | PASS | `validateReleaseApiUrl` rejected `http://127.0.0.1:5100` as expected |
| B0 source checks | PASS | 72 screens, 27 rules and baseline route checks passed |
| B0 .NET tests | BLOCKED_EXTERNAL | Script stopped because .NET SDK is absent; no xUnit pass is claimed |
| B1 | PASS | `test-b1.ps1 -KeepTestCustomer`: issuance, rotation, reuse/family revocation, password change, logout, reset/login |
| B2 | PASS | `test-b2.ps1 -KeepTestCustomers`: profile ownership/validation/settings/loyalty |
| B3 | PASS | `test-b3.ps1 -KeepTestData`: availability, real table, create/replay/conflict/change/preorder/cancel |
| B4 | FAILED | `-KeepTestData`: first four groups passed; walk-in fixture hit `table_session_active` for the second selected table |
| B5 | FAILED | `-KeepTestData`: initial table fixture hit `table_session_active`; no full B5 pass in this run |
| B6 | BLOCKED_EXTERNAL | `-KeepTestData`: quote/replay, voucher/points, payment lock and method checks passed; online payment stopped at `payment_provider_unavailable` |
| Split bill | BLOCKED_EXTERNAL | `test-split-bill.ps1 -KeepTestData`: creation, exact total, stable replay, duplicate guard and reload passed; online part payment unavailable |
| B7 | BLOCKED_EXTERNAL | `-KeepTestData`: favorites, FAQ/support idempotency, notification ready-event/read/ownership passed; paid-invoice review fixture stopped at unavailable provider |

Build tasks used Gradle's valid up-to-date outputs where applicable. Instrumentation rebuilt the physical-device debug configuration and installed with `install -r`; no app-data clearing/uninstall was performed. The HTTPS validation origin was supplied only on the command line and never called or committed as application configuration.

Lint warning classes: HardcodedText 499; UnusedResources 221; SetTextI18n 204; ContentDescription 88; Overdraw 71; UseCompoundDrawables 24; NotifyDataSetChanged 19; IconLocation 16; UseCompatTextViewDrawableXml 14; DefaultLocale 9; remaining minor categories 23. These are not all cosmetic: remaining accessibility labels and locale consistency need targeted follow-up. No blanket suppression was added.

### Backend fixture and data limitations

The legacy scripts delete test data using SQL by default. All executed B1–B7/split scripts used their `KeepTest*` option, disabling those deletion blocks. No SQL DELETE, table reset, database reset, volume deletion or environment modification was performed. Test accounts and business rows remain; B3 cancelled its reservation through the API. B4 created a checked-in session before its fixture failed. B6/B7/split created additional test sessions/quotes; these remain and can occupy tables. This is explicitly **not** a clean-room test fixture.

B4/B5 select Catalog `isAvailable` tables without verifying active Orders sessions. B6/B7/split already try candidates until Orders accepts one. The B4/B5 failures are reproducible fixture limitations, not evidence that the active-session conflict guard is broken. No existing customer session was reset to make a test pass. Fresh B6/B7/split sessions issued in the audit wrapper were logged out; retained backend records are still present. No public account deletion API exists.

The provider test wrapper was inspected but not run: it reconfigures/recreates Billing/Gateway using locally generated merchant credentials and calls scripts with SQL cleanup enabled. This audit did not enable a fake production provider or represent locally signed callbacks as merchant E2E.

## Samsung runtime

The 11 executed device tests cover signed-out and authenticated cold launch, real branch/menu, background/foreground, login success/failure, logout, FAQ reload without duplicates, denied-camera manual QR invalid/valid flow and recreation, local network policy, real Keystore corruption/migration/rotation, concurrent 401 refresh/relogin, and existing secure-session profile + SignalR connection.

ADB reported an active application process and retained reverse tunnel. SignalR logs show successful connections during the test run. No AndroidRuntime exception was present in the inspected log; `dumpsys activity lastanr` reported no ANR since boot. These checks do not prove every screen or every lifecycle path.

Network outage/recovery was **not rerun**: automatic execution approval rejected the combined Gateway stop/restore command with `blocked by policy` before execution. Gateway remained running. Prior UI-polish evidence exists, but is historical and is not counted as fresh acceptance here. Physical QR decode, deep TalkBack and complete reservation-to-payment Android E2E remain manual acceptance.

## Security and release configuration

- `SecureTokenStore` persists one encrypted token pair using AndroidKeyStore AES-256-GCM with random IV and AAD. `TokenVault` handles verified legacy migration, failure ordering and fail-closed corruption; logout clears blob/key. Device tests exercised these paths. No plaintext token persistence fallback was found.
- `ApiClient` restricts authenticated requests to the configured Gateway scheme/authority, disables automatic redirects, and shares synchronized refresh with SignalR. No `TrustAll`, `HostnameVerifier`, `SSLContext` or `X509TrustManager` bypass was found in production Android source.
- Debug network policy allows only named local HTTP hosts; release policy denies cleartext and trusts system CAs. Merged release manifest has `usesCleartextTraffic=false`. APK analyzer reports `debuggable=false`; defined-package scan found no AndroidJUnitRunner or the checked instrumentation test classes packaged in release.
- No release signing configuration/key was supplied; artifact is unsigned. Version remains code 1/name 1.0; production version/distribution policy is undecided. Minification remains off; no R8 policy was changed.
- Tracked text scan found zero JWT-shaped runtime tokens and zero private-key PEM headers. No tracked `.env`, signing keystore, APK or runtime log file was found. Credential-like matches were development configuration, environment references, test code and test-only values. Development JWT fallback and RabbitMQ guest credentials are tracked defaults, **not production credentials**; they must never be used for public deployment. No secret values are reproduced here. Pattern scanning is not proof that arbitrary historical secrets can never exist.
- Google source validates the Google credential in Identity using configured audience and returns VietTable JWT through Gateway. Real OAuth consent/client/account acceptance is BLOCKED_EXTERNAL; a public OAuth client ID is not a client secret.
- VNPAY source implements HMAC-SHA512, constant-time signature comparison, stored amount/currency validation, transaction/state replay rejection and display-only Return URL. Valid merchant/IPN E2E was not run; runtime currently rejects online creation as unavailable.
- Compose uses Development, published downstream ports, SA database access, unencrypted internal SQL, guest RabbitMQ and no container healthchecks/general restart policies. Domain readiness endpoints exist, but `docker ps` Up is not a Docker healthcheck. This compose is a local development environment and must not be published as production. Trusted proxy/forwarded-header topology and restricted service ingress remain unaccepted.
- Schema setup is mixed: `docker/db-init.sh`, owned-table materialization scripts, runtime bootstrap DDL and some migration files. It also creates shared-database synonyms. No production migration/rollback/backup-restore acceptance was demonstrated.

## Concrete defects and risks

| ID | Risk | Evidence and impact | Required follow-up |
|---|---|---|---|
| F01 | RELEASE_BLOCKER | Mobile ONSITE payment can be created/read as PENDING without provider fields, but its success/finalization path is absent. `TryFinalizeAsync` is called only by VNPAY IPN; `CashierController` completes legacy Bills/CheckoutCommands, not MobilePayments/Invoices. Runtime audit session 2026/payment 1030 remained PENDING. This does not establish cashier-to-mobile settlement. | Integrate authenticated cashier confirmation with canonical mobile payment, invoice, session and split-part settlement; add regression for single/split ONSITE completion. This is coordinated business work, not a small audit fix. |
| F02 | HIGH | Billing actively maps/writes `Bills`, which runtime metadata resolves through a synonym into shared RESTAURANT. Other service shells retain legacy synonyms. | Complete ownership migration with data-preserving plan and verify actual SQL dependencies; no schema rewrite during audit. |
| F03 | RELEASE_BLOCKER | No accepted public Gateway HTTPS/TLS/proxy deployment or production signing. Dev compose exposes internal services and uses development credentials/settings. | Provision production ingress/isolation/secrets and signing outside this task. |
| F04 | HIGH | Full B4/B5 regression is not deterministic against retained sessions; B6/split/B7 finalization/review CRUD cannot finish with provider disabled; B0 cannot execute without SDK. | Supply safe lifecycle fixtures/API cleanup and .NET SDK host; verify settlement before declaring full acceptance. |
| F05 | MEDIUM | Google and VNPAY external E2E are absent. Whether either is mandatory for first release is a product decision, not established by source. | Explicitly define release feature set; run required provider acceptance with real configuration. |
| F06 | MEDIUM | Physical QR decode, deep TalkBack, complete Android lifecycle and fresh network outage recovery not accepted in this pass. | Execute manual/device acceptance checklist and permitted recovery test. |
| F07 | LOW | Lint warnings remain (including accessibility/localization); resource cleanup must be selective. | Continue targeted fixes, no blanket suppression. |
| F08 | FUTURE | Version/distribution/R8 policy and repeatable migration/restore drills remain undefined. | Assign release ownership and document/run procedures before actual deployment. |

## Feature acceptance matrix

Status reflects the stated scope of each row, not an assertion that source presence proves E2E. Only PASS, BLOCKED_EXTERNAL, MANUAL_ACCEPTANCE, FUTURE and FAILED are used.

| Area | Source | Automated Test | Runtime | External Dependency | Status | Risk |
|---|---|---|---|---|---|---|
| Android → Gateway contract | Repository/ApiClient and mobile controllers | B1–B3/device | Gateway routes exercised | None locally | PASS | LOW |
| Full service ownership | Contexts/bootstrap | Metadata inspection | Billing Bills synonym confirmed | Migration plan | FAILED | HIGH |
| Backend health | healthz/readyz | 12 HTTP probes | All 200; SQL ONLINE; RabbitMQ ping | Local Docker | PASS | LOW |
| Android build/unit/lint | Gradle | 31 unit; lint no errors | APK installed | JDK/SDK available | PASS | LOW |
| Password auth | Identity B1 | B1 + device | Login/rotation/reuse/logout/reset | None locally | PASS | LOW |
| Token security | SecureTokenStore/TokenVault | JVM + real Keystore device | Existing secure session works | Device Keystore | PASS | LOW |
| Release HTTPS policy | Gradle/network config | Positive/negative build checks | Merged manifest/APK inspected | Real HTTPS E2E missing | PASS | LOW |
| Google Sign-In | Credential Manager → Gateway → Identity | Configuration/source only in this pass | No real OAuth acceptance | Google Cloud/account | BLOCKED_EXTERNAL | MEDIUM |
| Branch/menu | Canonical API, hidden unsupported metadata | Device + B3 | Real menu/branches loaded | None locally | PASS | LOW |
| Reservation/preorder API | Customers/Orders via Gateway | B3 pass | Create/change/cancel/replay | Existing catalog | PASS | LOW |
| Full reservation Android E2E | Existing UI/repository | Not covered end to end | Not accepted | Stable lifecycle fixture | MANUAL_ACCEPTANCE | MEDIUM |
| QR manual | Gateway validation | Device pass | Permission denied + invalid/valid | Real QR fixture | PASS | LOW |
| QR physical camera | CameraX/ML Kit; atomic guards/lifecycle | Source + build | Physical decode not observed | Printed/displayed QR | MANUAL_ACCEPTANCE | MEDIUM |
| Check-in/dining | Canonical Orders session | B4 first four groups pass | Check-in/replay/ownership pass; walk-in suite fixture fails | Available table | MANUAL_ACCEPTANCE | MEDIUM |
| Order/service full regression | B5 + Android repository | B5 blocked by occupied fixture; B7 ready event passed | Full multi-round/cancel not rerun successfully | Available table fixture | MANUAL_ACCEPTANCE | HIGH |
| SignalR connection/security | JWT hub, singleton/reconnect, REST resync | Device tests pass | Connected; background/foreground | None locally | PASS | LOW |
| SignalR event/fallback recovery | Event handlers and connected polling cancellation present | Policy unit tests; outage attempt rejected | No new outage recovery result | Permitted recovery run | MANUAL_ACCEPTANCE | MEDIUM |
| Quote/voucher/loyalty/payment lock | Billing server totals | B6 pre-provider assertions pass | Real quote/replay/discount/lock | None locally | PASS | LOW |
| Split creation/reload | Amount-only capability | Split pre-provider assertions pass | Sum/replay/duplicate/read pass | None locally | PASS | LOW |
| Split settlement/retry/all paid | Billing parts/Finalize + Android | Cannot finish current script | All-paid closure not accepted | Provider or completed ONSITE bridge | MANUAL_ACCEPTANCE | HIGH |
| ONSITE full payment | Missing mobile completion bridge | Create/read PENDING only | No canonical completion path | F01 | FAILED | RELEASE_BLOCKER |
| VNPAY | Signed URL/IPN, Return display-only | Current regression stopped unavailable | No merchant transaction | Merchant credential/public IPN | BLOCKED_EXTERNAL | MEDIUM |
| Invoice issuance E2E | Billing Finalize exists | No new paid fixture | Not accepted this pass | Settlement | MANUAL_ACCEPTANCE | HIGH |
| FAQ | Real support/faqs endpoint | B7/device pass | Read/reload nonduplicate | None locally | PASS | LOW |
| Review CRUD | Eligibility/ownership/rating/comment validation source exists | B7 pre-eligibility guard only; paid fixture blocked | Full CRUD not accepted this pass | Paid invoice test fixture | MANUAL_ACCEPTANCE | HIGH |
| Favorites/notifications/support | Gateway → Customers | B7 pre-payment assertions pass | Real writes/read/ownership | None locally | PASS | LOW |
| Production mock audit | src/main scan | No applyScenario/MockData/DS-001 | No fabricated state introduced | None | PASS | LOW |
| Basic accessibility | Critical labels/targets/text states | Static/lint review | Prior large-font evidence, no deep TalkBack rerun | Manual acceptance | MANUAL_ACCEPTANCE | MEDIUM |
| Release APK configuration | HTTPS, unsigned, minify off | Build/lint/package pass | debuggable=false; no checked test classes | Signing absent | PASS | LOW |
| Production signing/deployment | Not provisioned | Not accepted | No deploy attempted | Key/domain/TLS/proxy/secrets | BLOCKED_EXTERNAL | RELEASE_BLOCKER |
| Version/R8/unused resources | Existing 1 / 1.0, optimization off | No changes | Not a functional blocker alone | Release policy | FUTURE | FUTURE |

## UI/source audit notes

Exact production placeholder search found only the model comment `DiningSession.java` describing an example table name; it is not rendered and was not removed. No production `applyScenario`, `MockData`, DS-001, fake invoice, demo promotion or TLS bypass match was found. QR keeps manual input, atomic frame/consumed guards, permission/settings handling, analyzer clearing and camera unbinding. Android consumes server quote/payment/invoice/split objects and does not generate successful payments or business IDs locally.

`DisplayFormatters` is applied to critical timestamp displays with unit coverage. Monetary UI uses existing VND helpers; full visual acceptance of every amount/date on all screens is not claimed. Status text remains alongside color. The basic source accessibility pass is limited to critical controls; full TalkBack certification is not claimed. Contract baseline still labels some implemented B1–B3 endpoints as Planned/Partial and describes a universal list envelope, while actual endpoints use a mix of arrays/envelopes understood by the Android repository. Treat the endpoint implementations/runtime as evidence; schedule documentation reconciliation, not an API breaking change.

## Release checklist and next action

- [x] Git baseline clean and preserved; no remote/push.
- [x] Android debug/release build, 31 unit tests and 11 device tests pass.
- [x] Backend HTTP readiness, SQL and RabbitMQ verified.
- [x] Token/HTTPS/manifest/placeholder/secret-pattern audit completed.
- [ ] Resolve F01 mobile ONSITE settlement and prove invoice/session/split closure.
- [ ] Resolve F02 active shared-database dependency with data-preserving migration.
- [ ] Run deterministic B0–B7/split regression, including paid-invoice review CRUD, without SQL deletion.
- [ ] Accept physical camera QR, deeper TalkBack, full Android lifecycle and network recovery.
- [ ] Provision production signing, HTTPS ingress and isolated production services/secrets.
- [ ] Decide whether Google/VNPAY are mandatory release features and accept their E2E accordingly.

Recommended next action: address F01 first with a narrowly scoped cashier-to-mobile settlement task and regression for ONSITE single/split payment, then complete database ownership and rerun final acceptance. Do not publish the current dev compose or mark the current code baseline ready solely because Android builds/tests pass.

## Follow-up evidence, 2026-09-23

The findings above are the historical audit snapshot. F01 was closed by [ONSITE payment acceptance](ONSITE_PAYMENT_ACCEPTANCE.md). F02 is closed **for Billing only** by the [Billing ownership migration](BILLING_OWNERSHIP_MIGRATION.md): the active Bill mapping is local, all 30 Billing synonyms are retired, a real Gateway legacy cashier checkout wrote only to Billing, and post-cutover payment/review/backend regressions passed. The shared Bills source remains untouched. Other service synonyms and global ownership, external provider acceptance, release signing, and production HTTPS deployment remain open; this addendum does not change the overall release block.

The subsequent [full database ownership audit](DATABASE_OWNERSHIP_AUDIT.md) inventories every service and live SQL DB. It confirms Catalog/Identity/Billing and Gateway scoped PASS, but Orders has 26 and Customers 30 shared-DB aliases recreated by fresh setup. No active alias consumer was identified by source/metadata inspection; the strict global ownership check still FAILS. This is a later finding, not a rewrite of the original snapshot.
