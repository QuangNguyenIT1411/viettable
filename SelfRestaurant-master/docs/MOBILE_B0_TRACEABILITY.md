# VietTable Mobile - Screen and Rule Traceability (B0)

Legend: **Existing** = usable baseline route exists; **Partial** = some data/flow exists but mobile contract or rule is incomplete; **Planned** = backend capability is missing; **UI-only** = no backend endpoint is required.

Implementation overlay: **S03-S06 are implemented and runtime-tested in B1**. The `Baseline` column below remains unchanged so the B0 audit is reproducible.

## 72-screen matrix

| ID | Android screen | Target API/module | Write owner | Baseline |
|---|---|---|---|---|
| S01 | Splash | token/bootstrap | Gateway/Identity | UI-only |
| S02 | Welcome | none | - | UI-only |
| S03 | Login | `/auth/login`, `/auth/refresh` | Identity | Partial |
| S04 | Register | `/auth/register` | Identity | Partial |
| S05 | Forgot Password | `/auth/forgot-password` | Identity | Existing |
| S06 | Reset Password | `/auth/reset-password` | Identity | Existing |
| S07 | Branch List | `/branches` | Catalog | Existing |
| S08 | Branch Detail | `/branches/{id}` | Catalog | Partial |
| S09 | Branch Map / Nearby | `/branches/nearby` | Catalog | Planned |
| S10 | Change Branch | `/branches` plus local selection | Catalog | Existing |
| S11 | Home | `/home` composition | Gateway | Partial |
| S12 | Search | `/search` | Catalog | Planned |
| S13 | Search Result | `/search` | Catalog | Planned |
| S14 | Notification | `/notifications` | Customers | Partial |
| S15 | Active Dining Session Entry | `/dining-sessions/active` | Orders | Partial |
| S16 | Menu | `/branches/{id}/menu` | Catalog | Existing |
| S17 | Category | `/branches/{id}/menu?categoryId=` | Catalog | Existing |
| S18 | Food Detail | `/dishes/{id}` | Catalog | Partial |
| S19 | Favorites | `/me/favorites` | Customers | Planned |
| S20 | Select Date | `/branches/{id}/availability` | Customers/Catalog | Planned |
| S21 | Select Time | `/branches/{id}/availability` | Customers/Catalog | Planned |
| S22 | Guest Count | reservation validation | Customers | Partial |
| S23 | Select Area | `/branches/{id}/areas` | Catalog | Planned |
| S24 | Table Layout | `/branches/{id}/table-layout` | Catalog | Partial |
| S25 | Reservation Note | `/reservations` | Customers | Existing |
| S26 | Deposit | `/reservations/{id}/deposit` | Billing/Customers | Planned |
| S27 | Reservation Confirmation | `/reservations/{id}` | Customers | Existing |
| S28 | Reservation Detail | `/reservations/{id}` | Customers | Existing |
| S29 | Reservation History | `/reservations` | Customers | Existing |
| S30 | Change Reservation | `PUT /reservations/{id}` | Customers | Planned |
| S31 | Cancel Reservation | `/reservations/{id}/cancel` | Customers | Existing |
| S32 | No Table Suggestion | availability suggestions | Customers/Catalog | Planned |
| S33 | Pre-order Menu | reservation menu | Catalog | Existing |
| S34 | Pre-order Cart | `/reservations/{id}/preorder` | Orders | Partial |
| S35 | Pre-order Confirmation | `/preorder/submit` | Orders | Partial |
| S36 | Pre-order Detail | `/reservations/{id}/preorder` | Orders | Partial |
| S37 | Scan QR | `/check-ins/qr/validate` | Catalog/Orders | Partial |
| S38 | QR Validation | `/check-ins/qr/validate` | Gateway | Partial |
| S39 | Check-in Confirmation | `/check-ins/reservation` | Gateway | Planned |
| S40 | Reservation Mismatch | check-in validation result | Gateway | Planned |
| S41 | Walk-in Confirmation | `/check-ins/walk-in` | Orders | Partial |
| S42 | Dining Session | `/dining-sessions/{id}` | Orders | Partial |
| S43 | Dine-in Menu | `/branches/{id}/menu` | Catalog | Existing |
| S44 | Dine-in Cart | draft order | Orders | Existing |
| S45 | Order Confirmation | order submit | Orders | Existing |
| S46 | Call Staff | `/service-requests` | Orders | Planned |
| S47 | Service Request Status | `/service-requests` | Orders | Planned |
| S48 | Current Orders | `/orders` | Orders | Existing |
| S49 | Order Tracking | `/orders/{id}/tracking` plus realtime | Orders | Partial |
| S50 | Order Detail | `/orders/{id}` | Orders | Existing |
| S51 | Order Item Status Detail | `/orders/{id}/tracking` | Orders | Partial |
| S52 | Order History | `/orders?scope=history` | Orders | Existing |
| S53 | Payment Request / Summary | checkout quote/request | Billing/Orders | Planned |
| S54 | Invoice | `/dining-sessions/{id}/invoice` | Billing | Planned |
| S55 | Voucher Selection | `/me/vouchers` | Billing | Planned |
| S56 | Loyalty Point Selection | checkout loyalty selection | Identity/Billing | Partial |
| S57 | Payment Type Selection | checkout/payment contract | Billing | Planned |
| S58 | Online Payment Method | payment provider methods | Billing | Planned |
| S59 | On-site Payment Method | on-site payment request | Billing | Partial |
| S60 | Payment Location Selection | payment request location | Billing | Planned |
| S61 | On-site Payment Status | `/payments/{id}` | Billing | Planned |
| S62 | Payment Processing | `/payments/{id}` plus realtime | Billing | Planned |
| S63 | Payment Success | payment status/invoice | Billing | Partial |
| S64 | Payment Failed | payment status/retry | Billing | Planned |
| S65 | Split Bill | `/split-bill` | Billing | Planned |
| S66 | Profile | `/me` | Identity | Existing |
| S67 | Edit Profile | `PUT /me` | Identity | Existing |
| S68 | Voucher | `/me/vouchers` | Billing | Planned |
| S69 | Review | `/reviews` | Customers | Planned |
| S70 | Loyalty | `/me/loyalty` | Identity | Partial |
| S71 | Settings | `/me/settings` | Identity | Planned |
| S72 | Help / Support | `/support` | Customers | Planned |

