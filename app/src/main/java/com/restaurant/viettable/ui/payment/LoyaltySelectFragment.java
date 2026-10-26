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
    // No made-up defaults: nothing selected, and the balance is the server's (owner, 26/10).
    private long selectedPoints = 0;
    private long maxPoints = 0;
    private boolean balanceKnown;

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
        if (user != null) setBalance(user.getLoyaltyPoints());
        else showBalanceLoading();
        com.restaurant.viettable.data.MobileRepository.getInstance().loadLoyalty(
                new com.restaurant.viettable.data.MobileRepository.Callback<com.restaurant.viettable.model.LoyaltyInfo>() {
                    @Override public void onSuccess(com.restaurant.viettable.model.LoyaltyInfo info) {
                        if (binding != null) setBalance(info.getAvailablePoints());
                    }
                    @Override public void onError(com.restaurant.viettable.network.ApiException error) { }
                });

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

    private void showBalanceLoading() {
        balanceKnown = false;
        binding.tvAvailablePoints.setText("Đang tải…");
        binding.tvAvailableMoneyEquivalent.setText("");
        updatePointsUI();
    }

    private void setBalance(long points) {
        balanceKnown = true;
        maxPoints = Math.max(0, points);
        selectedPoints = Math.min(selectedPoints, maxPoints);
        binding.tvAvailablePoints.setText(String.format(Locale.GERMANY, "%,d điểm", maxPoints));
        binding.tvAvailableMoneyEquivalent.setText(String.format(Locale.GERMANY, "~%,d đ", maxPoints * 10));
        updatePointsUI();
    }

    private void updatePointsUI() {
        binding.tvSelectedPoints.setText(String.format(Locale.GERMANY, "%,d điểm", selectedPoints));
        binding.tvCalculatedDiscount.setText(String.format(Locale.GERMANY, "Giảm tương ứng: -%,d đ", selectedPoints * 10));
        // Nothing to confirm until the real balance is known and some points are picked.
        binding.btnConfirmPoints.setEnabled(balanceKnown && selectedPoints > 0);
        binding.btnPlusPoints.setEnabled(balanceKnown && selectedPoints + 500 <= maxPoints);
        binding.btnMinusPoints.setEnabled(selectedPoints >= 500);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
