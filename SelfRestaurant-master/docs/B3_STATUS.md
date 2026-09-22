# B3 Status - Branch Discovery, Reservations and Pre-order

Status: **implemented and runtime-tested on 2026-09-15**

## Delivered

- Mobile branch list/detail/nearby, area, table-layout, availability, menu and QR-context routes.
- Availability combines Catalog-owned active tables with Customers-owned reservation occupancy for a two-hour window.
- Suitable-table filtering by party size, overlapping reservation protection and nearby time suggestions when no table is available.
- Bearer-protected reservation create/list/detail/change/cancel routes with strict customer ownership.
- Reservation branch is immutable after creation.
- Required `Idempotency-Key` for create, change and pre-order submit; a reused key with another payload returns `409 idempotency_conflict`.
- Cursor/limit reservation history with upcoming/history scopes.
- Deposit-policy endpoint. Current configured policy is `NOT_REQUIRED` and zero VND; actual payment remains Billing/B6.
- Pre-order draft/read/submit routes with live Catalog dish validation and server-side name/price snapshots.
- Mobile pre-orders are stored in Orders-owned `ReservationPreOrders` and `ReservationPreOrderItems`; Customers remains the reservation owner.

## Deliberate compatibility choices

- Existing browser reservation and legacy pre-order routes remain available.
- Current source data has postal branch addresses but no geographic coordinates, so `distanceKm` is nullable instead of fabricated.
- Current source data has no table-area classification. B3 exposes the honest `GENERAL` area; explicit indoor/outdoor/private/window metadata can be added later without breaking v1.
- QR lookup returns branch/table context only. It never creates an order or checks the customer in; check-in belongs to B4.

## Verification

- Release solution build: 0 warnings, 0 errors.
- Runtime checks cover discovery, availability, authentication, ownership, overlapping slots, idempotency, change, deposit policy, authoritative pre-order pricing and cancellation state guards.
- Test data is deleted across Orders, Customers and Identity after each run unless `-KeepTestData` is supplied.
