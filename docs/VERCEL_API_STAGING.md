# VietTable API — Vercel Preview staging

## Scope and current status

Phase D targets only `VietTable.CloudHost` in the Vercel project `viettable-api`, using **Preview** and Supabase staging. On 09/10/2026 the user explicitly approved one harmless static Production bootstrap outside the repository, then a mandatory static Preview probe before uploading CloudHost. Both gates passed. The API is now deployed as a real Docker-backed Preview, not an empty READY deployment.

**Follow-up acceptance — 09/10/2026: REALTIME + RECOVERY: PASS** for actual multi-instance event delivery, reconnect/REST resync and protected **manual** durable recovery after redeploy. Current Preview: [CloudHost private backplane](https://viettable-6ezq8ysww-hienkhanhvu981-7145.vercel.app), `dpl_2n6LXbAyfocDMgG5DdTid2u6N5wK`. See [CLOUD_REALTIME_RECOVERY_ACCEPTANCE.md](CLOUD_REALTIME_RECOVERY_ACCEPTANCE.md) for root-cause reproduction, private-channel/RLS security, event counts, isolated fault harness and repeated/concurrent recovery. **Automatic unattended recovery scheduling remains UNVERIFIED.** The older PARTIAL evidence below is retained as history, not the current acceptance state.

Do not deploy the Chef, Cashier, Admin, or Customer frontends in this phase. Do not change Android's default API URL, configure Railway, or deploy application code to Production. Production must remain the harmless bootstrap.

## Local preparation evidence

- `Dockerfile.vercel` publishes the .NET 8 `VietTable.CloudHost` only. It reads Vercel's `PORT`, binds to `0.0.0.0`, runs as the non-root `app` user, and contains no staff frontend bundles.
- The validated public Supabase root CA is included at `/staging-ca/supabase-ca.crt`; it is not a private key or secret.
- Added `SelfRestaurant-master/.vercelignore` after CLI dry-run showed that default upload selection included local `.env` and generated artifacts. The verified upload manifest now contains 535 files / 20,589,006 bytes, includes the required CloudHost project, `vercel.json`, public CA and Billing SQL, and excludes environment files, private keys, frontend projects, `bin` and `obj`. Dry-run does not upload file contents; only the filtered manifest was deployed.
- Added `SelfRestaurant-master/vercel.json`: one container service `api`, root `.`, entrypoint `Dockerfile.vercel`, and catch-all ingress targeting that service. Actual remote evidence showed that Dockerfile auto-detection alone yielded an empty runtime output; explicit configuration made Vercel build/push/start the container. No additional service or frontend was introduced.
- Local Docker build: PASS. Image inspection: CA present; Chef, Cashier, and Admin SPA assets absent.
- CloudHost build: PASS (warnings were caused by concurrent builds sharing the existing SDK container output directories).
- CloudHost tests: 5/5 PASS.
- Backend contract tests: 141 passed, 1 skipped, 0 failed.
- DPAPI secret-safety scan: PASS; secret values were checked in memory only and were not emitted. `.idea` was excluded.
- These local test results are a preparation baseline, not a substitute for the remote acceptance evidence below. Local re-run attempts in this task could not execute: the host has only a .NET runtime (no SDK), and Docker Desktop's Linux engine is stopped. Remote Docker publish/build completed successfully.

## Preview Config variable names

These non-secret Preview Config variables have been set in Vercel (values intentionally omitted):

```text
Database__Provider
Database__ApplyMigrations
Database__SeedProfile
CloudHost__EnvironmentNamespace
CloudHost__ExposeStaffWeb
MediaStorage__Provider
MediaStorage__Supabase__ProjectUrl
MediaStorage__Supabase__Bucket
MobileJwt__Issuer
MobileJwt__Audience
Realtime__Provider
```

`CloudHost__ExposeStaffWeb` keeps the staff SPAs out of this API-only deployment. `Database__ApplyMigrations` is disabled: schemas are already migrated, and application startup must not mutate them.

## Preview Secrets set in Vercel

Each variable below is set with **Type: Secret** and **Environment: Preview**. Values are write-only in Vercel and are intentionally omitted here. They were transferred from the local current-user DPAPI store through process stdin; no plaintext temporary file or command-line value was used.

```text
ConnectionStrings__CatalogDb
ConnectionStrings__OrdersDb
ConnectionStrings__CustomersDb
ConnectionStrings__IdentityDb
ConnectionStrings__BillingDb
MediaStorage__Supabase__ServiceKey
MobileJwt__SigningKey
CloudHost__DataProtectionCertificate
CRON_SECRET
```

The five database variables must use the already-validated Supabase **transaction pooler** configuration and the corresponding owner/runtime identity. They must verify TLS using `/staging-ca/supabase-ca.crt`; do not use a local SQL Server connection, direct database endpoint, or privileged bootstrap credential. The Data Protection value must be the Base64 encoding of the passwordless PFX with a private key, as required by `CloudStaffConfiguration`. Use a new, sufficiently long recovery secret for `CRON_SECRET`; the recovery value is supplied as a Bearer token only to the protected manual recovery endpoint.

## Runtime constraints

- CloudHost explicitly disables legacy persistent workers and RabbitMQ, and uses durable PostgreSQL recovery state. Recovery is manually invoked through the authenticated endpoint for this Preview phase.
- Do not configure Vercel Cron for Preview. The Preview deployment must not rely on Cron; Hobby schedules are also not suitable for frequent recovery.
- Vercel's Dockerfile-backed HTTP server and WebSocket capabilities must be verified on the actual Preview deployment. SignalR is not accepted based on local tests alone.
- No persistent local filesystem is assumed. Supabase Storage remains the media store; transient local files must not become canonical state.
- Stop if Vercel requests an upgrade, paid add-on, or Production-only secret/configuration.

## Deployment execution and target gate

The bootstrap directory was outside the repository, linked to the existing project and verified to upload only `index.html`. It contains the approved two sentences and `noindex,nofollow`; no application source or credentials. The following commands were executed there:

```powershell
npx.cmd --yes vercel@62.4.0 deploy --yes --target production --project viettable-api
npx.cmd --yes vercel@62.4.0 deploy --yes --target preview --project viettable-api
```

| Purpose | Deployment ID | Inspected target/status |
| --- | --- | --- |
| Production bootstrap, retained | `dpl_HtbM5wEHbj4HcfQRW4RJCya6JPV5` | production / READY |
| Mandatory static Preview probe | `dpl_4zQMLrcnfba6WvAtiAgPNdHckjxJ` | preview / READY |
| CloudHost auto-detection attempt | `dpl_EaVPHuio5K3Lk2Rsc9knCp1qGaWV` | preview / READY, but empty runtime output and HTTP404 |
| Explicit container configuration | `dpl_F26MGu3Pp52wTjVfdrbTa48ga5ha` | preview / READY, real Docker build; startup HTTP500 |
| Repaired DB Preview configuration; main API E2E | `dpl_AX22YCgPNPvJN13BsjUBiiHa73WK` | preview / READY; health/readiness HTTP200 |
| Fresh container redeployment / persistence check | `dpl_4kZNurVoxMvTuLeyPGzymvrgkywo` | preview / READY; real Docker publish/build/push |

Production: [retained bootstrap](https://viettable-idkc9iz7l-hienkhanhvu981-7145.vercel.app), alias [viettable-api.vercel.app](https://viettable-api.vercel.app). HTML-only HTTP200; `/healthz`, `/readyz` and mobile branches HTTP404. Production alias was not moved to CloudHost.

Probe: [static Preview](https://viettable-2cks1vn7z-hienkhanhvu981-7145.vercel.app). Its `vercel inspect --json` target was verified as `preview` before any CloudHost upload. CLI deploy output can represent Preview as `target:null`; actual inspect normalizes the target to `preview`. No Production alias was assigned.

Main E2E: [accepted API Preview](https://viettable-16r26dk9i-hienkhanhvu981-7145.vercel.app).

Historical fresh-container baseline: [Preview before realtime fix](https://viettable-bl5keev4y-hienkhanhvu981-7145.vercel.app). Current deployment/evidence is linked at the top of this document.

Executed from `D:\Quang\VietTable\SelfRestaurant-master`:

```powershell
npx.cmd --yes vercel@62.4.0 deploy --yes --target preview --project viettable-api
npx.cmd --yes vercel@62.4.0 deploy --yes --target preview --force --with-cache --project viettable-api
```

### Root causes fixed

1. Dockerfile auto-detection alone produced `builds[].output=[]` and `/healthz` HTTP404 despite READY. Explicit `services.api.runtime=container`, `entrypoint=Dockerfile.vercel` and service ingress fixed packaging. Remote build logs prove .NET publish, OCI image push and non-root container execution.
2. The first container startup failed with `ArgumentException: Couldn't set max pool size`; Vercel returned `FUNCTION_INVOCATION_FAILED`. Existing manually constructed DB values used a keyword not accepted by the installed Npgsql parser. All five Preview DB values were rebuilt and locally parsed using that actual `NpgsqlConnectionStringBuilder`, preserving owner identities/passwords and transaction-pooler settings. This was an environment/configuration repair, not a service rewrite. Subsequent health/readiness and owner-backed API calls succeeded.
3. A temporary staff-auth assertion expected `session.user`, whereas the actual DTO is `session.staff`. The verifier was corrected; no application auth code was changed.
4. The first temporary raw WSS verifier did not send SignalR protocol pings during a long REST test. Heartbeat was added to that out-of-repository verifier. A later READY event observation still timed out; this is not accepted as event delivery PASS.

## Historical remote acceptance evidence — 09/10/2026, before realtime/recovery follow-up

Requests used the exact protected Preview HTTPS origin, fresh in-memory development OIDC for Deployment Protection, and separate application JWT/cookies. No localhost fallback or protection disabling was used. No `.env` was pulled; a stale ignored local OIDC override was avoided by running verification from the linked temporary directory.

### Database, auth and API E2E

- Five existing runtime-owner credentials connect to the accepted Supabase transaction pooler with verified TLS/public CA. No migrations, bootstrap import or password/account reset ran on Vercel.
- Customer: existing QA password login, refresh, JWT profile, reservation history and order history PASS.
- Admin/Chef/Cashier: cookie login, session DTO role/branch and dashboard PASS. Main Chef/Cashier are scoped to branch3; a second Cashier belongs to branch2. Accounts/passwords were not printed or written into test files.
- Catalog: 3 branches, 47 tables, 10 categories and 52 dishes PASS before the temporary Storage test; 52 dishes restored after cleanup.
- Customer token cannot perform Chef, Cashier Service Request or payment mutations (HTTP401 under the staff-cookie boundary). Chef cannot perform Admin mutation (HTTP401).
- Actual QA session13 / branch3 / table36; one order28 using a real lowest-priced available Catalog dish. Same-key order replay returned the same order; changed payload with the same key returned409. Chef start committed PREPARING; Chef ready and Customer received committed READY then SERVING, confirmed by canonical REST.
- Service Request13 WATER: OPEN → ACKNOWLEDGED → COMPLETED; branch2 Cashier mutation returned403 and Customer staff mutation401. REST confirmed COMPLETED.
- ONSITE/CASH/TABLE payment13: PENDING, same-key replay stable, wrong-branch Cashier403 and Customer401; payment remained PENDING after rejected mutations. Correct-branch Cashier confirmation committed SUCCEEDED, invoice13 total5,000VND, session13 CLOSED and table36 physically available. No manual recovery was needed for this normal close/release path.
- Authenticated recovery repeated twice retained invoice13; anonymous recovery returned401. No financial/history rows were SQL-deleted or reset.

### SignalR

- Anonymous mobile negotiate401; Customer on staff hub403; authorized mobile negotiate200.
- Real mobile and staff WSS upgrade/JSON handshake PASS; staff SubscribeBranch accepted its own branch and denied the foreign branch.
- Real reconnect handshake PASS.
- `orderUpdated READY`: not observed in the temporary verifier's 20-second event window after real Chef READY, even with protocol heartbeat. REST state was correct. Cause of cloud event-delivery failure is not conclusively isolated; process-local hub groups without a distributed backplane are a scaling risk, not a proven diagnosis from this test alone.
- `serviceRequestUpdated`: UNVERIFIED; the observer was no longer usable after the earlier event timeout. Both canonical staff transitions passed, but REST success is not evidence of a pushed event.
- **MULTI-INSTANCE: UNVERIFIED**. No claim of cloud realtime PASS; no large realtime rewrite, Redis purchase or architecture change was made.

### Storage

- Multipart upload through authenticated CloudHost Admin API PASS, producing temporary dish57 and an immutable object in Supabase Storage. Only a tiny QA PNG was used, not a production fallback.
- Public read bytes/checksum matched. The same object and Catalog reference were read through a fresh Preview after actual Docker redeployment: PASS; no laptop/local filesystem dependency.
- Valid anonymous CloudHost multipart upload401. Unauthenticated direct Supabase object write400 (this HTTP check is not an anon-key RLS proof; prior SQL/policy isolation evidence remains in `SUPABASE_ACCEPTANCE.md`).
- Exact temporary dish57 removed through the legitimate Admin API and exact temporary object removed through authenticated Storage API. Canonical52dishes and existing media were preserved. Test scripts/receipts remained outside Git.

### Recovery, restart and remaining gates

- Normal fast path, authenticated bounded endpoint and duplicate-invoice protection: PASS on the main API Preview.
- Fresh container Preview: `/healthz` and `/readyz` HTTP200. A Customer refresh credential issued by the earlier deployment was accepted by the new deployment. The original Admin/Chef/Cashier cookie values also decrypted successfully with the same roles/branches on the new instance; cookies were transferred only in memory to account for the different generated hostname, not printed or persisted. This proves shared Data Protection persistence across the two actual deployments, not browser cookie sharing between hostnames.
- New deployment reads session13 CLOSED, order28 COMPLETED and invoice13 unchanged; anonymous recovery401 and repeated authenticated recovery preserve that invoice. Production alias remains the approved HTML-only `noindex` bootstrap. **Committed-state and auth continuity across redeployment: PASS.**
- Remote pending-work fault injection: NOT RUN. The three test flags (`TestFailReadyProjection`, `TestFailAfterPaymentCommit`, `TestFailTableRelease`) require `IsDevelopment()`. The public container retains `ASPNETCORE_ENVIRONMENT=Production`; weakening this to activate failure paths or adding a public fault-injection API would exceed this small deployment-fix scope.
- Consequently pending-work survival/recovery after a remote crash/redeploy is **UNVERIFIED**, not inferred from successful normal payment or a no-op recovery pass. Earlier local CloudRecovery fault/restart evidence is a baseline only, documented in `CLOUD_RECOVERY_MODEL.md`.
- No Preview Cron, legacy workers, RabbitMQ, paid add-on or Railway runtime was enabled.

## Historical wrong-target attempts

Before the separately approved bootstrap, `dpl_43g3Zn3PrWHQzSMvYuJuvveUcoXt` and `dpl_9BsqfpGnzhAHPHps69kTjYt1KnGE` were classified Production despite Preview intent. Both were deleted in that earlier attempt and the project had zero deployments. No API acceptance ran against them. They are not the retained, approved bootstrap.

Upstream context: [Vercel first-deployment notice](https://github.com/vercel/vercel/pull/17206), [Preview target classified as Production](https://github.com/vercel/vercel/issues/17069). The target gate is now resolved, not the current blocker.

## Security and Git

Nine application Secrets remain Preview-only; a final metadata-only check confirmed Type Secret, Preview-only targets and zero Production environment entries. Production is the harmless placeholder and has no application DB/JWT/Storage/recovery secrets. Secret transfer used DPAPI/in-memory values and process stdin; no plaintext credential file, command-line secret, token screenshot or secret report was created. Temporary verifiers/bootstrap/upload artifacts were outside the repository and excluded from commit.

Final checks: deployment JSON scope/parse PASS; `test-staging-secret-safety.ps1` PASS (2 candidate text files, 11 DPAPI values checked in memory; `.vercelignore` reviewed separately); bounded remote runtime-log scan PASS on 72 returned entries across the failed-startup, API E2E and redeployed container deployments, checking the actual DPAPI values and key/JWT/credential-URI patterns without emitting raw logs. This bounded scan is not a claim to have inspected every historical platform log. Real remote .NET Docker publish/build PASS; no new local unit-suite PASS is claimed while SDK/Docker are unavailable.

Changes are limited to `.vercelignore`, `vercel.json` and this deployment evidence document; no Android Base URL, frontend or business implementation changed. Secret-safety scan and `git diff --check` run before the scoped commit. The pre-existing modified `.idea/misc.xml` and staged `.idea/vcs.xml` remain untouched and excluded. No reset, amend, merge or push.

Official references: [Dockerfile-backed Vercel containers](https://vercel.com/docs/functions/container-images), [Services configuration](https://vercel.com/docs/services/config-reference), [Supabase database connections and SSL](https://supabase.com/docs/guides/database/connecting-to-postgres).
