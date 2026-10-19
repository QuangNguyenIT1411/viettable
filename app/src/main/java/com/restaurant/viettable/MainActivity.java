package com.restaurant.viettable;

import android.os.Bundle;
import android.os.SystemClock;
import android.view.View;
import android.view.ViewGroup;
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
        insetsController.setAppearanceLightNavigationBars(true);
        insetsController.setAppearanceLightStatusBars(true);

        ApiClient.initialize(getApplicationContext());
        com.restaurant.viettable.network.BiometricTokenStore.initialize(getApplicationContext());
        com.restaurant.viettable.util.ImageLoader.getInstance().initialize(getApplicationContext());
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
        }
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
    public void onAppUnlocked() { hideAppLock(); }

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
                int baseBottomMargin = (int) (12 * getResources().getDisplayMetrics().density);
                navLp.bottomMargin = navigationBars.bottom + baseBottomMargin;
                binding.bottomNav.setLayoutParams(navLp);
            }

            return windowInsets;
        });
    }

    private void setupBottomNavigation() {
        binding.bottomNav.setOnItemSelectedListener(item -> {
            getSupportFragmentManager().popBackStack(null, FragmentManager.POP_BACK_STACK_INCLUSIVE);
            setFabVisible(true);
            setStatusBarLightIcons(false); // Default root screens have light header/background

            int itemId = item.getItemId();
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
        binding.bottomNav.setVisibility(visible ? View.VISIBLE : View.GONE);
    }

    @Override
    public void setFabVisible(boolean visible) {
        // The current activity layout has no floating action button.
    }

    @Override
    public void setStatusBarLightIcons(boolean isLightIcons) {
        if (insetsController != null) {
            // isLightIcons == true -> white icons (for dark green headers)
            // isLightIcons == false -> dark icons (for white/cream headers)
            insetsController.setAppearanceLightStatusBars(!isLightIcons);
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
