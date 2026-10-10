# Android cloud startup performance — acceptance in progress

Date: 2026-10-10. Branch: `cloud/postgres-migration`; base: `020ac61`.
Target: existing stable Vercel Preview, not Production.

## Evidence and measurement limits

Process cold launches used force-stop, `am start -W`, and repeated UI hierarchy reads until the real category `Món chính` appeared. Catalog time is an **observed upper bound**, including ADB/UI dump overhead, not frame-accurate rendering or backend cold-start latency. Three samples do not support P95. One initial measurement using an off-screen food title was invalid and excluded.

| Device | Baseline catalog observed (ms) | Cached catalog observed (ms) | Median improvement |
| --- | --- | --- | --- |
| Samsung SM-A528B | 32215 / 9589 / 14471 | 5269 / 4889 / 5660 | 63.6% |
| Small Phone emulator | 12156 / 18299 / 12767 | 7408 / 7005 / 6691 | 45.1% |

Those after samples preceded the final active-session single-flight/public-GET executor refinements. Final Samsung samples on the latest APK: first frames 1284 / 1327 / 1252 ms; categories observed 4785 / 4991 / 4920 ms. Median 4920 ms vs 14471 ms baseline: **66.0% improvement**. First-frame time remains around 1.2 seconds on Samsung; no claim of sub-second whole-screen rendering.

Real Samsung Menu and food images were visually verified. Final Home-to-Menu observation took 3356 ms including UI dump overhead; there is no valid pre-change Menu timing. Image-render timing remains unmeasured. Branch selection was switched to Gò Vấp and restored through the UI to Bình Thạnh. After a process restart, the active table card hydrated again from REST; current orders showed Order #38, `ĐÃ XÁC NHẬN`, 1 × Bánh mì ốp la, 35000 VND, Bàn 2 Bình Thạnh. No order/session/payment mutation was used.

Emulator guest Menu displayed the real cached Bánh mì ốp la/Bò né while Wi-Fi and mobile data were disabled. Network settings were restored in `finally`. This verifies offline Menu rendering, not complete slow-network/error/retry coverage.

## Traces and root causes

- Public Catalog previously required a network round trip after every process restart. Profile/bootstrap sequencing delayed Catalog access; public reads shared workers with private traffic.
- Real network samples included branches HTTP 200, 3 branches; branch-3 menu HTTP 200, 10 categories, 52 dishes. Menu TTFB varied up to about 30 seconds. Backend cold vs warm classification was not proven for these samples.
- Catalog mapping measured 5–40 ms on the main thread; latency is predominantly before mapping, not evidence of a JSON parsing stall.
- Vercel menu SQL logs showed roughly 207–220 ms per remote database command and the source performed one dish query per category. Warm server request traces were approximately 2.8–3.1 seconds. Duplicated EF log entries must not be counted as distinct SQL executions.
- QA order history independently returned HTTP 503 near the 30-second internal service deadline. Its list mapping did repeated per-order status/item/session reads and Catalog calls. N+1 work is a supported suspected cause, but post-deployment recovery remains unverified.

## Implementation under review

- Public Catalog cache: environment-hashed namespace, separate branch keys, 5-minute freshness, maximum 24-hour stale retention, disk restore off the main thread. Only real public branches/menu JSON is persisted. Private customer/session/order/payment data is not cached here.
- Existing Catalog single-flight and branch identity guards are reused. Explicit retry forces refresh; cached content can render before background refresh.
- Public anonymous GETs have separate workers; authentication mutations remain on the private executor. Active-session reads are coalesced.
- Public food/branch images have URL-hashed disk caching and duplicate-download coalescing, with bounded regenerated cache entries.
- Catalog dishes are projected once and grouped in memory instead of issuing one query per category. Order-history item/status reads and Catalog snapshots are batched across the existing bounded page. Ownership filters and API shape are preserved.

Backend batching was compiled, tested and verified on Preview. No index, database reset, scheduler, paid scaling or timeout increase was added.

## Preview runtime acceptance

