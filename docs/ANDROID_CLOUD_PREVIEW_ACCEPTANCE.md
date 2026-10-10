# Android CLOUD_PREVIEW acceptance — 2026-10-10

Status: **ANDROID CLOUD: PARTIAL**. Stable public alias and Android build/install are verified;
Samsung native requests reproduced exceeding the old 20-second read budget; CLOUD_PREVIEW now has a
measured-cold-start-compatible 60-second budget. Main Samsung business E2E is not accepted.
Do not substitute API-client/connectivity results for device business acceptance.

## Approved origin and scope

- Project: `viettable-api`, owner `hienkhanhvu981-7145`.
- Stable alias: `https://viettable-staging-hienkhanhvu981.vercel.app`.
- Pinned Preview: `dpl_7dbGYm8vsm6HTDahRT66mdN9fX6q`.
- Original generated Preview: `https://viettable-q6sf4c7h9-hienkhanhvu981-7145.vercel.app`.
- Alias metadata project ID: `prj_2EWpgevTH5xYnEqJbD5AKaaTtshX`; deployment ID checked before mutation.
- Exception scope: `alias-protection-override` on this alias's UID only, not deployment/project/wildcard.
- The entire alias origin, including Staff login pages, is publicly reachable. Application auth remains required.
- Project `ssoProtection` is unchanged; original generated Preview still redirects to Vercel SSO (302).
- Production deployment `dpl_HtbM5wEHbj4HcfQRW4RJCya6JPV5` and its existing aliases were not changed.
- No deployment, Production bootstrap, paid upgrade, Railway, Customer Web deployment or scheduler added.

Skills used: Vercel CLI/domain guidance selected CLI alias assignment and the documented alias-specific
API override; protected-deployment guidance kept pre-exception checks authenticated without embedding
bypass credentials. Browser/computer-use guidance was used for Chef login-page smoke; Samsung interaction
was restricted to app install/open/logs and a read-only instrumentation probe.

## Public HTTPS verification — no Vercel credentials

| Check | Result in this turn |
|---|---|
| `/healthz`, `/readyz` | 200, no SSO redirect, normal certificate validation |
| Anonymous `/api/mobile/v1/me` | 401 |
| Anonymous Chef/Cashier/Admin dashboard API | 401 |
| Anonymous `/api/internal/recovery` | 401 |
| Anonymous mobile Hub negotiate | 401 |
| Customer password login, profile | 200 using existing staging QA credential in memory |
| Catalog branches/menu | 200; 3 branches; a menu retry completed in 3.4 s after one timeout |
| Supabase branch image | HTTPS redirect followed to Storage; 200 `image/webp` |
| Chef/Cashier/Admin cookie login and dashboards | 200 for each existing staging actor |
| Cashier cookie → Chef dashboard | 401, current route's role-denial behavior; not claimed 403 |
| Mobile SignalR | JWT negotiate 200, fresh WSS connection and JSON handshake accepted |
| Durable recovery | Authorized POST 200 `processed` in 2.4 s on bounded retry; first 25 s probe timed out |
| Chef login browser UI | Real public login form rendered; not a new authenticated browser E2E |

No new session/order/payment was created by the verifier. Wrong-branch mutation, customer isolation,
event delivery and cross-instance fan-out were not rerun on the alias; previous acceptance of this exact
deployment is recorded in `STAFF_WEB_CLOUD_ACCEPTANCE.md`, not new Samsung evidence.
Final readiness recheck with a 60 s deadline: 200 at 26.0 s; a 25 s probe had timed out.
Both current and earlier generated Preview URLs were independently rechecked as protected (302).

## Android configuration and commands

`cloudPreview` is a separate debug-signed build type. It compiles the stable HTTPS alias and uses main's
system-CA-only network security configuration (no cleartext/loopback exception). It does not inherit the
debug resource source set. Package remains `com.restaurant.viettable`; install replaces the current build,
not application data. LOCAL/Preview customers live in separate databases; do not assume local credentials
or active sessions exist in staging. Use normal logout/login when switching environments.

REST, refresh and Hub URL continue through `ApiClient.absoluteUrl`; SignalR derives WSS from the HTTPS
origin. Branch/dish relative images resolve through the same origin and redirect to public Supabase Storage.
No trust-all TLS, hardcoded business IDs, Vercel bypass, backend signing key, DB or Storage credential added.
Cloud REST/refresh and image read timeouts are 60 seconds; LOCAL/release retain 20/6 seconds respectively.
Connect timeouts and mutation retry/idempotency rules are unchanged. No automatic registration retry added.

```powershell
cd D:\Quang\VietTable
.\gradlew.bat :app:assembleCloudPreview :app:assembleDebug :app:testDebugUnitTest
& D:\Quang\Android\platform-tools\adb.exe -s R5CRC14LFYD install -r .\app\build\outputs\apk\cloudPreview\app-cloudPreview.apk
& D:\Quang\Android\platform-tools\adb.exe -s R5CRC14LFYD shell am start -n com.restaurant.viettable/.MainActivity
```

APK: `app/build/outputs/apk/cloudPreview/app-cloudPreview.apk` (not committed).
Default LOCAL debug remains `http://10.0.2.2:5100`; existing phone-local scripts/property remain unchanged.
Both variants assembled successfully; shared Android unit tests **41/41 PASS**. This AGP configuration
generates host unit tests for debug only, so no nonexistent `testCloudPreviewUnitTest` result is claimed.

## Samsung diagnosis and remaining acceptance

