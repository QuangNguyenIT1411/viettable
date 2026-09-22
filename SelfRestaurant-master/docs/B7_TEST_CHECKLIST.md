# B7 Customer Engagement Test Checklist

## Automated gate

Keep Docker Desktop running, then execute:

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master
docker compose up -d --build
powershell -ExecutionPolicy Bypass -File .\scripts\test-b7.ps1
```

Expected: four `PASS` lines, ending with `B7 runtime checks passed.`

## Manual checks

- [ ] Adding the same available dish twice creates only one favorite.
- [ ] A favorite cannot be seen by a different customer.
- [ ] Removing a favorite makes it disappear from `GET /me/favorites`.
- [ ] A ready kitchen item appears in `GET /notifications`.
- [ ] Marking a notification read persists `isRead = true`.
- [ ] A customer cannot mark another customer's notification read.
- [ ] `GET /support/faqs` works without a token.
- [ ] Creating a support request without `Idempotency-Key` returns HTTP 400.
- [ ] Replaying the same support request/key returns the same request ID.
- [ ] Reusing the support key with changed content returns HTTP 409.
- [ ] Reviewing a missing or unfinished invoice returns HTTP 409 `review_not_eligible`.
- [ ] A paid invoice can be reviewed exactly once by its owner.
- [ ] Replaying the same review command/key returns the same review ID.
- [ ] Another customer cannot update or delete the review.
- [ ] The owner can update, list and delete the review.
