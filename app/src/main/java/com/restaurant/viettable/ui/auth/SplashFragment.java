package com.restaurant.viettable.ui.auth;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentSplashBinding;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.home.HomeFragment;
import com.restaurant.viettable.util.ImageLoader;

public class SplashFragment extends BaseFragment {

    private static final String SPLASH_BG_URL =
            "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?w=1080&h=1920&fit=crop&q=85";
    private static final String SPLASH_LOGO_URL =
            "https://img.icons8.com/3d-fluency/200/lotus.png";

    public static SplashFragment newInstance() {
        return new SplashFragment();
    }

    private FragmentSplashBinding binding;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private Runnable navigateRunnable;
    private long navigateAt;
    private boolean navigated;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentSplashBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        setStatusBarLightIcons(false); // Dark icons on light luminous photo splash

        // Dynamic online image & icon loading with bundled local fallbacks
        ImageLoader.getInstance().load(SPLASH_BG_URL, R.drawable.splash_bg, binding.ivSplashBg);
        ImageLoader.getInstance().load(SPLASH_LOGO_URL, R.drawable.ic_splash_lotus_3d, binding.ivSplashLogo);

        // Preserve the existing visual duration across pause/recreation. The timer
        // never owns navigation while the FragmentManager has saved its state.
        navigateAt = savedInstanceState == null ? SystemClock.uptimeMillis() + 1200
                : savedInstanceState.getLong("navigateAt", SystemClock.uptimeMillis());
        navigateRunnable = () -> {
            if (navigated || binding == null || !isResumed()
                    || getParentFragmentManager().isStateSaved()
                    || getParentFragmentManager().findFragmentById(R.id.fragment_container) != this) return;
            navigated = true;
            boolean loggedIn = appStateManager.isLoggedIn();
            setBottomNavVisible(loggedIn);
            navigationCoordinator.navigateTo(loggedIn ? HomeFragment.newInstance()
                    : WelcomeFragment.newInstance(), false);
        };
    }

    @Override public void onResume() {
        super.onResume();
        if (!navigated && navigateRunnable != null) handler.postAtTime(navigateRunnable, navigateAt);
    }

    @Override public void onPause() {
        if (navigateRunnable != null) handler.removeCallbacks(navigateRunnable);
        super.onPause();
    }

    @Override public void onSaveInstanceState(@NonNull Bundle outState) {
        outState.putLong("navigateAt", navigateAt);
        super.onSaveInstanceState(outState);
    }

    @Override
    public void onDestroyView() {
        if (navigateRunnable != null) {
            handler.removeCallbacks(navigateRunnable);
        }
        super.onDestroyView();
        binding = null;
    }
}
