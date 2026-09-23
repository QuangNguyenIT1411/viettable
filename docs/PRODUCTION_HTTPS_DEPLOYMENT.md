# VietTable production HTTPS deployment — prepared, not externally accepted

Baseline `9c5502e` (2026-09-23). The inspected machine is Windows with Docker Desktop running the **development** Compose project. The repository and local environment provide no actual production server, public hostname, DNS control, trusted certificate or existing reverse proxy. Ports 80/443 were not listening locally during the audit. The configuration below is for a separate deployment host; it has **not** been deployed there. No self-signed certificate is production evidence.

## Topology and files

Use `SelfRestaurant-master/docker-compose.yml` **together with** `SelfRestaurant-master/docker-compose.prod.yml` on a dedicated Docker Compose host. The original Compose file and Samsung `adb reverse tcp:5100` development flow remain unchanged. The override removes every base published port, then publishes only Caddy's TCP 80/443. Caddy terminates TLS, automatically obtains/renews a trusted certificate for `PUBLIC_HOST`, and proxies over the private Compose bridge. Caddy is chosen because this repository had no existing proxy and its automatic HTTPS and WebSocket proxying require one small Caddyfile, not separate certificate tooling.

| External port/path | Internal destination | Purpose |
|---|---|---|
| TCP 80 | Caddy | ACME HTTP challenge and HTTP→HTTPS redirect only |
| TCP 443, normal paths | Gateway `gateway:8080` | Android, web, REST and SignalR `/hubs/mobile-notifications` |
| TCP 443, exact `/api/payments/providers/vnpay/ipn` and `/return` | Billing `billing:8080` | Signed provider callback and display-only return |
| TCP 443, `/healthz` | Gateway liveness | Public non-sensitive health status |
| TCP 443, `/readyz`, `/api/internal/*`, `/internal/*`, `/swagger*` | 404 at Caddy | No public diagnostics/internal routes |

Gateway `:5100`, services `:5101–5105`, SQL `:1433` and RabbitMQ `:5672/15672` have **no host-published ports** in the merged production config. Container-to-container traffic uses Docker DNS. Gateway owns no DB; each service connects to its own database. Outbound Internet remains available for provider/identity dependencies. Do not publish Billing wholesale merely to receive an IPN.

Google Sign-In remains Android→Gateway→Identity; Identity needs no public port. Identity's password-reset links use the public HTTPS host in the production override instead of its local `http://localhost:5100` default. Gateway serves the web clients on the same origin, so browser CORS is not broadened and Android does not depend on CORS.

## External prerequisites and secrets

1. Provision a real server with Docker Compose 2.24.4+ (the override uses `!reset`), persistent storage and a firewall/NAT allowing inbound TCP **80 and 443 only** for the application. Admin SSH/RDP access should be separately restricted. Select an unused Docker CIDR and an IP within it for Caddy; do not overlap host/VPN/LAN networks.
2. Set a real `PUBLIC_HOST` DNS name. Its A record must point at the server's public IPv4 address; add AAAA only if IPv6 really reaches this server. Verify from a network outside the host before starting ACME. Port 80 and outbound ACME access must work for initial issuance and renewal. Caddy state is stored in `caddy-data`; do not delete it on updates.
3. Supply `MSSQL_SA_PASSWORD`, `MOBILE_JWT_SIGNING_KEY` (at least 32 random UTF-8 bytes), `RABBITMQ_USER`/`RABBITMQ_PASSWORD`, `PUBLIC_HOST`, `VIETTABLE_DOCKER_SUBNET`, `VIETTABLE_PROXY_IP` through a protected deployment environment/secret store. Do not reuse the local development signing key, RabbitMQ `guest`, or `.env.example` placeholders. Do not commit a populated `.env` or print `docker compose config` (it includes secret values). On an existing RabbitMQ volume, changing environment credentials alone does not rotate the persisted user; migrate credentials explicitly.
4. Keep `VNPAY_ENABLED=false` until a real merchant environment, hash secret and reachable IPN are accepted. When enabling it, set `VNPAY_TMN_CODE`, `VNPAY_HASH_SECRET`, `VNPAY_RETURN_URL=https://<PUBLIC_HOST>/api/payments/providers/vnpay/return`, and register `https://<PUBLIC_HOST>/api/payments/providers/vnpay/ipn` with the provider. Billing still verifies signature, amount and duplicate state. The proxy intentionally has no access log directive, avoiding signed query-string logging; keep that policy if logging is later added.

