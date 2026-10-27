package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.adapter.VoucherSelectAdapter;
import com.restaurant.viettable.databinding.FragmentVoucherSelectBinding;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.List;

public class VoucherSelectFragment extends BaseFragment {

    private static final String ARG_POINTS = "arg_points";
    private FragmentVoucherSelectBinding binding;
    private VoucherSelectAdapter adapter;
    private Voucher chosenVoucher;
    private long redeemedPoints = 0;

    public static VoucherSelectFragment newInstance(long points) {
        VoucherSelectFragment f = new VoucherSelectFragment();
        Bundle b = new Bundle();
        b.putLong(ARG_POINTS, points);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            redeemedPoints = getArguments().getLong(ARG_POINTS, 0);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentVoucherSelectBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        List<Voucher> availableVouchers = appStateManager.getVouchers();
        adapter = new VoucherSelectAdapter(availableVouchers, voucher -> {
            chosenVoucher = voucher;
        });

        binding.rvVouchers.setLayoutManager(new LinearLayoutManager(requireContext()));
        binding.rvVouchers.setAdapter(adapter);

        binding.btnApplyCode.setOnClickListener(v -> {
            String code = binding.etVoucherCode.getText() != null ? binding.etVoucherCode.getText().toString().trim() : "";
            if (TextUtils.isEmpty(code)) {
                binding.tilVoucherCode.setError("Vui lòng nhập mã");
                return;
            }
            binding.tilVoucherCode.setError(null);
            chosenVoucher = null;
            for (Voucher voucher : availableVouchers) {
                if (voucher.getCode().equalsIgnoreCase(code)) { chosenVoucher = voucher; break; }
            }
            if (chosenVoucher == null) {
                binding.tilVoucherCode.setError("Mã không tồn tại hoặc không thuộc tài khoản này");
                return;
            }
            showToast("Đã chọn mã " + chosenVoucher.getCode());
        });

        binding.btnConfirmVoucher.setOnClickListener(v -> {
            if (chosenVoucher == null && adapter.getSelectedVoucher() != null) {
                chosenVoucher = adapter.getSelectedVoucher();
            }
            if (chosenVoucher != null) {
                showToast("Đã chọn: " + chosenVoucher.getTitle());
            }
            navigateTo(InvoiceFragment.newInstanceWithVoucher(chosenVoucher, redeemedPoints));
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