### B2 implementation overlay (2026-09-15)

The baseline values above are preserved as the original inventory. Current implementation status:

| Screen | B2 result | Runtime coverage |
|---|---|---|
| S66 Profile | `GET /api/mobile/v1/me` implemented | Authorized actor/profile projection passed |
| S67 Edit Profile | `PUT /api/mobile/v1/me` implemented | Persistence, validation and duplicate checks passed |
| S70 Loyalty | `GET /api/mobile/v1/me/loyalty` implemented | Tier, card and next-tier progress passed |
| S71 Settings | `GET/PUT /api/mobile/v1/me/settings` implemented | Defaults, persistence, language and branch validation passed |

S68 Voucher remains Billing-owned and scheduled for B6.

### B3 implementation overlay (2026-09-15)

| Screens/rules | B3 result | Runtime coverage |
|---|---|---|
| S07-S10 | Branch list/detail/nearby/change context implemented | Discovery passed |
| S16-S17, S20-S25, S32 | Menu, date/time, guest, area, layout, availability and suggestions implemented | Availability/table-fit passed |
| S26-S31 | Deposit policy plus reservation create/detail/history/change/cancel implemented | Ownership, state and idempotency passed |
| S33-S36 | Pre-order draft/read/submit implemented in Orders | Catalog snapshot and retry passed |
| S37-S38 / R04 | QR resolves table and branch context only | Read-only QR contract covered |
| R02-R03 | Reservation branch is immutable; pre-order requires an owned confirmed reservation | Conflict checks passed |

Reservation/walk-in check-in (S39-S41) was scheduled for B4 and is covered below.

### B4 implementation overlay (2026-09-15)

| Screens/rules | B4 result | Runtime coverage |
|---|---|---|
| S37-S40 / R04, R08 | Read-only QR validation plus explicit mismatch confirmation implemented | Unknown QR, assigned table and wrong-table branches passed |
| S39-S42 / R09, R18 | Reservation and walk-in check-in create an Orders-owned explicit session | Stable retry, ownership and active-table conflict passed |
| Rule R10 | Submitted pre-order is converted to one linked kitchen order | Converted order ID remains stable across retry |
| Rules R24-R25 | Active/detail session reads and cancel-payment-request guard implemented | Active projection, navigation-safe reads and ownership passed |

B5 was responsible for in-session ordering, service requests and realtime tracking; its completed coverage is recorded below.

### B5 implementation overlay (2026-09-15)

| Screens/rules | B5 result | Runtime coverage |
|---|---|---|
| S43-S45 / R01, R05, R16 | Authoritative menu items can create multiple immutable order rounds in one session | Two rounds, one session and retry conflict passed |
| S46-S47 | Service requests support create/list/cancel with ownership and idempotency | Stable retry, payload conflict and cancel retry passed |
| S48-S52 / R15 | Owned order list/detail/tracking exposes independent order-item status | Kitchen transition from confirmed to preparing passed |
| Realtime / R24-R25 | JWT-authenticated mobile hub joins only the token customer's group | Anonymous rejection and authorized negotiation passed |