Use an encrypted/restricted host volume and backups for `mssql-data`, `rabbitmq-data`, `gateway-uploads`, `gateway-dpkeys` and `caddy-data` (and configuration needed to restore Caddy). `gateway-dpkeys` preserves ASP.NET cookie/session protection keys across container replacement. Those keys currently have no application-level XML encryptor; the deployment operator must secure the volume at rest and arrange an appropriate key-management/backup policy before treating operations as production-ready. SQL currently uses a privileged development-style credential and private-network SQL transport; least-privilege DB users and transport hardening are separate security acceptance work, not hidden by HTTPS at the edge.

## Validate, deploy and roll back

On the **deployment host only**, from `SelfRestaurant-master`, with the real environment already supplied:

```sh
docker compose -p viettable-prod -f docker-compose.yml -f docker-compose.prod.yml config --quiet
docker run --rm -e PUBLIC_HOST -v "$PWD/deploy/Caddyfile:/etc/caddy/Caddyfile:ro" caddy:2 caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
docker compose -p viettable-prod -f docker-compose.yml -f docker-compose.prod.yml up -d --build
docker compose -p viettable-prod -f docker-compose.yml -f docker-compose.prod.yml ps
```

Inspect the merged config **without printing secrets** to confirm that only Caddy publishes 80/443. If a container fails, inspect its logs/readiness; do not add arbitrary startup sleeps or reset volumes. Gateway readiness is currently a shallow app response; verify downstream services separately. The proxy's active check uses Gateway `/healthz`. `ASPNETCORE_ENVIRONMENT=Production` disables development Swagger/exception behavior. Gateway accepts `X-Forwarded-For/Proto/Host` only from the exact static `VIETTABLE_PROXY_IP`, one hop, with `PUBLIC_HOST` as the allowed forwarded host. Caddy overwrites incoming forwarded values. Choose a new subnet/IP together if changing the network. Internal proxy→Gateway HTTP is not a public cleartext endpoint.

After a real certificate and redirects work, enable `ENABLE_HSTS=true` and recreate Gateway; it is deliberately off during first issuance/acceptance. Do not preload the domain without a separate policy decision. Android release uses its existing build-time property, for example `-PVIETTABLE_API_BASE_URL=https://<real-host>`; release cleartext is disabled and normal system trust/hostname checks remain intact. No production domain is committed, and release signing is not part of this task.

Keep a copy of the last validated Compose/Caddyfile and image tags for rollback. Restore those files/configuration, validate again, then update **only** Caddy/Gateway as needed with `docker compose ... up -d --no-deps caddy gateway`. Preserve named volumes; never use `down -v`. A Caddy reload can interrupt existing WebSockets, so the Caddyfile allows a five-minute close delay, and Android reconnect behavior must still be checked on the real network.

## External acceptance still required

Local preparation checks on 2026-09-23 used an ephemeral reserved hostname **only for config parsing**, never for TLS issuance or an APK: merged Compose config passed and published only Caddy TCP 80/443; `caddy validate` passed. Gateway Docker publish passed. After rebuilding Gateway, local B1, B5 (authenticated SignalR negotiation), B6/split and B7/internal signed-provider regressions passed; all `:5100–:5105` health/ready checks returned 200. Android debug build/unit tests passed. Release URL syntax validation rejected cleartext loopback, explicit `:5100`, and `.invalid`; no release APK was built for a pretend host. None of these checks proves public TLS or WSS.

Run `scripts/test-production-smoke.ps1 -BaseUrl https://<real-host> -CheckPrivatePorts` from an **external** client. It checks trusted TLS, public Gateway health/branches/menu/FAQ, HTTP redirect and private-route/port closure. Set `VIETTABLE_PROD_TEST_ACCESS_TOKEN` only in the test process to include authenticated profile and SignalR negotiation; the script does not print it. This negotiation is **not** a WSS connection test. Also verify certificate hostname/chain/expiry, actual WSS connect/reconnect, signed VNPAY IPN reachability, and Samsung login/order/payment flows over Wi-Fi/mobile data with **no adb reverse**. Test the public IP/port surface externally, not only from the Docker host. Do not mark HTTPS E2E PASS until all those checks succeed.

Current statuses: proxy/Compose **PREPARED and statically validated**; external DNS **BLOCKED_EXTERNAL**; trusted production TLS **BLOCKED_EXTERNAL**; public Android/SignalR/VNPAY E2E **BLOCKED_EXTERNAL**. Database ownership remains PASS. Production release is still blocked by these external prerequisites and Android signing/other release acceptance items.
