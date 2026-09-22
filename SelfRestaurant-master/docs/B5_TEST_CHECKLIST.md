# B5 Dining Operations Test Checklist

## Automated gate

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master
docker compose up -d --build
powershell -ExecutionPolicy Bypass -File .\scripts\test-b5.ps1
```

Expected: every line prints `PASS`, ending with `B5 runtime checks passed.`

## Manual checks

- [ ] Order creation requires a customer Bearer token and `Idempotency-Key`.
- [ ] Menu order payload contains only dish ID, quantity and note; server resolves price.
- [ ] Retrying the same payload/key returns the same `orderId`.
- [ ] Reusing a key with changed items returns HTTP 409 `idempotency_conflict`.
- [ ] Two submissions with different keys create two order IDs in the same Dining Session.
- [ ] Another customer cannot read the order, tracking or service requests.
- [ ] Tracking displays independent item states such as `CONFIRMED`, `PREPARING`, `READY` and `SERVING`.
- [ ] A kitchen item transition appears on the next tracking read.
- [ ] `CALL_STAFF`, `WATER`, `UTENSILS` and `OTHER` service requests can be created.
- [ ] Service-request retry does not create a duplicate.
- [ ] An `OPEN` request can be cancelled; cancelling again returns the same result.
- [ ] Anonymous mobile hub negotiation returns 401.
- [ ] A valid customer token can connect to `/hubs/mobile-notifications`.
- [ ] New orders/service requests are blocked once the session enters payment-requested state.
