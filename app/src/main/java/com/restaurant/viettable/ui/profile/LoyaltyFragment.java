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
            binding.tvCardTier.setText(com.restaurant.viettable.util.StatusLabels.memberTier(user.getMembershipTier()).toUpperCase(Locale.ROOT));
        }

        // Tier progress only from the server's real points and next-tier threshold; hidden until then (owner, 26/10).
        com.restaurant.viettable.data.MobileRepository.getInstance().loadLoyalty(
                new com.restaurant.viettable.data.MobileRepository.Callback<com.restaurant.viettable.model.LoyaltyInfo>() {
                    @Override public void onSuccess(com.restaurant.viettable.model.LoyaltyInfo info) { bindTierProgress(info); }
                    @Override public void onError(com.restaurant.viettable.network.ApiException error) { }
                });
    }

    private void bindTierProgress(com.restaurant.viettable.model.LoyaltyInfo info) {
        if (binding == null) return;
        String progress = com.restaurant.viettable.util.LoyaltyDisplayRules.progressPoints(info);
        binding.cardTierProgress.setVisibility(progress == null ? View.GONE : View.VISIBLE);
        binding.tvCardPoints.setText(String.format(Locale.GERMANY, "%,d pts", info.getAvailablePoints()));
        if (progress == null) return;
        String next = com.restaurant.viettable.util.StatusLabels.memberTier(info.getNextTierCode());
        binding.tvTierProgressTitle.setText("Tiến độ lên hạng " + next);
        binding.tvTierProgressPoints.setText(progress);
        binding.progressTier.setProgress(info.getProgressPercent());
        binding.tvTierProgressHint.setText("Tích lũy thêm " + com.restaurant.viettable.util.LoyaltyDisplayRules.points(
                info.getPointsToNextTier()) + " điểm để lên hạng " + next + ".");
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
