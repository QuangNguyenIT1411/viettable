package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentLoyaltySelectBinding;
import com.restaurant.viettable.model.User;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.Locale;

public class LoyaltySelectFragment extends BaseFragment {

    private static final String ARG_VOUCHER = "arg_voucher";
    private FragmentLoyaltySelectBinding binding;
    private Voucher selectedVoucher;
    private long selectedPoints = 2000;
    private long maxPoints = 15000;

    public static LoyaltySelectFragment newInstance(Voucher voucher) {
        LoyaltySelectFragment f = new LoyaltySelectFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_VOUCHER, voucher);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            selectedVoucher = (Voucher) getArguments().getSerializable(ARG_VOUCHER);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentLoyaltySelectBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        User user = appStateManager.getCurrentUser();
        if (user != null) {
            maxPoints = user.getLoyaltyPoints();
        }

        binding.tvAvailablePoints.setText(String.format(Locale.GERMANY, "%,d điểm", maxPoints));
        binding.tvAvailableMoneyEquivalent.setText(String.format(Locale.GERMANY, "~%,d đ", maxPoints * 10));

        updatePointsUI();

        binding.btnMinusPoints.setOnClickListener(v -> {
            if (selectedPoints >= 500) {
                selectedPoints -= 500;
                updatePointsUI();
            }
        });

        binding.btnPlusPoints.setOnClickListener(v -> {
            if (selectedPoints + 500 <= maxPoints) {
                selectedPoints += 500;
                updatePointsUI();
            }
        });

        binding.btnQuick1000.setOnClickListener(v -> {
            selectedPoints = Math.min(1000, maxPoints);
            updatePointsUI();
        });

        binding.btnQuick2000.setOnClickListener(v -> {
            selectedPoints = Math.min(2000, maxPoints);
            updatePointsUI();
        });

        binding.btnQuickMax.setOnClickListener(v -> {
            selectedPoints = maxPoints;
            updatePointsUI();
        });

        binding.btnConfirmPoints.setOnClickListener(v -> {
            showToast("Đã chọn dùng " + selectedPoints + " điểm!");
            navigateTo(InvoiceFragment.newInstanceWithVoucher(selectedVoucher, selectedPoints));
        });
    }

    private void updatePointsUI() {
        binding.tvSelectedPoints.setText(String.format(Locale.GERMANY, "%,d điểm", selectedPoints));
        binding.tvCalculatedDiscount.setText(String.format(Locale.GERMANY, "Giảm tương ứng: -%,d đ", selectedPoints * 10));
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
