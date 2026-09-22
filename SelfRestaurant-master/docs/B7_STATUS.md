# B7 Status - Customer Engagement

Status: **implemented and runtime-tested on 2026-09-16**

## Delivered

- Customer-scoped favorite add, list and removal with Catalog-owned dish validation.
- Paged notification inbox backed by the existing ready-dish event projection, including mark-as-read and ownership checks.
- Review create, list, update and delete with rating/image validation and customer ownership.
- Review eligibility is verified against a Billing-issued invoice, so an incomplete transaction cannot be reviewed.
- One review per invoice plus idempotent create/retry conflict handling.
- Public FAQ retrieval and authenticated support-request submission with validation and idempotency.
- Customers owns engagement persistence; Gateway performs cross-service orchestration without cross-domain database reads.

## Verification

- Docker B7 runtime gate passes favorites, notifications, completed-transaction reviews, FAQ and support requests.
- Cross-customer favorite, notification and review access is rejected.
- Test-created records are removed automatically unless `-KeepTestData` is supplied.
