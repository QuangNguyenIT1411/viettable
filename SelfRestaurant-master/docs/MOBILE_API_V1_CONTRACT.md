# VietTable Mobile API v1 - Contract Baseline (B0)

Status: **frozen baseline for implementation**  
Base path: `/api/mobile/v1`  
Source of UI/business truth: `../Customer_App_Function_List_Normalized_Function_Flow_UIUX_v5.md`  
Source of service ownership truth: `AGENTS.md`

## 1. Compatibility rule

- Android calls only the Gateway. It never calls a domain service directly.
- Existing `/api/gateway/customer`, `/api/gateway/staff/*` and service-internal routes remain compatible while mobile v1 is built.
- The Gateway orchestrates cross-domain flows. A service must not read another service's database.
- Breaking changes require a new API version. Additive response fields are allowed in v1.

## 2. Ownership

| Domain | Write owner | Gateway responsibility |
|---|---|---|
| Authentication, customer identity, refresh tokens, loyalty balance | Identity | Mobile auth envelope, token rotation, actor propagation |
| Branches, areas, tables, QR, menu, dish options, inventory | Catalog | Compose customer-facing branch/menu/table views |
| Reservation, reservation slots, favorites, reviews, notifications | Customers | Ownership checks and cross-service validation |
| Dining session, orders, order items, service requests | Orders | QR/check-in orchestration and realtime projection |
| Voucher redemption, invoice, checkout, payment, split bill | Billing | Quote/payment orchestration and provider callbacks |

## 3. Transport conventions

- JSON uses `camelCase` and UTF-8.
- Timestamps are ISO-8601 UTC. Restaurant-local dates use `yyyy-MM-dd`; time slots include an explicit offset when converted to a timestamp.
- Money is JSON integer VND. Database values use `decimal(18,2)`; APIs reject negative amounts.
- IDs are opaque to Android even when currently represented by integers.
- Auth uses `Authorization: Bearer <access-token>` for mobile. Refresh tokens rotate and are revocable. Existing browser cookie sessions are not removed.
- Mutation retries use `Idempotency-Key`. Reusing a key with a different payload returns `409 idempotency_conflict`.
- List endpoints use `cursor` and `limit`; response shape is `{ "items": [], "nextCursor": null }`.
- Every response includes `X-Correlation-Id`. The client may send that header, otherwise Gateway creates it.

## 4. Error contract

All `/api/mobile/v1` errors use the `MobileApiError` record:

```json
{
  "success": false,
  "code": "reservation_slot_unavailable",
  "message": "Khung giờ đã hết bàn phù hợp.",
  "correlationId": "00-f4...-01",
  "details": {
    "suggestions": []
  }
}
```

| HTTP | Meaning |
|---|---|
| 400 | Invalid request or validation failure |
| 401 | Missing, invalid or expired access token |
| 403 | Authenticated actor does not own/cannot perform the action |
| 404 | Resource is absent or intentionally hidden from this actor |
| 409 | State conflict, duplicate slot/session or idempotency conflict |
| 422 | Valid payload that violates a business rule |
| 429 | Rate limited |
| 502/503 | Downstream/provider unavailable; safe retry information is returned |

Canonical codes are lower `snake_case`. Field validation details use `{ "fields": { "fieldName": ["message"] } }`.

## 5. State machines

### Reservation

`PENDING -> PENDING_DEPOSIT -> CONFIRMED -> CHECKING_IN -> CHECKED_IN -> COMPLETED`

Terminal alternatives are `CANCELLED` and `NO_SHOW`. Invalid transitions return `409 reservation_state_conflict`.

### Dining session

`CREATED -> ACTIVE -> PAYMENT_REQUESTED -> PAID -> CLOSED`

`PAYMENT_REQUESTED -> ACTIVE` is allowed only by cancelling the payment request. `CANCELLED` is terminal.

### Order and item

- Order: `DRAFT -> SUBMITTED -> CONFIRMED -> IN_PROGRESS -> COMPLETED`; `CANCELLED` is terminal.
- Item: `PENDING -> CONFIRMED -> PREPARING -> READY -> SERVED`; `CANCELLED` is terminal.
- An order becomes `COMPLETED` only when every item is `SERVED` or `CANCELLED`.

### Payment

`CREATED -> PENDING -> PROCESSING -> SUCCEEDED` or `FAILED/CANCELLED/EXPIRED`.

Only `SUCCEEDED` may move a dining session to `PAID` and then `CLOSED`.

## 6. Endpoint families

This catalog is the v1 target. `Planned` means the route is contractually reserved but is implemented in B1-B7.

