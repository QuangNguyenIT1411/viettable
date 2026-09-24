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

public class WelcomeFragment extends BaseFragment {

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

        binding.ivWelcomeBg.setImageResource(R.drawable.splash_bg);
        binding.ivWelcomeLogo.setImageResource(R.drawable.ic_splash_lotus_3d);

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
