package com.restaurant.viettable.ui.session;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentCallStaffBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.ServiceRequest;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

public class CallStaffFragment extends BaseFragment {

    private FragmentCallStaffBinding binding;

    public static CallStaffFragment newInstance() {
        return new CallStaffFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentCallStaffBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        DiningSession session = appStateManager.getActiveDiningSession();
        if (session != null) {
            binding.tvStaffTableInfo.setText("Yêu cầu từ: " + session.getTableLabel() + " • " + session.getBranchName());
        }

        binding.btnViewServiceHistory.setOnClickListener(v -> {
            navigateTo(ServiceStatusFragment.newInstance());
        });

        binding.btnSubmitCallStaff.setOnClickListener(v -> {
            int checkedId = binding.chipGroupService.getCheckedChipId();
            String serviceType = "Hỗ trợ chung";
            if (checkedId != View.NO_ID) {
                Chip chip = binding.chipGroupService.findViewById(checkedId);
                if (chip != null) {
                    serviceType = chip.getText().toString();
                }
            }

            String note = binding.etServiceNote.getText() != null ? binding.etServiceNote.getText().toString().trim() : "";
            if (TextUtils.isEmpty(note)) {
                note = "Khách yêu cầu tại bàn";
            }

            binding.btnSubmitCallStaff.setEnabled(false);
            MobileRepository.getInstance().createServiceRequest(serviceType, note, new MobileRepository.Callback<ServiceRequest>() {
                @Override public void onSuccess(ServiceRequest value) { showToast("Đã gửi yêu cầu! Nhân viên sẽ đến bàn ngay."); navigateTo(ServiceStatusFragment.newInstance()); }
                @Override public void onError(ApiException error) { if (binding != null) { binding.btnSubmitCallStaff.setEnabled(true); showToast(apiError(error)); } }
            });
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
