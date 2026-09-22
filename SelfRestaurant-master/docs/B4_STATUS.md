# B4 Status - QR Check-in and Dining Sessions

Status: **implemented and runtime-tested on 2026-09-15**

## Delivered

- Bearer-protected QR validation, reservation check-in and walk-in check-in routes.
- QR remains a Catalog-owned branch/table identifier and validation performs no write.
- Wrong-table reservation scans return an explicit confirmation requirement; they never check the customer in automatically.
- Orders owns the new explicit `DiningSessions` lifecycle, including sessions with no order yet.
- One active session per table is protected by both serializable command handling and a filtered unique database index.
- Check-in mutations require `Idempotency-Key`; retries return the original session and conflicting payloads are rejected.
- Submitted reservation pre-orders are converted to one Orders-owned kitchen order and linked to the session exactly once.
- Active/detail session reads enforce customer ownership from the JWT subject.
- Cancel-payment-request is an ownership-checked, terminal-state-safe transition ready for the Billing phase.
- Catalog table projections now include `qrCode` and `statusCode` required by check-in clients.

## Compatibility

- Existing cashier check-in and legacy order-session routes remain available.
- Existing `DiningSessionTables` links remain the compatibility bridge used by order, cashier and payment flows.
- The explicit B4 session can exist before the customer submits any food order.

## Verification

- Release solution build: 0 warnings, 0 errors.
- Contract suite: 64/64 passed.
- Docker runtime checks cover invalid QR, reservation/table mismatch, reservation check-in, one-time pre-order conversion, stable retries, ownership, walk-in and active-table conflicts.
- Runtime test data is removed after the run unless `-KeepTestData` is supplied.
