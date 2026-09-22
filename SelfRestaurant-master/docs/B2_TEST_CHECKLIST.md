# B2 Mobile Profile Test Checklist

## Automated gate

With Docker Desktop running:

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master
docker compose up -d --build
powershell -ExecutionPolicy Bypass -File .\scripts\test-b2.ps1
```

Expected: every B2 line prints `PASS`, ending with `B2 runtime checks passed.`

For the complete backend regression gate:

```powershell
dotnet build .\SelfRestaurant.Microservices.sln -c Release
dotnet test .\tests\SelfRestaurant.Backend.ContractTests\SelfRestaurant.Backend.ContractTests.csproj -c Debug
powershell -ExecutionPolicy Bypass -File .\scripts\test-b0.ps1 -SkipBuild -Runtime
powershell -ExecutionPolicy Bypass -File .\scripts\test-b1.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\test-b2.ps1
```

## Manual API checks

Use an access token returned by B1 register/login in `Authorization: Bearer <token>`.

- [ ] `GET /api/mobile/v1/me` returns only the signed-in customer's profile.
- [ ] `PUT /api/mobile/v1/me` persists name, phone, email, gender, date of birth and address.
- [ ] Username cannot be changed through the profile payload.
- [ ] Invalid phone/email returns HTTP 400; duplicate phone/email returns HTTP 409.
- [ ] `GET /api/mobile/v1/me/loyalty` returns points, card number, tier, progress and benefits.
- [ ] A new customer starts at MEMBER with 0 points and SILVER at 1,000 points as the next tier.
- [ ] First `GET /api/mobile/v1/me/settings` creates and returns safe defaults.
- [ ] `PUT /api/mobile/v1/me/settings` persists notification toggles, biometric preference, language and default branch.
- [ ] Unsupported language returns HTTP 400; a missing/inactive default branch returns HTTP 404.
- [ ] Calling any B2 endpoint without a valid access token returns HTTP 401 in `MobileApiError` format.
- [ ] Voucher functionality is not expected in B2; it remains in Billing/B6.
