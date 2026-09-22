# B4 QR Check-in Test Checklist

## Automated gate

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master
docker compose up -d --build
powershell -ExecutionPolicy Bypass -File .\scripts\test-b4.ps1
```

Expected: all lines print `PASS`, ending with `B4 runtime checks passed.`

## Manual checks

- [ ] An unknown QR returns HTTP 404 `qr_not_found`.
- [ ] QR validation without a matching reservation returns `WALK_IN_AVAILABLE` and creates no session.
- [ ] A valid assigned-table QR inside the arrival window returns `READY`.
- [ ] A different table returns `MISMATCH_CONFIRMATION_REQUIRED`.
- [ ] Reservation check-in without mismatch confirmation returns HTTP 409 and creates no session.
- [ ] Reservation and walk-in check-in reject a missing `Idempotency-Key`.
- [ ] Retrying the same check-in returns the same `diningSessionId`.
- [ ] A submitted pre-order becomes one order; retrying check-in creates neither a second order nor duplicate items.
- [ ] A second customer cannot read another customer's dining session.
- [ ] Two customers cannot hold active sessions for the same table.
- [ ] `GET /dining-sessions/active` returns the signed-in customer's active session.
- [ ] Walk-in creates an active session without creating an empty kitchen order.
- [ ] Cancelling a payment request on an active session is safe and idempotent.
