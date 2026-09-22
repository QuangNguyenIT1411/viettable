package com.restaurant.viettable.ui.auth;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentWelcomeBinding;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.branch.BranchListFragment;
import com.restaurant.viettable.util.ImageLoader;

public class WelcomeFragment extends BaseFragment {

    private static final String WELCOME_BG_URL =
            "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?w=1080&h=1920&fit=crop&q=85";
    private static final String WELCOME_LOGO_URL =
            "https://img.icons8.com/3d-fluency/200/lotus.png";

    public static WelcomeFragment newInstance() {
        return new WelcomeFragment();
    }

    private FragmentWelcomeBinding binding;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentWelcomeBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        setStatusBarLightIcons(false); // Dark icons on light photographic background

        // Dynamic online image & 3D emblem loading with bundled local fallbacks
        ImageLoader.getInstance().load(WELCOME_BG_URL, R.drawable.splash_bg, binding.ivWelcomeBg);
        ImageLoader.getInstance().load(WELCOME_LOGO_URL, R.drawable.ic_splash_lotus_3d, binding.ivWelcomeLogo);

        binding.btnLogin.setOnClickListener(v -> navigateTo(new LoginFragment()));
        binding.btnRegister.setOnClickListener(v -> navigateTo(new RegisterFragment()));
        binding.btnSkipAuth.setOnClickListener(v -> {
            appStateManager.setLoggedIn(false);
            navigateTo(new BranchListFragment());
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
