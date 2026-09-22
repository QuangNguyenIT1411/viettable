# VNPAY Sandbox setup

VietTable uses VNPAY Pay v2.1.0 for ONLINE/QR payments. Billing creates the
signed provider URL; Android opens it and polls the payment through Gateway.
Only a signed IPN can finalize an invoice and close a Dining Session.

## Merchant configuration

1. Register a VNPAY Sandbox merchant and obtain `TmnCode` and `HashSecret`.
2. Expose Billing's provider ingress through HTTPS. Do not expose the database,
   RabbitMQ, or the other service ports.
3. Configure the merchant IPN URL as:
   `https://<public-host>/api/payments/providers/vnpay/ipn`
4. Set these local environment values without committing them:

```text
VNPAY_ENABLED=true
VNPAY_TMN_CODE=<sandbox terminal code>
VNPAY_HASH_SECRET=<sandbox hash secret>
VNPAY_PAYMENT_URL=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
VNPAY_RETURN_URL=https://<public-host>/api/payments/providers/vnpay/return
VNPAY_EXPIRY_MINUTES=15
```

Then recreate Billing and Gateway with `docker compose up -d --force-recreate billing gateway`.
Do not place actual credentials in `.env.example`, source code, Android resources,
logs, or screenshots.

## Trust boundary

- The return URL only displays a result and does not mutate payment state.
- IPN signature, provider reference, VND currency and exact amount are verified.
- The VNPAY transaction number is stored as provider metadata, never used as an
  internal `PaymentId`.
- Duplicate terminal callbacks are acknowledged as already processed and do not
  issue another invoice.
- FAILED/CANCELLED/EXPIRED attempts may be retried through the existing B6 retry
  endpoint; a retry gets a new internal payment and provider reference.
- ONSITE CASH/QR/TRANSFER continues to use the existing staff-confirmation flow
  and does not depend on VNPAY configuration.

## Local verification

The provider runtime wrapper can create one-time local contract credentials,
recreate Billing/Gateway, run non-split and split signed-IPN tests, and restore
the provider-disabled defaults. It does not store the generated secret:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-payment-provider.ps1 -UseLocalContractCredentials
```

These tests prove VietTable's signing, validation and state transitions. A real
end-to-end payment on VNPAY's hosted page still requires merchant sandbox
credentials and VNPAY reaching the public IPN URL.