- SM-A528B connected and cloud APK installed/opened successfully. ADB reverse list empty; none created.
- Docker Desktop was already running; no container was started/stopped for this task. Cloud APK's API origin
  is HTTPS Vercel, not Docker/localhost. **Docker OFF was not proven** and is not reported PASS.
- User opened alias `/healthz` in Samsung Chrome and saw `{"status":"ok"}`.
- User reported Google login error. Backend source requires `Authentication:Google:ClientId` (or its
  documented environment fallback); current Preview lacks that setting and returns `google_not_configured`.
  Android's public Google Web Client ID is present. No fake token/OAuth workaround or redeploy performed.
- User reported password registration network error. Vercel recorded the registration request 200;
  do not create duplicate accounts or reset passwords to compensate for a client timeout.
- Read-only native instrumentation reproduced `SocketTimeoutException` at the 20-second read deadline.
  One run: TLS 260 ms, health 200 in 687 ms; next branches request TLS 152 ms then response timeout at
  20,163 ms. Another run: TLS 231 ms, health response timeout at 20,242 ms.
- Corresponding cloud requests include all five module-start messages before handler execution; branches
  handler ultimately 200 in 579 ms, registration handler 200 in 1,726 ms. A workstation health request also
  took 23.4 s, while a subsequent warm request took 0.5 s. This supports cold-start/edge-to-container latency,
  not certificate failure. Exact full cold-start contribution still needs measurement/optimization.
- Extended native probe: TLS 242 ms, health 200 at **25,388 ms**, then branches 200 at 1,067 ms,
  anonymous profile 401 at 537 ms, empty login 400 at 498 ms; **1/1 diagnostic test PASS** with 60 s budget.
  This directly proves a response arriving after the old Android deadline, not a guessed TLS/network error.
- Small fix: cloudPreview read budgets 60 s, shared clients consume BuildConfig timeout fields. LOCAL/release
  deadlines remain unchanged. Cloud startup/authentication was not redeployed or rewritten.
  Rebuilt/reinstalled cloud APK and reran native diagnostics with the installed default budget:
  **2/2 device tests PASS in 51.9 s**, including actual `ApiClient` anonymous callbacks; no timeout override.
  LOCAL build and shared **41 unit tests PASS** rerun after source changes. APK secret scan **PASS**:
  2,608 expanded-entry encodings checked against 11 DPAPI secret values, plus key/JWT patterns.
  Debug diagnostics emit only host/method/status or exception class, never request body/header/query/token.
- Samsung QR, ordering, Chef `orderUpdated`, received-item action, service requests, Cashier payment,
  invoice/history/session close/table release, background/reconnect/isolation: **UNVERIFIED this turn**.
- Wi-Fi versus 4G/5G was not established. Both remain **UNVERIFIED**, not inferred from Chrome health.

Native diagnostic only (test APK compiled against same origin, installed cloudPreview app remains target):

```powershell
.\gradlew.bat :app:assembleDebugAndroidTest -PVIETTABLE_API_BASE_URL=https://viettable-staging-hienkhanhvu981.vercel.app
& D:\Quang\Android\platform-tools\adb.exe -s R5CRC14LFYD install -r .\app\build\outputs\apk\androidTest\debug\app-debug-androidTest.apk
& D:\Quang\Android\platform-tools\adb.exe -s R5CRC14LFYD shell am instrument -w -e class com.restaurant.viettable.network.CloudPreviewConnectivityTest com.restaurant.viettable.test/androidx.test.runner.AndroidJUnitRunner
```

Probe defaults to the installed target APK's BuildConfig read deadline (read reflectively to avoid the debug
test compiler's inlined constants). Optional `-e cloudReadTimeoutMs 20000` reproduces the former budget.
It does not establish main E2E PASS. It also tests the actual installed `ApiClient` with an isolated, unused
token vault and anonymous health/Catalog callbacks. It uses no credential and
only health/Catalog/unauthorized profile/empty-login validation, no persisted business mutation.

## Redeploy / rollback instructions (not executed)

Inspect a future deployment first: project must match and **target must be preview**, then:

```powershell
npx vercel alias set <VERIFIED-PREVIEW-URL> viettable-staging-hienkhanhvu981.vercel.app --scope hienkhanhvu981-7145
```

Recheck alias deployment ID, hostname-only protection override, ordinary health/ready, protected original
Preview and unchanged Production. Never point the stable alias at Production or broaden exceptions.

Rollback only this exception, using the exact alias UID read from metadata:

```powershell
$aliasRecord = npx vercel api /v4/aliases/viettable-staging-hienkhanhvu981.vercel.app --scope hienkhanhvu981-7145 --raw | ConvertFrom-Json
'{"override":{"scope":"alias-protection-override","action":"revoke"}}' | npx vercel api "/aliases/$($aliasRecord.uid)/protection-bypass" -X PATCH --scope hienkhanhvu981-7145 --input - --raw
```

Verify ordinary health redirects to SSO after propagation. Do not use deployment ID, wildcard or the
project automation-bypass API for this rollback. API mutation output must not be stored as a credential artifact.

## Next acceptance

Native connectivity and the new cloud read budget are verified; optimize server cold-start separately
if needed, and configure staging Google if Google is required.
Use the customer already registered through Samsung UI; finish the actual QR → Chef → Cashier → invoice
flow without ADB reverse. Observe WSS updates and REST resync on-device. Repeat on Wi-Fi and, manually,
4G/5G, background/foreground and network reconnect. Only then update the final status to PASS.
