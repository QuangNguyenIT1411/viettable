# B6 Checkout and Payment Test Checklist

## Automated gate

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master
docker compose up -d --build
powershell -ExecutionPolicy Bypass -File .\scripts\test-b6.ps1
```

Expected: four `PASS` lines, ending with `B6 runtime checks passed.`

## Manual checks

- [f ] Creating a quote without `Idempotency-Key` returns HTTP 400.
- [ ] Retrying the same quote command returns the same quote ID.
- [ ] Quote subtotal contains all non-cancelled items from all rounds in the Dining Session.
- [ ] A new quote has zero voucher and point discounts.
- [ ] Voucher and points are applied only after explicit customer actions.
- [ ] Reusing an idempotency key with a changed payment payload returns HTTP 409.
- [ ] Payment request changes the session to `PAYMENT_REQUESTED` and blocks new orders/service requests.
- [ ] ONLINE rejects CASH and any onsite location; ONLINE QR succeeds without location.
- [ ] ONSITE requires TABLE/CASHIER and accepts CASH, QR or TRANSFER.
- [ ] A failed provider result does not close the session or issue an invoice.
- [ ] A failed/cancelled/expired payment can be retried as a new attempt.
- [ ] Replaying a successful webhook does not duplicate the invoice or loyalty settlement.
- [ ] A successful payment closes the session and returns its invoice.
- [ ] Split amounts must be positive and sum exactly to the quote total.
- [ ] A split bill closes only after every part has succeeded.
