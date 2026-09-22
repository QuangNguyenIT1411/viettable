package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentResNoTableBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.ui.base.BaseFragment;

public class ResNoTableFragment extends BaseFragment {

    private FragmentResNoTableBinding binding;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResNoTableBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.btnSelectSuggestedTime.setOnClickListener(v -> {
            showToast("Đã chọn khung giờ 20:00 hôm nay");
            navigateBack();
        });

        binding.btnSelectSuggestedBranch.setOnClickListener(v -> {
            if (appStateManager.getBranches().size() > 1) {
                Branch badinh = appStateManager.getBranches().get(1);
                appStateManager.setSelectedBranch(badinh);
                showToast("Đã chuyển sang chi nhánh " + badinh.getName());
                navigateTo(new ResDateFragment());
            }
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
