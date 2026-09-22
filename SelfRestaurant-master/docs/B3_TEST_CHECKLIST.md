# B3 Reservation Journey Test Checklist

## Automated gate

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master
docker compose up -d --build
powershell -ExecutionPolicy Bypass -File .\scripts\test-b3.ps1
```

Expected: all lines print `PASS`, ending with `B3 runtime checks passed.`

## Manual checks

- [ ] Branch list, detail, nearby, area, layout and menu load without authentication.
- [ ] Availability returns only physically active, sufficiently large and non-overlapping tables.
- [ ] QR lookup returns only table/branch context and causes no write.
- [ ] Reservation endpoints reject missing/invalid Bearer tokens.
- [ ] Create/change/pre-order submit reject a missing `Idempotency-Key`.
- [ ] Retrying a command with the same key and payload returns the original result.
- [ ] Reusing a command key with another payload returns HTTP 409 `idempotency_conflict`.
- [ ] A second customer cannot read, change, cancel or pre-order against another customer's reservation.
- [ ] Two customers cannot reserve the same table in an overlapping two-hour window.
- [ ] Changing a reservation cannot change its branch.
- [ ] Reservation list supports `scope=upcoming|history`, `cursor` and `limit`.
- [ ] Deposit returns `NOT_REQUIRED`/0 VND under the current configuration.
- [ ] Pre-order ignores client-supplied price because Android sends only dish ID, quantity and note.
- [ ] Cancelling a reservation prevents later changes.
- [ ] Check-in is not expected in B3; it is implemented in B4.