| Module | Target endpoints | Owner | Baseline |
|---|---|---|---|
| Auth | `POST /auth/register`, `/auth/login`, `/auth/google`, `/auth/refresh`, `/auth/logout`, `/auth/forgot-password`, `/auth/reset-password`, `/auth/change-password` | Identity | Partial; refresh/JWT planned B1 |
| Profile | `GET/PUT /me`, `GET /me/loyalty`, `/me/vouchers`, `/me/settings` | Identity/Billing | Partial |
| Branch | `GET /branches`, `/branches/{id}`, `/branches/nearby` | Catalog | Partial |
| Tables | `GET /branches/{id}/areas`, `/availability`, `/table-layout`; `GET /qr/{code}` | Catalog | Partial |
| Menu | `GET /branches/{id}/menu`, `/dishes/{id}`, `/search`, `/recommendations` | Catalog | Partial |
| Favorites | `GET/POST/DELETE /me/favorites[/dishes/{id}]` | Customers | Implemented B7 |
| Reservations | `POST /reservations`; `GET /reservations`, `/reservations/{id}`; `PUT /reservations/{id}`; `POST /reservations/{id}/cancel` | Customers | Partial; availability/change/deposit planned B3 |
| Pre-order | `GET/PUT /reservations/{id}/preorder`; `POST /reservations/{id}/preorder/submit` | Orders via Gateway | Partial; current snapshot items migrate in B3 |
| Check-in | `POST /check-ins/qr/validate`, `/check-ins/reservation`, `/check-ins/walk-in` | Orders/Customers/Catalog | Implemented B4 |
| Dining session | `GET /dining-sessions/active`, `/dining-sessions/{id}`; `POST /dining-sessions/{id}/cancel-payment-request` | Orders | Explicit entity implemented B4 |
| Orders | `POST /dining-sessions/{id}/orders`; `GET /orders`, `/orders/{id}`, `/orders/{id}/tracking` | Orders | Implemented B5 |
| Service requests | `POST/GET /dining-sessions/{id}/service-requests`; `POST /service-requests/{id}/cancel` | Orders | Implemented B5 |
| Notifications | `GET /notifications`; `POST /notifications/{id}/read`; realtime `/hubs/mobile-notifications` | Customers/Gateway | Implemented B5/B7 |
| Checkout quote | `POST /dining-sessions/{id}/checkout/quote`, `/payment-request`, `/payment-request/cancel` | Billing/Orders | Implemented B6 |
| Voucher | `GET /me/vouchers`; `POST /checkout/quotes/{id}/voucher` | Billing | Implemented B6 |
| Loyalty | `POST /checkout/quotes/{id}/loyalty` | Identity/Billing | Implemented B6 |
| Payment | `POST /checkout/quotes/{id}/payments`; `GET /payments/{id}`; `POST /payments/{id}/retry`; provider webhook is not a mobile route | Billing | Implemented B6 |
| Invoice | `GET /dining-sessions/{id}/invoice`, `/invoices/{id}` | Billing | Implemented B6 |
| Split bill | `POST/GET /dining-sessions/{id}/split-bill`; `GET /split-bills/{id}`; `POST /split-bills/{id}/payments` | Billing | Implemented B6; additive read projection for mobile resume |
| Reviews | `POST /reviews`; `GET /me/reviews`; `PUT/DELETE /reviews/{id}` | Customers | Implemented B7 |
| Support | `GET /support/faqs`; `POST /support/requests` | Customers | Implemented B7 |

## 7. Idempotent command keys

Required for reservation creation/change, pre-order submit, check-in, order submit, service request creation, payment request, payment creation and provider webhook handling. Commands store request hash, result and terminal status. Concurrent duplicate commands return the same completed result or `409 command_in_progress`.

### B6 VNPAY additive payment fields

ONLINE/QR payment responses retain all B6 fields and add `currency`, `providerName`,
`providerReference`, `providerTransactionId`, `paymentUrl` and
`providerExpiresAtUtc`. Android opens `paymentUrl` in a browser and continues to
read the authoritative state through `GET /api/mobile/v1/payments/{id}` via the
Gateway. It never marks a payment successful from the browser return URL.

VNPAY calls Billing directly at `GET /api/payments/providers/vnpay/ipn`. This is
provider ingress, not a mobile API. Billing verifies `vnp_SecureHash`, resolves
the internal payment by the server-generated `vnp_TxnRef`, and requires the
signed amount and currency to equal the stored payment before changing state.
`GET /api/payments/providers/vnpay/return` is display-only and never finalizes a
payment. Existing ONSITE payments do not create a provider session.

## 8. B0 exit criteria

- The 72-screen and 27-rule traceability matrix is complete.
- The contract conventions and target endpoint families above are frozen.
- Baseline route and document completeness tests are in the solution.
- Existing backend compiles without warnings.
- Runtime feature work starts at B1; a `Planned` endpoint is not claimed as implemented.
