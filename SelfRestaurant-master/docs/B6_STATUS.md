# B6 Status - Checkout, Payment and Invoice

Status: **implemented and runtime-tested on 2026-09-22**

2026-09-23 follow-up: **ONSITE Cashier → mobile Payment → invoice/split/session acceptance PASS**, including Samsung UI, concurrent confirmation and Orders-outage reconciliation. See [ONSITE acceptance](../../docs/ONSITE_PAYMENT_ACCEPTANCE.md) for current evidence and limitations; the earlier verification below is historical.

## Delivered

- Billing-owned immutable checkout quotes aggregate every non-cancelled item in one Dining Session.
- Quotes start with zero voucher and loyalty discount. Customers must explicitly select or remove each benefit.
- Active voucher discovery, minimum-order checks, fixed/percentage discount calculation and maximum-discount caps.
- Loyalty points are validated against Identity and settled only after successful payment.
- Payment request locks the Orders-owned Dining Session and prevents new food/service requests.
- Payment state machine supports pending, failed/cancelled/expired, retry and successful provider webhook completion.
- ONLINE supports QR without a location. ONSITE supports CASH/QR/TRANSFER and requires TABLE or CASHIER.
- A failed payment leaves the Dining Session locked and open; only success closes it and issues an invoice.
- Invoice detail preserves the quote's item and money snapshot.
- Split bills validate that all parts sum exactly to the quote total and close only after every part succeeds.
- Split bill reads expose each part's linked payment status so clients can safely resume or retry failed, cancelled, or expired attempts.
- Payment updates are projected through the authenticated customer SignalR group.
- ONLINE/QR creates a signed VNPAY Sandbox payment URL; Android opens that URL and polls the Gateway-owned payment resource.
- VNPAY IPN is HMAC-SHA512 verified, correlated by the server-generated provider reference, and validates both signed VND amount and currency.
- The browser return URL is informational only. Verified successful IPN and authorized Cashier ONSITE confirmation use the same finalization service; durable reconciliation retries cross-service completion.
- Provider transaction identifiers are persisted separately from internal payment IDs/codes; sensitive provider secrets remain environment-only.

## Compatibility

- Existing cashier checkout and bill APIs remain for legacy sessions. Mobile checkout sessions use the mobile ONSITE panel; legacy checkout rejects a disconnected Bill settlement for those sessions. Legacy Bill reports are not migrated or synchronized with mobile invoices here.
- Orders continues to own Dining Session/order state; Billing calls Orders APIs instead of reading its database.
- Public mobile APIs remain Gateway-only and customer ownership comes from the JWT subject.

## Verification

- Solution build: 0 warnings, 0 errors.
- Existing source and contract regression suite: 75/75 passed.
- Docker B6 runtime gate covers quote, voucher/loyalty, lock, method validation, invalid signature, amount mismatch, failed payment, retry, duplicate webhook, successful IPN, invoice and session closure.
- Split-bill runtime gate verifies failed-part retry, new provider reference relinking, all-part success and authoritative finalization.
- External VNPAY Sandbox acceptance still requires a real sandbox `TmnCode`, `HashSecret` and a public HTTPS IPN URL; repository tests use runtime-only credentials and never emulate success in production code.
