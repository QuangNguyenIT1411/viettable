# Android lifecycle / SignalR recovery

## Symptom and evidence (Samsung SM-A528B, 2026-09-24)

Baseline: `404d171`. Working tree was clean before this fix. No imagery, icon,
logo, Splash visuals, dependencies or backend business logic changed.

Two distinct exceptions were present in the device Logcat:

1. `NullPointerException` on `OkHttp http://127.0.0.1:5100/...`:
   `HubConnection.lambda$stop$0:448` calls `Transport.stop()` on null,
   through `CallbackCompletableObserver.onComplete`,
   `CompletableOnErrorComplete`, `CompletableSubject.onError`,
   `HubConnection.lambda$start$10:358`, `DefaultHttpClient$2.onResponse:156`.
   Original evidence: 02:03:24, preserved locally in
   `artifacts/lifecycle/before-fix.log`.
2. `IllegalStateException: Can not perform this action after onSaveInstanceState`
   on **main**: `FragmentManager.checkStateLoss` → transaction commit →
   `MainActivity.navigateTo:168` → `BaseFragment.navigateTo:155` →
   `SplashFragment.lambda$onViewCreated$0:55` → Handler.
   Reproduced at **07:48:11.833**, PID 4106, by cold-launching with `am start -W`
   then immediately pressing Home before Splash's existing timer completed.
   Evidence: `artifacts/lifecycle/reproduced-before-fix.log`.

These local artifacts contain filtered runtime/crash diagnostics, not credentials.

## Root cause

- The installed SignalR Java SDK **8.0.30** dereferences the transport in its
  stop continuation without a null guard after failed negotiation. Verified from
  the resolved SDK bytecode with `javap`, not inferred from an unrelated version.
  The old manager allowed `stop()` during `start()` and cleared its start flag
  before the asynchronous SDK operation completed. Old close callbacks also had
  no Hub identity check and could affect a later connection.
- Splash scheduled navigation in `onViewCreated`, cancelling only at
  `onDestroyView`. A stopped, still-added Fragment passed the old checks even
  though its FragmentManager had already saved state.

## Fix

- `SignalRManager`: serialize start/stop, drain a pending start before stopping;
  use Hub identity for terminal/event callbacks; track desired foreground state
  and session generation; avoid duplicate start, refresh and queued retry;
  suppress queued notifications for removed UI listeners. A login callback in
  background cannot independently resume the Hub.
- `MainActivity.onStart`: explicitly resume the app-scoped manager.
- `SplashFragment`: schedule only while resumed, cancel at pause/destroy,
  preserve the original 1200 ms deadline across recreation, validate current
  destination/state, navigate once and do not retain Splash on the back stack.
- No new production delay, sleep, global Rx error handler, state-loss commit,
  broad catch or dependency replacement.

SignalR remains notification-only. A successful connection performs canonical
REST active-session/service resync. Existing reconnect backoff and screen fallback
polling remain in place. API token refresh continues to use the existing shared,
synchronized refresh/rotation path; a network error does not revoke the session.

## Regression coverage

- `LifecycleRecoveryTest`: actual Gateway connection, pause during pending start,
  repeated starts retaining the same Hub, removed listener callback suppression,
  Activity recreation, saved-state Splash callback and duplicate/detached callback;
  real REST fallback scheduling/rescheduling while disconnected and cancellation
  after reconnect.
- `AuthUiIntegrationTest`: reconnect before logout, secure session cleared and
  Hub stopped, then login again through UI and reconnect with real menu data.
- All three lifecycle tests run in the Integration and All instrumentation tiers.
  The existing optional persisted-user-session security test retains its opt-in.
  Its headless harness explicitly resumes/pauses the manager, matching the new
  foreground contract rather than pretending a background login resumes the app.

## Acceptance execution

Device: Samsung SM-A528B, serial `R5CRC14LFYD`; APK Gateway base
`http://127.0.0.1:5100`; `adb reverse tcp:5100 tcp:5100` retained.

