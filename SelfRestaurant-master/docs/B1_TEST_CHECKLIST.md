# B1 Mobile Authentication Test Checklist

## Automated gate

With Docker Desktop and the B0 stack running:

```powershell
cd D:\Quang\VietTable\SelfRestaurant-master
dotnet build .\SelfRestaurant.Microservices.sln -c Release
docker compose up -d --build
powershell -ExecutionPolicy Bypass -File .\scripts\test-b1.ps1
```

Expected: build has 0 warnings/errors and every B1 line prints `PASS`.

## Manual API checks

- [ ] `POST /api/mobile/v1/auth/register` returns access token, refresh token and customer profile.
- [ ] `POST /api/mobile/v1/auth/login` accepts username, email or phone plus password.
- [ ] `POST /api/mobile/v1/auth/google` rejects an invalid Google ID token; a valid token requires the configured Google client ID.
- [ ] `POST /api/mobile/v1/auth/refresh` returns a different refresh token.
- [ ] Reusing an old refresh token returns HTTP 401 and revokes its complete token family.
- [ ] `POST /api/mobile/v1/auth/logout` revokes the supplied refresh token.
- [ ] `POST /api/mobile/v1/auth/change-password` without Bearer token returns the `MobileApiError` envelope with HTTP 401.
- [ ] Password change/reset revokes all active mobile refresh tokens for that customer.
- [ ] Forgot password never reveals whether an account exists. Development may expose its reset token for testing.
- [ ] Existing `/api/gateway/customer/auth/*` cookie/session flows continue working.

Refresh tokens are stored only as SHA-256 hashes in `RESTAURANT_IDENTITY.dbo.MobileRefreshTokens`.
