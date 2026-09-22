package com.restaurant.viettable.ui.branch;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentBranchMapBinding;
import com.restaurant.viettable.ui.base.BaseFragment;

public class BranchMapFragment extends BaseFragment {

    private FragmentBranchMapBinding binding;

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
        binding.mapCanvas.setVisibility(View.GONE);
        binding.tvMapBranchName.setText("Bản đồ chi nhánh chưa khả dụng");
        binding.tvMapBranchAddress.setText("VietTable chưa có dữ liệu tọa độ chi nhánh. Vui lòng chọn chi nhánh từ danh sách.");
        binding.btnChooseMapBranch.setVisibility(View.GONE);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