| Verification | Result |
|---|---|
| `gradlew.bat :app:assembleDebug :app:testDebugUnitTest :app:assembleDebugAndroidTest -PVIETTABLE_API_BASE_URL=http://127.0.0.1:5100` | PASS; 31 unit tests, 0 failures/errors |
| `powershell -ExecutionPolicy Bypass -File scripts/test-android-instrumentation.ps1 -Tier All` | PASS; 13 passed + 1 unchanged opt-in skip, 44.444 s |
| Same All command plus `-RunExistingSessionSecurity` | **14/14 PASS**, 44.678 s; persisted encrypted session retained |
| Home → background → Gateway restart → **actual Samsung Recents card tap** | **10/10 PASS, 0 crashes**, PID 8118 unchanged; each iteration one connected transition + canonical REST resync + real Home/menu text |
| Gateway unavailable while resumed | **25.4 s** measured outage, plus an earlier ~46 s outage; repeated transport failures/retry, no session revocation/crash; automatic reconnect and REST resync |
| Rapid Home/foreground commands | **10/10**, PID 12099 unchanged; no duplicate connection logs; very short transitions may not reach Activity.onStop |
| Android background `am kill`, not force-stop, then Recents restore | PID 8118 absent → new 11685 (Menu); PID 11685 absent → new 12099 (Home). Both reconnect/resync without crash. Restored Home displays branch and food names |
| Recreation/configuration | ActivityScenario.recreate PASS (authenticated Home + login); physical rotation not separately exercised |
| Logout after reconnect → login again | PASS through UI, secure tokens clear, STOPPED, then real branch/menu and SignalR connected |
| Token refresh | Persisted phone session refreshed on initial launch; existing concurrent REST 401/rotation regression PASS |
| Payment/demo spot check | Home/menu and read-only order screen only; no new order/payment, no cashier confirmation, no B6 rerun |

The first expanded logout test timed out opening Profile immediately after an
ActivityScenario window transition. The reconnect precondition was isolated from
that window transition (pause → STOPPED → resume → CONNECTED); no assertion/skip
was removed. Real system transitions remain covered by Recents stress, background
smoke, Activity recreation and process-kill tests.

Local evidence: `artifacts/lifecycle/recents-results.txt`, `recents-stress.log`,
`offline-results.txt`, `offline-recovery.log`, `rapid-and-process-restore.log`.
Artifacts are intentionally untracked. Initial crash logs are separate from the
post-install run; `final-logcat.log` contains **no AndroidRuntime/FATAL EXCEPTION**
since the post-fix log reset. Final app launch shows real Home branch/menu and
successful refresh/reconnect/resync; Gateway is ready and USB reverse is present.

**Lifecycle crash blocker: CLOSED for the tested local Samsung scenario.** The
separate full-Menu process-restoration rendering limitation below remains open.

## Limitations

- This is a local Gateway/Samsung lifecycle fix, not external provider, production
  HTTPS or complete process-death business-state certification.
- A pending SDK start is drained to its normal completion/network timeout instead
  of invoking the unsafe SDK stop-during-negotiate path. It cannot dispatch events
  or reconnect when foreground/session no longer permits it.
- Existing **MenuFragment process-restoration rendering debt**: killing the process
  while on the full Menu screen restores that screen before bootstrap has loaded
  its branch/catalog. Its existing `onStateChanged` refreshes only the cart, leaving
  menu rows empty. Returning Home restores real catalog display; reopening Menu
  is the local workaround. Source is unchanged from the baseline, and no crash
  occurs. This is not claimed as full process-death UI acceptance.
- SDK retry backoff can make recovery take about 30 s after Gateway is available;
  retry timing was not reduced to make the test pass. Menu/network failure still
  uses the existing Toast/cached-content behavior, not a new offline-state design.
