package com.restaurant.viettable;

import android.os.Bundle;
import android.os.SystemClock;
import android.view.View;
import android.view.ViewGroup;
import android.view.animation.OvershootInterpolator;
import android.widget.ImageView;
import android.widget.Toast;

import androidx.activity.EdgeToEdge;
import androidx.activity.OnBackPressedCallback;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.view.WindowInsetsControllerCompat;
import androidx.fragment.app.Fragment;
import androidx.fragment.app.FragmentManager;
import androidx.fragment.app.FragmentTransaction;

import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.network.ApiClient;
import com.restaurant.viettable.network.BiometricTokenStore;
import com.restaurant.viettable.ui.auth.AppLockFragment;
import com.restaurant.viettable.ui.auth.WelcomeFragment;
import com.restaurant.viettable.ui.lock.AppLock;
import com.restaurant.viettable.ui.lock.LockExempt;
import com.restaurant.viettable.util.AppLockPolicy;
import com.restaurant.viettable.network.SignalRManager;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.reminder.ReservationReminders;
import com.restaurant.viettable.ui.auth.LoginFragment;
import com.restaurant.viettable.ui.reservation.ResDetailFragment;
import com.restaurant.viettable.databinding.ActivityMainBinding;
import com.restaurant.viettable.navigation.NavigationCoordinator;
import com.restaurant.viettable.ui.auth.SplashFragment;
import com.restaurant.viettable.ui.home.HomeFragment;
import com.restaurant.viettable.ui.order.OrderHistoryFragment;
import com.restaurant.viettable.ui.profile.ProfileFragment;
import com.restaurant.viettable.ui.reservation.ResDateFragment;
import com.restaurant.viettable.ui.session.DiningSessionFragment;
import com.restaurant.viettable.ui.session.QrScanFragment;

