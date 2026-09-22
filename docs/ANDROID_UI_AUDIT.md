# Android UI audit

Audit date: 2026-09-23
Scope: all 72 production `Fragment` classes under `app/src/main/java/com/restaurant/viettable/ui`.

## Classification

- `OK`: production behavior and state handling were already suitable for this release scope.
- `NEEDS_POLISH`: the flow worked but had a form, presentation, retry, formatting, or accessibility issue.
- `BROKEN_STATE`: a legitimate empty/error state could render stale, blank, or misleading content.
- `STATIC/PLACEHOLDER`: production UI could display a value not supplied by the API.
- `ACCESSIBILITY_ISSUE`: an important icon action or touch target was unlabeled or undersized.

The table records the state found during this audit and the resulting disposition. It does not claim that every screen has been manually exercised with every possible backend state.

| Group | Screen | Audit classification | Result |
|---|---|---|---|
| auth | SplashFragment | OK | Kept existing startup/session routing. |
| auth | WelcomeFragment | OK | Kept existing entry actions. |
| auth | LoginFragment | NEEDS_POLISH, ACCESSIBILITY_ISSUE | Added field errors/focus, IME submit, submit loading label, and labeled 48dp back action. |
| auth | RegisterFragment | STATIC/PLACEHOLDER, NEEDS_POLISH | Removed prefilled person/phone and pre-checked terms; aligned password rule with backend; added field errors, IME flow, and loading guard. |
| auth | ForgotPasswordFragment | NEEDS_POLISH | Added inline error/focus, IME submit, and request button state. |
| auth | ResetPasswordFragment | NEEDS_POLISH | Added inline password errors, IME flow, and request button state. |
| home | HomeFragment | STATIC/PLACEHOLDER | Removed the hardcoded live promotion and fake voucher action; retained canonical branch/menu/session/reservation data. |
| home | NotificationFragment | BROKEN_STATE | Added cached-content-aware loading, filtered empty state, error, and real retry. |
| home | SearchFragment | ACCESSIBILITY_ISSUE | Labeled and enlarged back/clear actions. |
| home | SearchResultFragment | ACCESSIBILITY_ISSUE | Labeled and enlarged back/search actions; removed branch preview placeholder. |
| branch | BranchListFragment | BROKEN_STATE, STATIC/PLACEHOLDER | Added loading/empty/error/retry; hid unsupported map; stopped showing unavailable distance/rating/open data. |
| branch | BranchDetailFragment | STATIC/PLACEHOLDER | Removed first-branch fallback; missing arguments return safely; unavailable rating/distance/hours/facilities/map are hidden. |
| branch | BranchMapFragment | STATIC/PLACEHOLDER | Removed all production routes and replaced any direct opening with an explicit unavailable state; no fake coordinates are presented. |
| branch | ChangeBranchFragment | NEEDS_POLISH | Removed static branch preview text and labeled back action. |
| food | MenuFragment | ACCESSIBILITY_ISSUE | Labeled/enlarged navigation and search actions; canonical menu behavior retained. |
| food | CategoryFragment | ACCESSIBILITY_ISSUE | Labeled/enlarged back action; canonical category filtering retained. |
| food | FoodDetailFragment | STATIC/PLACEHOLDER, ACCESSIBILITY_ISSUE | Removed first-food fallback and sample detail text; hid unavailable metadata; added dynamic favorite labels and 48dp actions. |
| food | FavoritesFragment | BROKEN_STATE, STATIC/PLACEHOLDER | Added loading/empty/error/retry; removed unsupported favorite-branch tab; retained API-backed dish favorites. |
| reservation | ResDateFragment | ACCESSIBILITY_ISSUE | Labeled/enlarged back action; real branch/date selection retained. |
| reservation | ResTimeFragment | ACCESSIBILITY_ISSUE | Labeled/enlarged back action; availability flow retained. |
| reservation | ResGuestCountFragment | ACCESSIBILITY_ISSUE | Labeled/enlarged back and quantity actions. |
| reservation | ResAreaFragment | ACCESSIBILITY_ISSUE | Labeled/enlarged back action. |
| reservation | ResTableLayoutFragment | STATIC/PLACEHOLDER, ACCESSIBILITY_ISSUE | Removed preview table text; real available-table default selection retained; labeled back action. |
| reservation | ResNoTableFragment | STATIC/PLACEHOLDER, ACCESSIBILITY_ISSUE | Removed hardcoded branch/time summary; labeled back action. |
| reservation | ResNoteFragment | STATIC/PLACEHOLDER, ACCESSIBILITY_ISSUE | Removed sample contact/branch/table values; labeled back action. |
| reservation | ResDepositFragment | ACCESSIBILITY_ISSUE | Labeled/enlarged back action; backend deposit policy retained. |
| reservation | ResConfirmationFragment | BROKEN_STATE, STATIC/PLACEHOLDER | Removed first-reservation fallback/sample values; missing argument returns safely to Home. |
| reservation | ResHistoryFragment | BROKEN_STATE | Added per-tab loading/empty/error/retry. |
| reservation | ResDetailFragment | BROKEN_STATE, STATIC/PLACEHOLDER | Removed first-reservation fallback/sample values; missing argument returns safely. |
| reservation | ResChangeFragment | BROKEN_STATE, ACCESSIBILITY_ISSUE | Removed first-reservation fallback; labeled/enlarged navigation and quantity actions. |
| reservation | ResCancelFragment | BROKEN_STATE, ACCESSIBILITY_ISSUE | Removed first-reservation fallback; existing destructive confirmation retained; labeled back action. |
| preorder | PreorderMenuFragment | STATIC/PLACEHOLDER, ACCESSIBILITY_ISSUE | Removed sample reservation/table summary; labeled/enlarged back action. |
| preorder | PreorderCartFragment | STATIC/PLACEHOLDER, ACCESSIBILITY_ISSUE | Removed sample reservation/table summary; labeled/enlarged back action. |
| preorder | PreorderConfirmFragment | OK | Server-backed submit and confirmation behavior retained. |
| preorder | PreorderDetailFragment | STATIC/PLACEHOLDER | Removed sample reservation/table/time summary. |
| session | QrScanFragment | NEEDS_POLISH | Added IME submit and correct CameraX opt-in; existing permission/manual/error/retry flow retained. |
| session | QrValidationFragment | OK | Existing Gateway validation routing retained. |
| session | CheckinConfirmFragment | STATIC/PLACEHOLDER | Removed sample table/customer content; API result continues to bind values. |
| session | ReservationMismatchFragment | STATIC/PLACEHOLDER | Removed sample table IDs and made action labels generic until canonical values bind. |
| session | WalkinConfirmFragment | STATIC/PLACEHOLDER | Removed sample table value. |
| session | DiningSessionFragment | OK | Existing loading/empty/error/retry and no-scenario behavior retained; ISO start time now formatted for users. |
| session | DineinMenuFragment | STATIC/PLACEHOLDER | Removed sample table header. |
| session | DineinCartFragment | STATIC/PLACEHOLDER | Removed sample destination header. |
| session | OrderConfirmFragment | OK | Existing server order confirmation retained. |
| session | CallStaffFragment | STATIC/PLACEHOLDER | Removed sample branch/table summary. |
| session | ServiceStatusFragment | BROKEN_STATE, STATIC/PLACEHOLDER | Removed fake “staff is coming” banner; added loading/empty/error/retry and no-session handling. |
| order | CurrentOrdersFragment | BROKEN_STATE, STATIC/PLACEHOLDER | Added no-session loading/empty/error/retry and removed sample totals/table. |
| order | OrderDetailFragment | STATIC/PLACEHOLDER | Removed sample table/time/branch summary. |
| order | OrderItemDetailFragment | OK | Canonical order-item status behavior retained. |
| order | OrderTrackingFragment | BROKEN_STATE, STATIC/PLACEHOLDER | Added loading/empty/error/retry and removed sample order ID/status. |
| order | OrderHistoryFragment | OK | Existing active/history empty states retained. |
| payment | PaymentCheckoutFragment | STATIC/PLACEHOLDER | Removed sample amount and formatted session timestamp. |
| payment | VoucherSelectFragment | OK | Existing server-owned quote selection retained. |
| payment | LoyaltySelectFragment | OK | Existing server-owned quote selection retained. |
| payment | SplitBillFragment | OK | Amount/equal-parts capability wording retained; unsupported item split remains visibly disabled. |
| payment | PaymentTypeFragment | STATIC/PLACEHOLDER | Removed sample table/branch summary; provider availability remains server-authoritative. |
| payment | OnlinePaymentMethodFragment | OK | Existing disabled-provider/error flow retained; no provider success is faked. |
| payment | OnsitePaymentMethodFragment | OK | Existing onsite choices retained. |
| payment | PaymentLocationFragment | STATIC/PLACEHOLDER | Removed sample table sentence. |
| payment | PaymentProcessingFragment | NEEDS_POLISH | Provider expiry is now formatted rather than exposing ISO-8601. |
| payment | PaymentSuccessFragment | STATIC/PLACEHOLDER | Removed sample table/branch summary; backend success remains authoritative. |
| payment | PaymentFailedFragment | STATIC/PLACEHOLDER | Removed sample table sentence; backend failure/retry flow retained. |
| payment | OnsiteStatusFragment | STATIC/PLACEHOLDER | Removed sample table text. |
| payment | InvoiceFragment | STATIC/PLACEHOLDER | Removed sample branch/amount and formatted session timestamp; missing quote handling retained. |
| profile | ProfileFragment | STATIC/PLACEHOLDER, ACCESSIBILITY_ISSUE | Removed sample phone and enlarged/labeled edit action. |
| profile | EditProfileFragment | STATIC/PLACEHOLDER | Removed sample phone; existing API-backed profile update retained. |
| profile | SettingsFragment | OK | Existing server-backed settings retained. |
| profile | LoyaltyFragment | OK | Existing server-backed loyalty projection retained. |
| profile | CustomerVoucherFragment | BROKEN_STATE, STATIC/PLACEHOLDER | Added loading/empty/error/retry; removed fake “copy/apply” action and sample voucher content. |
| profile | SupportFragment | STATIC/PLACEHOLDER | Hid unconfigured hotline/live-chat controls; real FAQ and support request remain. |
| profile | ReviewHistoryFragment | OK | Existing loading/empty/error/retry and CRUD actions retained. |
| profile | ReviewFragment | ACCESSIBILITY_ISSUE, NEEDS_POLISH | Added 48dp labeled/focusable rating actions and submit loading state. |

## Cross-cutting changes

- Production placeholder scan for the requested markers (`Mock`, scenario IDs, `Bàn B12`, sample customer/contact values) now returns no matches in production Java/layout resources.
- `ImageLoader` clears recycled content before every request and renders a neutral local background when an image URL is absent or fails.
- `DisplayFormatters` converts backend ISO timestamps to `HH:mm, dd/MM/yyyy` instead of exposing wire-format values.
- Core-library desugaring supports `java.time` on the declared minimum SDK 24.
- The app remains intentionally light-themed; it does not claim complete dark-mode support.

## Remaining UI work

- Lint still reports legacy warnings, primarily hardcoded Vietnamese strings, unused resources, `setText` concatenation, and decorative images without explicit `@null`. No fatal lint issue remains. These are future cleanup, not proof of a runtime defect by themselves.
- Full large-font/TalkBack certification has not been completed. Samsung inspection covered real high font scaling and critical automated paths, but not all 72 screens.
- Real map/geolocation remains unavailable until backend coordinates exist.
