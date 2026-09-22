package com.restaurant.viettable.ui.branch;

import android.os.Bundle;
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

        if (branch == null && !appStateManager.getBranches().isEmpty()) {
            branch = appStateManager.getBranches().get(0);
        }

        binding.btnBack.setOnClickListener(v -> navigateBack());

        if (branch != null) {
            ImageLoader.getInstance().load(branch, binding.ivBranchHero);
            binding.tvBranchName.setText(branch.getName());
            binding.tvAddress.setText(branch.getAddress());
            binding.tvPhone.setText(branch.getPhone());
            binding.tvHours.setText("Giờ mở cửa: " + branch.getOpeningHours());
            binding.tvRatingReviews.setText(branch.getRating() + " • " + branch.getReviewCount() + " đánh giá");
            binding.tvDistanceDetail.setText("• Cách bạn " + branch.getDistanceKm() + " km");
            binding.tvDescription.setText(branch.getDescription());

            if (branch.isOpen()) {
                binding.tvOpenBadge.setText("Đang mở cửa");
                binding.tvOpenBadge.setBackgroundResource(R.drawable.bg_status_badge);
                binding.tvOpenBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFE8F5E9));
                binding.tvOpenBadge.setTextColor(0xFF2E7D5B);
            } else {
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

            binding.btnSelectAndStart.setOnClickListener(v -> {
                appStateManager.setSelectedBranch(branch);
                showToast("Đã chọn chi nhánh " + branch.getName());
                setBottomNavVisible(true);
                switchTab(R.id.nav_home);
            });

            binding.btnViewOnMap.setOnClickListener(v -> navigateTo(new BranchMapFragment()));
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
