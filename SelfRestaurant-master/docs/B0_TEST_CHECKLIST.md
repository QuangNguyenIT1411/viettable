# B0 Test Checklist

This checklist verifies the frozen baseline. It does not claim that B1-B7 mobile features are implemented.

## Automated local check

From `SelfRestaurant-master`:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-b0.ps1 -SkipDocker
```

Expected:

- Restore succeeds.
- Solution build reports 0 warnings and 0 errors.
- Source contract checks pass. On an unrestricted Windows host, xUnit contract tests also pass.
- Traceability test confirms exactly 72 screens and 27 rules with continuous IDs.
- Baseline route source checks pass.

If Windows Application Control blocks loading a newly built unsigned test assembly, the script detects that condition instead of reporting a false pass. To make actual xUnit execution mandatory, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-b0.ps1 -SkipDocker -RequireDotnetTestExecution
```

## Docker definition check

Create local environment configuration, change the example password, then run:

```powershell
Copy-Item .\.env.example .\.env
# Edit .env and replace YourStrong!Passw0rd with a local strong password.
docker compose config --quiet
```

Expected: exit code 0 and no configuration errors.

## Optional runtime health check

Start infrastructure and services:

```powershell
docker compose up -d --build
powershell -ExecutionPolicy Bypass -File .\scripts\test-b0.ps1 -SkipBuild -Runtime
```

Expected HTTP 200:

- Gateway `http://localhost:5100/healthz`
- Catalog `http://localhost:5101/healthz`
- Orders `http://localhost:5102/healthz`
- Customers `http://localhost:5103/healthz`
- Identity `http://localhost:5104/healthz`
- Billing `http://localhost:5105/healthz`

The corresponding `/readyz` endpoint on every port must also return HTTP 200.

Docker Desktop must be running in Linux-container mode. Do not commit `.env`.

## Manual contract review

- [ ] Confirm Android will call only `/api/mobile/v1` through Gateway.
- [ ] Confirm browser cookie APIs remain supported during migration.
- [ ] Confirm mobile auth will use access + rotating refresh tokens.
- [ ] Confirm VND is represented as an integer in the mobile JSON contract.
- [ ] Confirm API times are UTC and reservation display uses restaurant timezone.
- [ ] Confirm all mutation retries use `Idempotency-Key`.
- [ ] Confirm every error follows the `MobileApiError` envelope.
- [ ] Confirm the state machines match the Android UI labels.
- [ ] Confirm all 72 screens appear once in the traceability matrix.
- [ ] Confirm all 27 rules appear once in the traceability matrix.
- [ ] Confirm `Partial` and `Planned` are understood as unfinished work.

## B0 acceptance record

Record tester, date, command output and any contract decisions here or in an issue. B1 must not silently change a confirmed B0 contract; amendments must update the contract, traceability row and its test together.

- 2026-09-15: `docker compose up -d --build` completed successfully; `db-init` exited 0.
- 2026-09-15: source contract checks passed for 72 screens and 27 rules.
- 2026-09-15: xUnit result 14 passed, 0 failed, 0 skipped.
- 2026-09-15: `/healthz` and `/readyz` returned HTTP 200 on ports 5100-5105.
