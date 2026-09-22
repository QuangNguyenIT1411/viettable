# B1 Status - Mobile Authentication

Status: **implemented and runtime-tested on 2026-09-15**

## Delivered

- All eight frozen routes under `/api/mobile/v1/auth`: register, login, Google, refresh, logout, forgot password, reset password and change password.
- HMAC-SHA256 JWT access tokens with issuer, audience, lifetime, customer subject and customer role validation at Gateway.
- Opaque cryptographically random refresh tokens; only SHA-256 hashes are stored by Identity.
- Refresh-token rotation inside a serializable transaction.
- Reuse detection revokes every active token in the compromised token family.
- Logout, password change and password reset revoke refresh sessions.
- Every mobile error returned by Gateway uses `MobileApiError` and includes the correlation ID.
- Existing browser cookie/session auth routes remain unchanged.

## Verification

- Release solution build: 0 warnings, 0 errors.
- B1 runtime flow: register, login, rotate, reuse rejection, family revocation, Bearer authorization, change password, logout, forgot/reset and final login all passed.
- Test-created customers and their tokens are removed automatically unless `-KeepTestCustomer` is supplied.

Google success requires a real Google ID token for the configured client ID; invalid-token and missing-configuration behavior is implemented, while a real-device Google sign-in must be checked when the Android client is connected.
