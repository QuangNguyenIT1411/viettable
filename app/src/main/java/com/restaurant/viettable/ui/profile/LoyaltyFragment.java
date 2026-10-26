package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.TextView;
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

    /** The tier's own benefits from the server, one tick per line; nothing is shown when the list is empty. */
    private void bindBenefits(com.restaurant.viettable.model.LoyaltyInfo info) {
        binding.layoutBenefits.removeAllViews();
        boolean any = !info.getBenefits().isEmpty();
        binding.tvBenefitsTitle.setVisibility(any ? View.VISIBLE : View.GONE);
        binding.cardBenefits.setVisibility(any ? View.VISIBLE : View.GONE);
        binding.tvBenefitsTitle.setText("Đặc quyền hạng " + info.getTierName());
        float density = getResources().getDisplayMetrics().density;
        for (int i = 0; i < info.getBenefits().size(); i++) {
            android.widget.LinearLayout row = new android.widget.LinearLayout(requireContext());
            row.setOrientation(android.widget.LinearLayout.HORIZONTAL);
            row.setGravity(android.view.Gravity.CENTER_VERTICAL);
            android.widget.ImageView tick = new android.widget.ImageView(requireContext());
            tick.setImageResource(com.restaurant.viettable.R.drawable.ic_check);
            tick.setImageTintList(androidx.core.content.ContextCompat.getColorStateList(requireContext(),
                    com.restaurant.viettable.R.color.colorSuccess));
            tick.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            row.addView(tick, new android.widget.LinearLayout.LayoutParams((int) (24 * density), (int) (24 * density)));
            TextView text = new TextView(requireContext());
            text.setText(info.getBenefits().get(i));
            text.setTextSize(13);
            text.setTextColor(androidx.core.content.ContextCompat.getColor(requireContext(),
                    com.restaurant.viettable.R.color.colorTextPrimary));
            android.widget.LinearLayout.LayoutParams textLp = new android.widget.LinearLayout.LayoutParams(0,
                    android.view.ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
            textLp.setMarginStart((int) (10 * density));
            row.addView(text, textLp);
            android.widget.LinearLayout.LayoutParams rowLp = new android.widget.LinearLayout.LayoutParams(
                    android.view.ViewGroup.LayoutParams.MATCH_PARENT, android.view.ViewGroup.LayoutParams.WRAP_CONTENT);
            if (i > 0) rowLp.topMargin = (int) (12 * density);
            binding.layoutBenefits.addView(row, rowLp);
        }
    }

    private void bindTierProgress(com.restaurant.viettable.model.LoyaltyInfo info) {
        if (binding == null) return;
        String progress = com.restaurant.viettable.util.LoyaltyDisplayRules.progressPoints(info);
        binding.cardTierProgress.setVisibility(progress == null ? View.GONE : View.VISIBLE);
        binding.tvCardPoints.setText(String.format(Locale.GERMANY, "%,d pts", info.getAvailablePoints()));
        binding.tvCardNumber.setText(info.getCardNumber());
        bindBenefits(info);
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
