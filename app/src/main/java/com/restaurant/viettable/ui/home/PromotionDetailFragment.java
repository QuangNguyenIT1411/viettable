package com.restaurant.viettable.ui.home;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentPromotionDetailBinding;
import com.restaurant.viettable.model.Promotion;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.ImageLoader;
import com.restaurant.viettable.util.PromotionRules;
import com.restaurant.viettable.util.SafeText;

/** One promotion: picture (when set), discount, conditions, time and where it applies. */
public class PromotionDetailFragment extends BaseFragment {

    private static final String ARG_PROMOTION = "arg_promotion";
    private FragmentPromotionDetailBinding binding;
    private Promotion promotion;

    public static PromotionDetailFragment newInstance(Promotion promotion) {
        PromotionDetailFragment f = new PromotionDetailFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_PROMOTION, promotion);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) promotion = (Promotion) getArguments().getSerializable(ARG_PROMOTION);
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentPromotionDetailBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        if (promotion == null) { navigateBack(); return; }
        binding.tvPromoDetailName.setText(promotion.getName());
        binding.tvPromoDetailBadge.setText(PromotionRules.badge(promotion));
        binding.tvPromoDetailSummary.setText(promotion.getSummary());
        boolean hasDescription = !SafeText.isBlank(promotion.getDescription());
        binding.tvPromoDetailDescription.setVisibility(hasDescription ? View.VISIBLE : View.GONE);
        if (hasDescription) binding.tvPromoDetailDescription.setText(SafeText.clean(promotion.getDescription()));
        binding.tvPromoDetailCondition.setText(PromotionRules.condition(promotion));
        binding.tvPromoDetailUntil.setText(PromotionRules.until(promotion.getEndsAtUtc()));
        binding.tvPromoDetailWhere.setText(PromotionRules.where(promotion));
        boolean hasImage = !SafeText.isBlank(promotion.getImageUrl());
        binding.cardPromoImage.setVisibility(hasImage ? View.VISIBLE : View.GONE);
        if (hasImage) ImageLoader.getInstance().load(promotion.getImageUrl(), R.drawable.pd_plate_empty, binding.ivPromoImage);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
