# VietTable API — Vercel Preview staging

## Scope and current status

Phase D targets only `VietTable.CloudHost` in the Vercel project `viettable-api`, using a **Preview** deployment and Supabase staging. The Vercel project is linked to `SelfRestaurant-master`; no deployment has been started and there is no Preview URL yet. Deployment is paused until the owner enters the required Preview Secrets in Vercel.

Do not deploy the Chef, Cashier, Admin, or Customer frontends in this phase. Do not change Android's default API URL, configure Railway, or deploy to Production.

## Local preparation evidence

- `Dockerfile.vercel` publishes the .NET 8 `VietTable.CloudHost` only. It reads Vercel's `PORT`, binds to `0.0.0.0`, runs as the non-root `app` user, and contains no staff frontend bundles.
- The validated public Supabase root CA is included at `/staging-ca/supabase-ca.crt`; it is not a private key or secret.
- Local Docker build: PASS. Image inspection: CA present; Chef, Cashier, and Admin SPA assets absent.
- CloudHost build: PASS (warnings were caused by concurrent builds sharing the existing SDK container output directories).
- CloudHost tests: 5/5 PASS.
- Backend contract tests: 141 passed, 1 skipped, 0 failed.
- DPAPI secret-safety scan: PASS; secret values were checked in memory only and were not emitted. `.idea` was excluded.
- These are local preparation checks only. No Vercel runtime, health, database, Storage, auth, recovery, or WebSocket acceptance has passed yet.

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
```

`CloudHost__ExposeStaffWeb` keeps the staff SPAs out of this API-only deployment. `Database__ApplyMigrations` is disabled: schemas are already migrated, and application startup must not mutate them.

## Preview Secrets to enter in Vercel

Set each variable below with **Type: Secret** and **Environment: Preview**. Enter values directly in the Vercel dashboard; never place them in chat, source, documentation, command history, or `.env` files.

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

## Deployment and acceptance status

Deployment remains **NOT STARTED — waiting for Preview Secrets**. Once all nine Secrets are entered, deploy from `SelfRestaurant-master` with the pinned Vercel CLI, without `--prod`:

```powershell
npx.cmd --yes vercel@62.4.0 deploy --yes --project viettable-api
```

Record only the safe Preview URL. Then test `/healthz`, `/readyz`, staged mobile APIs and staff auth, Supabase Storage, CloudHost recovery authorization/durability, cold start, filesystem independence, and SignalR WSS (`/hubs/mobile-notifications`). If multi-instance SignalR fan-out cannot be proven, document it as `UNVERIFIED`, not PASS.

| Acceptance area | Status |
| --- | --- |
| Local CloudHost Docker image | PASS |
| Preview deployment | WAITING FOR USER-ENTERED SECRETS |
| Health / readiness on Vercel | NOT RUN |
| Supabase runtime database / Storage | NOT RUN |
| Customer / staff authentication and API E2E | NOT RUN |
| Durable recovery and authorization on Vercel | NOT RUN |
| Cold start / restart | NOT RUN |
| SignalR WSS and reconnect | NOT RUN |
| SignalR cross-instance fan-out | NOT RUN |

Official platform references: [Dockerfile-backed Vercel Functions](https://vercel.com/kb/guide/docker), [Vercel WebSocket public beta](https://vercel.com/changelog/websocket-support-is-now-in-public-beta), [Supabase database connections and SSL](https://supabase.com/docs/guides/database/connecting-to-postgres).