### B6 implementation overlay (2026-09-16)

| Screens/rules | B6 result | Runtime coverage |
|---|---|---|
| S53-S55 / R06, R11-R13 | Immutable Dining Session quote; voucher and loyalty start at zero and change only by explicit selection | Quote snapshot, stable retry, discount calculation and order lock passed |
| S56-S61 / R14, R19-R21 | Payment state machine separates channel, method and onsite location; failed payments are retryable | Invalid combination, failure, retry and successful webhook passed |
| S62-S67 | Invoice is generated from the authoritative session quote only after successful payment | Invoice total and session closure passed |
| S68-S69 | Billing-owned vouchers and split-bill parts/payments are persisted with ownership and idempotency | Voucher runtime passed; split routes included in B6 gate/source verification |

### B7 implementation overlay (2026-09-16)

| Screens/rules | B7 result | Runtime coverage |
|---|---|---|
| S14 notifications | Paged notification inbox and mark-as-read use the authenticated customer identity | Ready-event projection, read and cross-customer rejection passed |
| S19 favorites | Favorites are customer-owned and reference an active Catalog dish | Add/retry/list/remove and customer isolation passed |
| S69 / R07 | A review requires a Billing-issued invoice owned by the customer | Pre-completion rejection, stable retry, ownership, update/list/delete passed |
| S72 support | FAQ is public; support requests are authenticated, validated and idempotent | FAQ, missing key, retry and key-conflict checks passed |

## 27-rule matrix

| ID | Rule | Enforcement owner | Baseline | Acceptance test |
|---|---|---|---|---|
| R01 | Cart belongs to one branch | Gateway/Android | Partial | Reject mixed-branch cart |
| R02 | Reservation never changes branch implicitly | Customers | Existing | Update cannot silently change branch |
| R03 | Pre-order requires Reservation | Orders | Partial | Reject pre-order without reservation |
| R04 | QR identifies branch/table, not an Order | Catalog/Gateway | Existing | Scan returns context without submitting order |
| R05 | Dining Session contains multiple Orders | Orders | Existing | Two submits create two rounds in one session |
| R06 | Invoice aggregates Dining Session | Billing | Partial | Invoice contains every non-cancelled session item |
| R07 | Review only after completion | Customers | Planned | Reject review before completed transaction |
| R08 | Wrong-table scan does not auto check-in | Gateway | Planned | Return mismatch requiring confirmation |
| R09 | Table has at most one active session | Orders | Partial | Concurrent session creation yields one winner |
| R10 | Check-in links existing pre-order | Orders/Customers | Partial | Items are linked and not cloned |
| R11 | Checkout is per Dining Session | Billing | Partial | Reject isolated order checkout in a session |
| R12 | Customer explicitly selects voucher/points | Billing | Planned | Quote has zero discount until selection |
| R13 | PAYMENT_REQUESTED blocks new Order | Orders | Planned | Submit returns state conflict until cancellation |
| R14 | Close session only after payment success | Billing/Orders | Partial | Failed payment keeps session open |
| R15 | Order and item statuses are separate | Orders | Existing | Item transitions do not overwrite sibling items |
| R16 | Additional dishes create a new Order | Orders | Existing | Second submit produces a new order ID |
| R17 | Order completes when all items are served/cancelled | Orders | Existing | Mixed active item prevents completion |
| R18 | Check-in never recreates pre-order | Orders | Partial | Repeated check-in is idempotent |
| R19 | Payment channel/method/location are separate | Billing | Planned | Validate independent fields |
| R20 | Location applies only to on-site payment | Billing | Planned | Reject location on online payment |
| R21 | MVP supports online QR and on-site methods | Billing | Partial | Supported-method matrix passes |
| R22 | Exactly one customer Home | Android | UI-only | Navigation UI test |
| R23 | Bottom navigation has five fixed tabs | Android | UI-only | Navigation UI test |
| R24 | Active/payment-requested session has quick entry | Gateway/Android | Partial | Active endpoint drives visible card |
| R25 | Leaving screen does not end session | Orders/Android | Partial | Navigation causes no state mutation |
| R26 | Success leads to Review/Home and clears active card | Android/Orders | Partial | Closed session absent from active endpoint |
| R27 | Pre-order completion stays in Reservation context | Android/Orders | Partial | Submit response has no active dining session |

## Coverage summary at B0

| Type | Existing | Partial | Planned | UI-only | Total |
|---|---:|---:|---:|---:|---:|
| Screens | 20 | 22 | 28 | 2 | 72 |
| Rules | 6 | 13 | 6 | 2 | 27 |

The summary is a baseline, not a claim of mobile readiness. B1-B7 must move every non-UI row to **Implemented + automated acceptance test**.