public class MainActivity extends AppCompatActivity implements NavigationCoordinator, AppStateManager.StateChangeListener,
        AppLockFragment.Host {

    private ActivityMainBinding binding;
    private AppStateManager appStateManager;
    private WindowInsetsControllerCompat insetsController;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        EdgeToEdge.enable(this);
        super.onCreate(savedInstanceState);
        binding = ActivityMainBinding.inflate(getLayoutInflater());
        setContentView(binding.getRoot());

        insetsController = new WindowInsetsControllerCompat(getWindow(), getWindow().getDecorView());
        // "Phố đêm" is always dark: light system bar icons on every screen.
        insetsController.setAppearanceLightNavigationBars(false);
        insetsController.setAppearanceLightStatusBars(false);

        ApiClient.initialize(getApplicationContext());
        ReservationReminders.initialize(getApplicationContext());
        com.restaurant.viettable.network.BiometricTokenStore.initialize(getApplicationContext());
        com.restaurant.viettable.util.ImageLoader.getInstance().initialize(getApplicationContext());
        com.restaurant.viettable.data.GreetingNameStore.getInstance().initialize(getApplicationContext());
        ApiClient.getInstance().setSessionInvalidatedListener(this::showExpiredSession);
        SignalRManager.getInstance().initialize();
        appStateManager = AppStateManager.getInstance();
        appStateManager.initializeBranchSelection(getApplicationContext(),
                MobileRepository.getInstance().hasSession() ? BiometricTokenStore.getInstance().sessionAccount() : null);
        appStateManager.setLoggedIn(MobileRepository.getInstance().hasSession());
        appStateManager.addListener(this);

        MobileRepository.getInstance().initializePublicCatalog(getApplicationContext(), () -> {
            if (appStateManager.isLoggedIn()) MobileRepository.getInstance().refreshBootstrap(null);
        });

        setupSystemBarsAndInsets();
        setupBottomNavigation();
        // While locked, Back leaves the app (to the background); it never reaches the screens underneath.
        lockBackCallback = new OnBackPressedCallback(false) {
            @Override public void handleOnBackPressed() { moveTaskToBack(true); }
        };
        getOnBackPressedDispatcher().addCallback(this, lockBackCallback);

        if (savedInstanceState == null) {
            // Cold start ALWAYS starts at SplashFragment
            setBottomNavVisible(false);
            setFabVisible(false);
            navigateTo(SplashFragment.newInstance(), false);
            takeReminderIntent(getIntent());
        } else {
            pendingReservationId = savedInstanceState.getString(STATE_PENDING_RESERVATION);
        }
    }

    // ---- Tapped booking reminder: open that reservation once signed in, past the splash and unlocked ----

    private static final String STATE_PENDING_RESERVATION = "pendingReservationId";
    private String pendingReservationId;
    private boolean pendingReservationLookup;

    @Override
    protected void onNewIntent(android.content.Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        takeReminderIntent(intent);
    }

    @Override
    protected void onSaveInstanceState(@androidx.annotation.NonNull Bundle outState) {
        super.onSaveInstanceState(outState);
        outState.putString(STATE_PENDING_RESERVATION, pendingReservationId);
    }

    private void takeReminderIntent(android.content.Intent intent) {
        String id = intent == null ? null : intent.getStringExtra(ReservationReminders.EXTRA_RESERVATION_ID);
        if (id == null) return;
        intent.removeExtra(ReservationReminders.EXTRA_RESERVATION_ID); // a later recreate must not open it again
        pendingReservationId = id;
        pendingReservationLookup = false;
        binding.getRoot().post(this::openPendingReservation);
    }

    private void openPendingReservation() {
        if (pendingReservationId == null || isFinishing() || isLocked()) return;
        FragmentManager fm = getSupportFragmentManager();
        if (fm.isStateSaved() || !MobileRepository.getInstance().hasSession()) return;
        Fragment top = fm.findFragmentById(R.id.fragment_container);
        if (top == null || top instanceof SplashFragment || top instanceof WelcomeFragment || top instanceof LoginFragment) return;
        for (Reservation r : appStateManager.getReservations()) {
            if (pendingReservationId.equals(r.getId())) {
                pendingReservationId = null;
                navigateTo(ResDetailFragment.newInstance(r), true);
                return;
            }
        }
        if (pendingReservationLookup) return;
        pendingReservationLookup = true; // not loaded yet (cold start): look once, give up if it is not this user's
        String wanted = pendingReservationId;
        MobileRepository.getInstance().loadReservations(new MobileRepository.Callback<java.util.List<Reservation>>() {
            @Override public void onSuccess(java.util.List<Reservation> value) {
                boolean mine = false;
                for (Reservation r : value) if (wanted.equals(r.getId())) mine = true;
                if (!mine && wanted.equals(pendingReservationId)) pendingReservationId = null; // another account's
                else openPendingReservation(); // still waits for the unlock if the app is locked
            }
            @Override public void onError(com.restaurant.viettable.network.ApiException error) { pendingReservationLookup = false; }
        });
    }

    @Override
    protected void onStart() {
        super.onStart();
        SignalRManager.getInstance().resume();
        BiometricTokenStore store = BiometricTokenStore.getInstance();
        boolean fingerprintOn = store.isEnabled(store.sessionAccount());
        if (AppLock.get().shouldLockOnForeground(MobileRepository.getInstance().hasSession(), fingerprintOn,
                isLocked(), AppLockPolicy.timeoutMs(BuildConfig.APP_LOCK_TIMEOUT_OVERRIDE_MS))) {
            showAppLock();
        }
        syncLockUi();
    }

    // ---- App lock (fingerprint on): drawn above everything, the screens underneath keep their state ----

    private OnBackPressedCallback lockBackCallback;

    private boolean isLocked() {
        return getSupportFragmentManager().findFragmentByTag(AppLockFragment.TAG) != null;
    }

    private void showAppLock() {
        FragmentManager fm = getSupportFragmentManager();
        if (isLocked() || fm.isStateSaved()) return;
        fm.beginTransaction().replace(R.id.lock_container, new AppLockFragment(), AppLockFragment.TAG).commitNow();
    }

    private void hideAppLock() {
        FragmentManager fm = getSupportFragmentManager();
        Fragment lock = fm.findFragmentByTag(AppLockFragment.TAG);
        if (lock != null) {
            if (fm.isStateSaved()) fm.beginTransaction().remove(lock).commitNowAllowingStateLoss();
            else fm.beginTransaction().remove(lock).commitNow();
        }
        syncLockUi();
    }

    private void syncLockUi() {
        boolean locked = isLocked();
        binding.lockContainer.setVisibility(locked ? View.VISIBLE : View.GONE);
        // Screen readers must not reach the screens underneath either.
        binding.fragmentContainer.setImportantForAccessibility(locked
                ? View.IMPORTANT_FOR_ACCESSIBILITY_NO_HIDE_DESCENDANTS : View.IMPORTANT_FOR_ACCESSIBILITY_AUTO);
        binding.bottomNav.setImportantForAccessibility(locked
                ? View.IMPORTANT_FOR_ACCESSIBILITY_NO_HIDE_DESCENDANTS : View.IMPORTANT_FOR_ACCESSIBILITY_AUTO);
        binding.btnNavQr.setImportantForAccessibility(locked
                ? View.IMPORTANT_FOR_ACCESSIBILITY_NO_HIDE_DESCENDANTS : View.IMPORTANT_FOR_ACCESSIBILITY_AUTO);
        if (lockBackCallback != null) {
            lockBackCallback.setEnabled(locked);
            if (locked) {
                // Newest callback wins: put the lock's Back above any screen's own Back handling.
                lockBackCallback.remove();
                getOnBackPressedDispatcher().addCallback(this, lockBackCallback);
            }
        }
    }

    @Override
    public void onAppUnlocked() { hideAppLock(); openPendingReservation(); }

    @Override
    public void onLockLoggedOut() {
        hideAppLock();
        resetTo(WelcomeFragment.newInstance());
    }

    @Override
    protected void onResumeFragments() {
        super.onResumeFragments();
        if (pendingExpiredSession) showExpiredSession();
        else if (pendingReset != null) resetTo(pendingReset);
        openPendingReservation();
    }

    private boolean pendingExpiredSession;
    private Fragment pendingReset;

    private void showExpiredSession() {
        if (isFinishing()) return;
        if (getSupportFragmentManager().isStateSaved()) { pendingExpiredSession = true; return; }
        pendingExpiredSession = false;
        hideAppLock(); // no session left to protect
        getSupportFragmentManager().popBackStackImmediate(null, FragmentManager.POP_BACK_STACK_INCLUSIVE);
        setBottomNavVisible(false);
        setFabVisible(false);
        navigateTo(new com.restaurant.viettable.ui.auth.LoginFragment(), false);
    }

    @Override
    protected void onStop() {
        if (!isChangingConfigurations()) {
            Fragment top = getSupportFragmentManager().findFragmentById(R.id.fragment_container);
            AppLock.get().onAppBackground(top instanceof LockExempt);
        }
        SignalRManager.getInstance().pause();
        super.onStop();
    }

    private void setupSystemBarsAndInsets() {
        // Prevent BottomNavigationView from consuming or adding bottom window insets padding to itself
        ViewCompat.setOnApplyWindowInsetsListener(binding.bottomNav, (v, insets) -> {
            v.setPadding(0, 0, 0, 0);
            return insets;
        });

        ViewCompat.setOnApplyWindowInsetsListener(binding.mainLayout, (v, windowInsets) -> {
            Insets statusBars = windowInsets.getInsets(WindowInsetsCompat.Type.statusBars());
            Insets navigationBars = windowInsets.getInsets(WindowInsetsCompat.Type.navigationBars());

            // Adjust bottomNav margin so the floating dock hovers comfortably above the system navigation bar
            int navBarHeight = getResources().getDimensionPixelSize(R.dimen.bottom_nav_height);
            binding.bottomNav.setPadding(0, 0, 0, 0);
            ViewGroup.MarginLayoutParams navLp = (ViewGroup.MarginLayoutParams) binding.bottomNav.getLayoutParams();
            if (navLp != null) {
                navLp.height = navBarHeight;
                int baseBottomMargin = getResources().getDimensionPixelSize(R.dimen.pd_nav_margin_bottom);
                navLp.bottomMargin = navigationBars.bottom + baseBottomMargin;
                binding.bottomNav.setLayoutParams(navLp);
            }

            return windowInsets;
        });
    }

    private void setupBottomNavigation() {
        // The raised centre button stands in for the nav_qr tab under it: same tap, same destination.
        binding.btnNavQr.setOnClickListener(v -> {
            View qrTab = binding.bottomNav.findViewById(R.id.nav_qr);
            if (qrTab != null) qrTab.performClick();
        });
        binding.bottomNav.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, or, ob) ->
                moveNavDot(binding.bottomNav.getSelectedItemId(), false));
        binding.bottomNav.setOnItemSelectedListener(item -> {
            getSupportFragmentManager().popBackStack(null, FragmentManager.POP_BACK_STACK_INCLUSIVE);
            setFabVisible(true);
            setStatusBarLightIcons(false); // Default root screens have light header/background

            int itemId = item.getItemId();
            binding.bottomNav.post(() -> moveNavDot(itemId, true));
            if (itemId == R.id.nav_home) {
                navigateTo(HomeFragment.newInstance(), false);
                return true;
            } else if (itemId == R.id.nav_reservation) {
                navigateTo(ResDateFragment.newInstance(), false);
                return true;
            } else if (itemId == R.id.nav_qr) {
                navigateTo(QrScanFragment.newInstance(), false);
                return true;
            } else if (itemId == R.id.nav_orders) {
                navigateTo(OrderHistoryFragment.newInstance(), false);
                return true;
            } else if (itemId == R.id.nav_profile) {
                navigateTo(ProfileFragment.newInstance(), false);
                return true;
            }
            return false;
        });
    }

    @Override
    public void navigateTo(Fragment fragment, boolean addToBackStack) {
        FragmentTransaction ft = getSupportFragmentManager().beginTransaction();
        ft.setCustomAnimations(
                android.R.anim.fade_in,
                android.R.anim.fade_out,
                android.R.anim.fade_in,
                android.R.anim.fade_out
        );
        ft.replace(R.id.fragment_container, fragment);
        if (addToBackStack) {
            ft.addToBackStack(null);
        }
        ft.commit();
        // Splash/login finished: a tapped reminder can now open its reservation.
        if (pendingReservationId != null) binding.getRoot().post(this::openPendingReservation);
    }

    @Override
    public void resetTo(Fragment fragment) {
        if (getSupportFragmentManager().isStateSaved()) { pendingReset = fragment; return; } // e.g. reply arrived in background
        pendingReset = null;
        getSupportFragmentManager().popBackStackImmediate(null, FragmentManager.POP_BACK_STACK_INCLUSIVE);
        setBottomNavVisible(false);
        setFabVisible(false);
        navigateTo(fragment, false);
    }

    @Override
    public void navigateBack() {
        FragmentManager fm = getSupportFragmentManager();
        if (fm.getBackStackEntryCount() > 0) {
            fm.popBackStack();
        } else {
            // Return to Home tab if on root
            if (binding.bottomNav.getSelectedItemId() != R.id.nav_home) {
                switchTab(R.id.nav_home);
            } else {
                finish();
            }
        }
    }

    @Override
    public void switchTab(int navItemId) {
        setBottomNavVisible(true);
        setFabVisible(true);
        if (binding.bottomNav.getSelectedItemId() == navItemId) {
            getSupportFragmentManager().popBackStack(null, androidx.fragment.app.FragmentManager.POP_BACK_STACK_INCLUSIVE);
            setStatusBarLightIcons(false);
            if (navItemId == R.id.nav_home) {
                navigateTo(HomeFragment.newInstance(), false);
            } else if (navItemId == R.id.nav_reservation) {
                navigateTo(ResDateFragment.newInstance(), false);
            } else if (navItemId == R.id.nav_qr) {
                navigateTo(QrScanFragment.newInstance(), false);
            } else if (navItemId == R.id.nav_orders) {
                navigateTo(OrderHistoryFragment.newInstance(), false);
            } else if (navItemId == R.id.nav_profile) {
                navigateTo(ProfileFragment.newInstance(), false);
            }
        } else {
            binding.bottomNav.setSelectedItemId(navItemId);
        }
    }

    @Override
    public void openDiningSession() {
        navigateTo(DiningSessionFragment.newInstance(), true);
    }

    @Override
    public void setBottomNavVisible(boolean visible) {
        int visibility = visible ? View.VISIBLE : View.GONE;
        binding.bottomNav.setVisibility(visibility);
        binding.btnNavQr.setVisibility(visibility);
        binding.navDot.setVisibility(visibility);
        if (visible) binding.bottomNav.post(() -> moveNavDot(binding.bottomNav.getSelectedItemId(), false));
    }

    /** Puts the yellow dot under the selected tab's icon; it slides when the tab changes (instant if animations are off). */
    private void moveNavDot(int itemId, boolean animate) {
        View tab = binding.bottomNav.findViewById(itemId);
        ImageView icon = tab == null ? null : findIcon(tab);
        if (icon == null || icon.getWidth() == 0) return;
        int[] nav = new int[2];
        int[] pos = new int[2];
        binding.bottomNav.getLocationInWindow(nav);
        icon.getLocationInWindow(pos);
        float gap = 5 * getResources().getDisplayMetrics().density;
        float x = pos[0] - nav[0] + (icon.getWidth() - binding.navDot.getWidth()) / 2f;
        float y = pos[1] - nav[1] + icon.getHeight() + gap;
        float alpha = itemId == R.id.nav_qr ? 0f : 1f; // the raised yellow button already marks the QR tab
        binding.navDot.setTranslationY(y);
        if (animate) {
            binding.navDot.animate().translationX(x).alpha(alpha).setDuration(250)
                    .setInterpolator(new OvershootInterpolator(1.2f)).start();
        } else {
            binding.navDot.animate().cancel();
            binding.navDot.setTranslationX(x);
            binding.navDot.setAlpha(alpha);
        }
    }

    private static ImageView findIcon(View view) {
        if (view instanceof ImageView) return (ImageView) view;
        if (!(view instanceof ViewGroup)) return null;
        ViewGroup group = (ViewGroup) view;
        for (int i = 0; i < group.getChildCount(); i++) {
            ImageView icon = findIcon(group.getChildAt(i));
            if (icon != null) return icon;
        }
        return null;
    }

    @Override
    public void setFabVisible(boolean visible) {
        // The current activity layout has no floating action button.
    }

    @Override
    public void setStatusBarLightIcons(boolean isLightIcons) {
        if (insetsController != null) {
            // Every screen sits on the dark "Phố đêm" page now, so the status bar icons stay light whatever the screen asks.
            insetsController.setAppearanceLightStatusBars(false);
        }
    }

    @Override
    public void onStateChanged() {
        // State updated
    }

    @Override
    protected void onDestroy() {
        ApiClient.getInstance().setSessionInvalidatedListener(null);
        super.onDestroy();
        if (appStateManager != null) {
            appStateManager.removeListener(this);
        }
    }
}
