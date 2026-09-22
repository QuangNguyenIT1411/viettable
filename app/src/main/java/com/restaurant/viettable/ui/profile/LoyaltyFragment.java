package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentLoyaltyBinding;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.Locale;

public class LoyaltyFragment extends BaseFragment {

    private FragmentLoyaltyBinding binding;

    public static LoyaltyFragment newInstance() {
        return new LoyaltyFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentLoyaltyBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        User user = appStateManager.getCurrentUser();
        if (user != null) {
            binding.tvCardHolder.setText(user.getFullName().toUpperCase());
            binding.tvCardPoints.setText(String.format(Locale.GERMANY, "%,d pts", user.getLoyaltyPoints()));
            binding.tvCardTier.setText(user.getMembershipTier().toUpperCase());
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