- Deployment: `dpl_4CiTfSuo4LhKb2fjFJSX4zaWKTgH`.
- URL: https://viettable-o9udbp2p9-hienkhanhvu981-7145.vercel.app
- CLI: `vercel deploy --target preview --yes --scope hienkhanhvu981-7145`, from the freshly allowlisted staff-preserving upload context.
- Inspection: `target=preview`, `readyState=READY`.
- Protected Preview health/readiness: HTTP 200. First health request took 24665 ms; readiness 3174 ms.
- Preview Catalog: 3 branches; menu 10 categories / 52 dishes, 2730 ms. The stable alias subsequently returned the same counts in 2153 ms; another sample took 25692 ms.
- Preview QA History: HTTP 200, 23 orders, 3846 ms instead of the prior 503 near 34 seconds. Stable alias History with the current QA token: HTTP 200, 23 orders, 27885 ms followed by 3181 ms. One probe using the earlier helper-bound auth returned 401; the explicit current-token retry succeeded. No user credential was changed to work around it.
- Existing stable alias was assigned to this accepted Preview only. No protection rule was changed; unauthenticated alias health/menu still returned HTTP 200, and readiness was HTTP 200 at 4312 ms.

The N+1 History failure is no longer reproduced in these accepted current-token samples. **Slow first requests/instance startup and occasional host-side connection failure remain**; there is no cold-start percentile, multi-instance performance guarantee or claim that all 25-second requests have been eliminated.

## Security incident follow-up

The old shared QA credential had appeared in earlier helper output. The user provided a replacement through current-user DPAPI outside Git. Six affected QA accounts were rotated through their own authenticated password-change APIs: one Admin, one Chef, two branch-scoped Cashiers and two Customers.

- Password-change: 6/6 HTTP 200.
- New-password login: 6/6 HTTP 200.
- Old-password login: four staff HTTP 400 and two customer HTTP 401; no successful login.
- The acceptance helper's encrypted DPAPI credential was updated only after all six verifications passed.
- The real Samsung customer was not rotated, logged out, cleared or uninstalled.
- This proves password rotation, **not global revocation of previously issued staff authentication cookies**. Authentication architecture was not changed.
- No plaintext credential, token or private log is stored in this document.

## Tests actually completed

- Android LOCAL `assembleDebug`, `testDebugUnitTest`: PASS (54 unit tests).
- Android CLOUD_PREVIEW `assembleCloudPreview`, test APK build: PASS.
- Samsung cache/TLS/anonymous-auth instrumentation: 3/3 PASS on the updated APK, using isolated test token/cache namespaces.
- Emulator cache/TLS/anonymous-auth instrumentation: 3/3 PASS on the updated APK.
- Initial cache instrumentation: 1/1 PASS on Samsung and emulator.
- CloudHost tests in SDK container: 48/48 PASS.
- Mobile history/Catalog source-contract tests: 4/4 PASS. These are not runtime query-plan or latency tests.
- Staff-preserving Docker build: PASS, including CloudHost Release publish, Chef/Cashier/Admin builds and production-bundle credential tests (3/3). Existing npm audit warnings remain; no unrelated dependency upgrades were made.
- Candidate source/APK secret scan: PASS, 12 DPAPI values and 2608 APK entry encodings checked.
- `git diff --check`: PASS before this documentation addition; rerun before commit.

## Remaining acceptance

- Investigate remaining first-request/instance startup overhead separately; do not add keepalive, paid scaling or a scheduler as an unapproved workaround.
- Full slow-network/error/retry and frame-accurate Menu/image benchmarks remain unverified. Final device cloud probes and Samsung Catalog samples completed; offline Menu smoke completed on emulator.
- Private active-session hydration can still lag public cached Home during slow cloud responses. It is intentionally not persisted in the public cache or replaced with a fake session.
- Review/commit accepted source and documentation only; exclude both `.idea` changes and all temporary artifacts.

Status: **ANDROID CLOUD PERFORMANCE: PARTIAL — remaining first-request cloud startup latency and complete slow-network regression acceptance.** Public Catalog restore and bounded backend read batching have measured improvements. No push.
