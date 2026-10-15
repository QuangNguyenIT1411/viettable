package com.restaurant.viettable.ui.branch;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentBranchDetailBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.ImageLoader;
import com.restaurant.viettable.util.SafeText;

public class BranchDetailFragment extends BaseFragment {

    private static final String ARG_BRANCH = "arg_branch";
    private FragmentBranchDetailBinding binding;
    private Branch branch;

    public static BranchDetailFragment newInstance(Branch branch) {
        BranchDetailFragment fragment = new BranchDetailFragment();
        Bundle args = new Bundle();
        args.putSerializable(ARG_BRANCH, branch);
        fragment.setArguments(args);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            branch = (Branch) getArguments().getSerializable(ARG_BRANCH);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentBranchDetailBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        if (branch == null) {
            showToast("Không tìm thấy thông tin chi nhánh.");
            navigateBack();
            return;
        }

        if (branch != null) {
            ImageLoader.getInstance().load(branch, binding.ivBranchHero);
            binding.tvBranchName.setText(branch.getName());
            binding.tvAddress.setText(SafeText.clean(branch.getAddress()));
            binding.tvPhone.setText(SafeText.clean(branch.getPhone()));
            binding.tvPhone.setVisibility(SafeText.isBlank(branch.getPhone()) ? View.GONE : View.VISIBLE);
            binding.tvHours.setText("Giờ mở cửa: " + SafeText.clean(branch.getOpeningHours()));
            binding.tvHours.setVisibility(SafeText.isBlank(branch.getOpeningHours()) ? View.GONE : View.VISIBLE);
            binding.tvRatingReviews.setText(branch.getRating() + " • " + branch.getReviewCount() + " đánh giá");
            boolean hasRating = branch.getRating() > 0 && branch.getReviewCount() > 0;
            binding.layoutRating.setVisibility(hasRating ? View.VISIBLE : View.GONE);
            binding.tvDistanceDetail.setText("• Cách bạn " + branch.getDistanceKm() + " km");
            binding.tvDistanceDetail.setVisibility(branch.getDistanceKm() > 0 ? View.VISIBLE : View.GONE);
            binding.tvDescription.setText(SafeText.clean(branch.getDescription()));
            boolean hasDescription = !TextUtils.isEmpty(branch.getDescription());
            binding.tvDescriptionHeading.setVisibility(hasDescription ? View.VISIBLE : View.GONE);
            binding.tvDescription.setVisibility(hasDescription ? View.VISIBLE : View.GONE);

            boolean hasOpeningData = !TextUtils.isEmpty(branch.getOpeningHours());
            binding.tvOpenBadge.setVisibility(hasOpeningData ? View.VISIBLE : View.GONE);
            if (hasOpeningData && branch.isOpen()) {
                binding.tvOpenBadge.setText("Đang mở cửa");
                binding.tvOpenBadge.setBackgroundResource(R.drawable.bg_status_badge);
                binding.tvOpenBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFE8F5E9));
                binding.tvOpenBadge.setTextColor(0xFF2E7D5B);
            } else if (hasOpeningData) {
                binding.tvOpenBadge.setText("Đã đóng cửa");
                binding.tvOpenBadge.setBackgroundResource(R.drawable.bg_status_badge);
                binding.tvOpenBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFFFEBEE));
                binding.tvOpenBadge.setTextColor(0xFFD32F2F);
            }

            binding.chipGroupFacilities.removeAllViews();
            if (branch.getFacilities() != null) {
                for (String facility : branch.getFacilities()) {
                    Chip chip = new Chip(getContext());
                    chip.setText(facility);
                    chip.setCheckable(false);
                    binding.chipGroupFacilities.addView(chip);
                }
            }
            boolean hasFacilities = binding.chipGroupFacilities.getChildCount() > 0;
            binding.tvFacilitiesHeading.setVisibility(hasFacilities ? View.VISIBLE : View.GONE);
            binding.chipGroupFacilities.setVisibility(hasFacilities ? View.VISIBLE : View.GONE);

            binding.btnSelectAndStart.setOnClickListener(v -> {
                appStateManager.setSelectedBranch(branch);
                showToast("Đã chọn chi nhánh " + branch.getName());
                setBottomNavVisible(true);
                switchTab(R.id.nav_home);
            });

            binding.btnViewOnMap.setVisibility(View.GONE);
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
