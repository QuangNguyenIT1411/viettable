package com.restaurant.viettable.ui.profile;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.google.android.material.tabs.TabLayout;
import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.VoucherCardAdapter;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentCustomerVoucherBinding;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.List;

public class CustomerVoucherFragment extends BaseFragment {

    private FragmentCustomerVoucherBinding binding;
    private VoucherCardAdapter adapter;
    private final List<Voucher> displayedVouchers = new ArrayList<>();

    public static CustomerVoucherFragment newInstance() {
        return new CustomerVoucherFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentCustomerVoucherBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        binding.tabLayoutMyVouchers.addOnTabSelectedListener(new TabLayout.OnTabSelectedListener() {
            @Override
            public void onTabSelected(TabLayout.Tab tab) {
                filterVouchers(tab.getPosition());
            }

            @Override
            public void onTabUnselected(TabLayout.Tab tab) {}

            @Override
            public void onTabReselected(TabLayout.Tab tab) {}
        });

        adapter = new VoucherCardAdapter(displayedVouchers, voucher -> {
            showToast("Đã sao chép mã " + voucher.getCode() + " và áp dụng cho lần đặt bàn tiếp theo!");
            switchTab(R.id.nav_reservation);
        });

        binding.rvCustomerVouchers.setLayoutManager(new LinearLayoutManager(requireContext()));
        binding.rvCustomerVouchers.setAdapter(adapter);

        filterVouchers(0);
        MobileRepository.getInstance().loadVouchers(new MobileRepository.Callback<List<Voucher>>() {
            @Override public void onSuccess(List<Voucher> value) { if (binding != null) filterVouchers(binding.tabLayoutMyVouchers.getSelectedTabPosition()); }
            @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
        });
    }

    private void filterVouchers(int tabIndex) {
        displayedVouchers.clear();
        for (Voucher v : appStateManager.getVouchers()) {
            if (tabIndex == 0 && !v.isUsed()) {
                displayedVouchers.add(v);
            } else if (tabIndex == 1 && v.isUsed()) {
                displayedVouchers.add(v);
            }
        }
        adapter.notifyDataSetChanged();
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
