package com.restaurant.viettable;

import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Toast;

import androidx.activity.EdgeToEdge;
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

public class MainActivity extends AppCompatActivity implements NavigationCoordinator, AppStateManager.StateChangeListener {

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
        ApiClient.getInstance().setSessionInvalidatedListener(this::showExpiredSession);
        SignalRManager.getInstance().initialize();
        appStateManager = AppStateManager.getInstance();
        appStateManager.setLoggedIn(MobileRepository.getInstance().hasSession());
        appStateManager.addListener(this);

        if (appStateManager.isLoggedIn()) {
            MobileRepository.getInstance().refreshBootstrap(null);
        }

        setupSystemBarsAndInsets();
        setupBottomNavigation();

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
        SignalRManager.getInstance().startIfAuthenticated();
    }

    @Override
    protected void onResumeFragments() {
        super.onResumeFragments();
        if (pendingExpiredSession) showExpiredSession();
    }

    private boolean pendingExpiredSession;

    private void showExpiredSession() {
        if (isFinishing()) return;
        if (getSupportFragmentManager().isStateSaved()) { pendingExpiredSession = true; return; }
        pendingExpiredSession = false;
        getSupportFragmentManager().popBackStackImmediate(null, FragmentManager.POP_BACK_STACK_INCLUSIVE);
        setBottomNavVisible(false);
        setFabVisible(false);
        navigateTo(new com.restaurant.viettable.ui.auth.LoginFragment(), false);
    }

    @Override
    protected void onStop() {
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
