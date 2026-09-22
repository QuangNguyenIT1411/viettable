# B2 Status - Mobile Profile, Loyalty and Settings

Status: **implemented and runtime-tested on 2026-09-15**

## Delivered

- Bearer-protected `GET/PUT /api/mobile/v1/me` for reading and editing the signed-in customer's profile.
- Customer identity is always taken from the JWT subject; clients cannot select or update another customer by ID.
- Profile validation for name, phone, email, gender and date of birth, including duplicate phone/email conflict responses.
- `GET /api/mobile/v1/me/loyalty` with points, generated card number, tier, next-tier threshold, progress and benefits.
- Explicit loyalty tiers: MEMBER (0), SILVER (1,000), GOLD (5,000), DIAMOND (20,000).
- `GET/PUT /api/mobile/v1/me/settings` with persisted push, SMS reminder, biometric preference, language and default branch.
- Default branch validation is performed through Catalog's API; Identity does not read Catalog-owned tables.
- All external errors use the frozen `MobileApiError` envelope with correlation ID.

## Data ownership

- `RESTAURANT_IDENTITY.dbo.Customers` remains the source for profile and loyalty balance.
- New `RESTAURANT_IDENTITY.dbo.CustomerSettings` is owned and bootstrapped by Identity.
- Vouchers remain Billing-owned and are intentionally deferred to B6.
- `BiometricLoginEnabled` synchronizes only the user's preference. Biometric credentials and keys must remain device-local when Android integration is implemented.

## Verification

- Release solution build: 0 warnings, 0 errors.
- Automated runtime checks cover authorization, profile persistence, validation/conflicts, loyalty projection, settings defaults/persistence and Catalog branch validation.
- Test-created customers and dependent rows are removed automatically unless `-KeepTestCustomers` is supplied.
