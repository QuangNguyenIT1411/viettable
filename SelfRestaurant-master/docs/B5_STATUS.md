# B5 Status - In-session Ordering, Service Requests and Tracking

Status: **implemented and runtime-tested on 2026-09-15**

## Delivered

- Customers can submit one or more order rounds to an active Dining Session.
- Each round is a separate Orders-owned order linked to the same explicit session.
- Prices and availability come from Catalog; client-provided prices are not accepted.
- Order submission requires `Idempotency-Key` and a server-generated request hash; stable retries return the original order and changed payloads return `409 idempotency_conflict`.
- New order creation is blocked for closed and `PAYMENT_REQUESTED` sessions.
- Customer-scoped current/history order list, order detail and item-level tracking routes.
- Orders-owned service request lifecycle: `CALL_STAFF`, `WATER`, `UTENSILS`, `OTHER`; create/list/cancel with ownership and retry protection.
- JWT-authenticated `/hubs/mobile-notifications`; connections join only the customer group derived from the token subject.
- Order submit, kitchen preparing and ready transitions publish `orderUpdated`; service request mutations publish `serviceRequestUpdated`.
- B4 pre-order conversion now enters the kitchen as `CONFIRMED`, consistent with normal submitted rounds.

## Compatibility

- Existing browser/table order APIs and the existing table notification hub remain available.
- Existing chef and cashier screens keep their current routes.
- B5 adds mobile projections instead of exposing unscoped internal order endpoints.

## Verification

- Release solution build: 0 warnings, 0 errors.
- Contract suite covers routes, ownership, idempotency, service ownership and realtime isolation.
- Docker runtime checks cover multiple rounds, stable retry, changed-payload conflict, ownership, tracking, service requests and authenticated hub negotiation.
- Runtime test data is removed unless `-KeepTestData` is supplied.
