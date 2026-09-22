package com.restaurant.viettable.ui.branch;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentBranchMapBinding;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.ui.base.BaseFragment;

public class BranchMapFragment extends BaseFragment {

    private FragmentBranchMapBinding binding;
    private Branch selectedNearest;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentBranchMapBinding.inflate(inflater, container, false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        if (!appStateManager.getBranches().isEmpty()) {
            selectedNearest = appStateManager.getBranches().get(0);
            updateSelectedBranchView(selectedNearest);
        }

        binding.markerHoanKiem.setOnClickListener(v -> {
            selectedNearest = appStateManager.getBranches().get(0);
            updateSelectedBranchView(selectedNearest);
        });

        binding.markerBaDinh.setOnClickListener(v -> {
            if (appStateManager.getBranches().size() > 1) {
                selectedNearest = appStateManager.getBranches().get(1);
                updateSelectedBranchView(selectedNearest);
            }
        });

        binding.btnChooseMapBranch.setOnClickListener(v -> {
            if (selectedNearest != null) {
                appStateManager.setSelectedBranch(selectedNearest);
                showToast("Đã chọn: " + selectedNearest.getName());
                setBottomNavVisible(true);
                switchTab(R.id.nav_home);
            }
        });
    }

    private void updateSelectedBranchView(Branch branch) {
        binding.tvMapBranchName.setText(branch.getName());
        binding.tvMapBranchAddress.setText(branch.getAddress() + " • " + branch.getDistanceKm() + " km");
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
