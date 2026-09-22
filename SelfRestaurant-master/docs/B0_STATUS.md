# B0 Status - 2026-09-15

## Completed

- Frozen `/api/mobile/v1` conventions, ownership, endpoint families and state machines.
- Traced all 72 Android screens to target API/module/service owner and current coverage.
- Traced all 27 business rules to enforcement owner and acceptance test.
- Added stable `MobileApiError` contract without changing existing web error responses.
- Added `SelfRestaurant.Backend.ContractTests` to the solution.
- Added automated source contract checks and optional runtime health checks in `scripts/test-b0.ps1`.
- Removed the three compiler nullability warnings found during the baseline audit.
- Verified solution restore/build: 0 warnings, 0 errors.
- Verified source checks: 72 continuous screens, 27 continuous rules, required contract conventions and baseline routes.
- Verified `docker compose up -d --build`: all six backend/Gateway images build and start successfully.
- Verified database bootstrap completes before APIs start; service-owned Catalog, Identity and Orders schemas are initialized idempotently.
- Verified all six `/healthz` and all six `/readyz` endpoints return HTTP 200 through Docker ports 5100-5105.
- Verified xUnit contract suite: 14 passed, 0 failed, 0 skipped.

## Baseline coverage

- Screens: 20 Existing, 22 Partial, 28 Planned, 2 UI-only.
- Rules: 6 Existing, 13 Partial, 6 Planned, 2 UI-only.

B1 starts only after the manual contract decisions in `B0_TEST_CHECKLIST.md` are accepted.
