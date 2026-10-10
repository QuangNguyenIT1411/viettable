# Android Cloud Home Catalog fix — 2026-10-10

Scope: Home/Menu/branch selection only, approved stable Vercel Preview, Samsung SM-A528B Android 14.
No backend source, deployment, auth protocol, SignalR implementation, order/payment mutation or fake
data added. User session 20/order 38 preserved. Existing unrelated `.idea` and acceptance-doc edits excluded.

## Root cause and reproduction

Home previously only rendered in-memory categories/foods. It did not fetch Catalog or expose load/error
state; startup/login bootstrap errors with a null callback were silently discarded. Bootstrap also gates
menu behind profile. Thus slow/failed startup could leave Home empty with no recovery despite real Catalog
data. Selected branch was RAM-only, so cold start could select a different first branch. ChangeBranch
updated selected branch but neither fetched new menu nor cleared old Catalog; Menu only updated its cart
on shared-state changes. Old async menu responses could overwrite a newly selected branch.

Direct Samsung reproduction: open ChangeBranch while branches are still loading → original screen stays
empty even after branches/menu arrive, because it captured a one-time snapshot and had no state listener.
This was fixed locally without changing Gateway contracts.

## Implementation

- AppStateManager initializes a non-sensitive selectedBranchId preference scoped to Gateway origin,
  resolves the saved ID against real fetched branches, and tracks Catalog branch/loading/error.
- Explicit branch changes retain dining session/order state, clear old branch Catalog/cart per existing
  cart rule, and notify listeners. Responses/errors for a no-longer-selected branch cannot overwrite state.
- Repository coalesces concurrent branch/menu requests, uses the existing public Gateway routes and
  validates that menu has a categories array. No JWT is required for these public Catalog endpoints.
- Home fetches/retries Catalog independently of profile bootstrap. Loading, no-branch guidance, real
  empty state, business/network error and Thử lại are rendered instead of silent blank sections.
- ChangeBranch listens to async shared state, fetches branches when absent and exposes loading/retry.
- Menu renders category/content/branch changes and loading/error/empty/retry states. Category index is
  bounded during tab rebuild to avoid transient invalid selection callbacks.
- Debug CatalogLoad logs contain branch ID, counts, applied flag, elapsed time/status only; no user
  identity, payload, cookie, password, authorization header, JWT or connection string.

## Cloud evidence

Stable origin: https://viettable-staging-hienkhanhvu981.vercel.app

| Read-only check | Result |
|---|---|
| GET `/api/mobile/v1/branches` | 200, 3 real branches |
| GET `/api/mobile/v1/branches/3/menu` | 200, 10 categories, 52 dishes, ~25.7 s |
| Supabase Catalog counts | 3 Branches, 10 Categories, 52 Dishes |
| Menu JSON | branchId/branchName/categories with nested dishes, matching Android parser |
| User fixture | session 20 ACTIVE, item 38 CONFIRMED, quantity 1, 35,000 VND, PaymentId null |

No independent categories/dishes route was invented: the mobile contract returns them in branch menu.

## Samsung evidence

- CLOUD_PREVIEW installed with `adb install -r`; current customer authentication preserved, no logout,
  password entry or token extraction needed. No ADB reverse; no Docker backend used for runtime requests.
- On slow cloud startup, Home displays Đang tải danh mục và món ăn… and a meaningful branch prompt.
  Opening selector during this load now shows loading then real branches rather than permanently blank.
- Gò Vấp → Bình Thạnh through actual branch buttons: Home title changes and menu reloads correctly.
- Bình Thạnh Home displays category chips plus recommendation, best-seller and featured food cards.
  Actual screenshots inspected; Menu displays Bánh mì ốp la (35,000), Bò né (85,000), Bún Bò Huế
  (55,000), Bún Chả Hà Nội (60,000), with real food photographs.
- CatalogLoad: branch 2 and 3 each return 10 categories/52 foods; observed warm menu loads ~3.1–3.2 s.
- Existing recommendation/featured lists are Catalog-backed; existing best-seller ordering reverses the
  Catalog list, not a server-ranked sales feed. Ranking semantics were not redesigned by this bug fix.
- Force-stop persistence PASS: explicitly selected Gò Vấp (ID 2, not the first cloud branch),
  force-stopped and relaunched without clearing data. Home restored Gò Vấp with category chips/menu
  and the original Bình Thạnh dining session still present. Returned selection to Bình Thạnh afterward.

## Tests

```powershell
.\gradlew.bat :app:assembleDebug :app:assembleCloudPreview :app:testDebugUnitTest
```

PASS, **45 unit tests, 0 failures/errors**, including 4 new BranchCatalogStateTest cases: branch change,
stale response/error rejection, refreshed list keeps selection, retry/error/empty-load completion.

Installed CLOUD_PREVIEW native `CloudPreviewConnectivityTest`: **2/2 PASS**, 72.923 s. Normal system TLS;
health 200 ~23.7 s, branches 200 ~24.0 s, anonymous profile 401 ~558 ms, empty login 400 ~23.2 s.
Both real ApiClient callbacks and native HTTP passed. Isolated diagnostic vault does not replace user tokens.

Secret scan and `git diff --check`: PASS. No backend/web build required: those sources did not change.
No new QR/order/payment mutation was performed. Full SignalR event acceptance is not claimed by this fix.
Network-error/retry transition is covered in unit state tests; deliberate offline UI injection was not
performed on the user's authenticated device. Observed real slow responses verified loading behavior.

Final Home data rendering acceptance: **ANDROID HOME DATA: PASS** (Samsung real Catalog data shown).
