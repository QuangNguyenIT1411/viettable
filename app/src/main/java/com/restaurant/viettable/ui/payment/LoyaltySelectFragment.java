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
    /** Dong per point and redeem step from the server; null = not told, so nothing can be picked (no assumed rate). */
    @Nullable private com.restaurant.viettable.util.LoyaltyDisplayRules.Conversion conversion;

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
        com.restaurant.viettable.data.MobileRepository.getInstance().loadPointsConversion(
                new com.restaurant.viettable.data.MobileRepository.Callback<com.restaurant.viettable.util.LoyaltyDisplayRules.Conversion>() {
                    @Override public void onSuccess(com.restaurant.viettable.util.LoyaltyDisplayRules.Conversion value) {
                        conversion = value;
                        if (binding != null) { showQuickButtons(); showMoneyEquivalent(); updatePointsUI(); }
                    }
                    @Override public void onError(com.restaurant.viettable.network.ApiException error) { }
                });
        com.restaurant.viettable.data.MobileRepository.getInstance().loadLoyalty(
                new com.restaurant.viettable.data.MobileRepository.Callback<com.restaurant.viettable.model.LoyaltyInfo>() {
                    @Override public void onSuccess(com.restaurant.viettable.model.LoyaltyInfo info) {
                        if (binding != null) setBalance(info.getAvailablePoints());
                    }
                    @Override public void onError(com.restaurant.viettable.network.ApiException error) { }
                });

        // One step = the server's redeem step (1.000 points); no half steps.
        binding.btnMinusPoints.setOnClickListener(v -> {
            if (conversion != null && selectedPoints >= conversion.step) {
                selectedPoints -= conversion.step;
                updatePointsUI();
            }
        });

        binding.btnPlusPoints.setOnClickListener(v -> {
            if (conversion != null && selectedPoints + conversion.step <= usablePoints()) {
                selectedPoints += conversion.step;
                updatePointsUI();
            }
        });

        binding.btnQuick1000.setOnClickListener(v -> pickSteps(1));
        binding.btnQuick2000.setOnClickListener(v -> pickSteps(2));
        binding.btnQuickMax.setOnClickListener(v -> {
            selectedPoints = usablePoints();
            updatePointsUI();
        });
        showQuickButtons();

        binding.btnConfirmPoints.setOnClickListener(v -> {
            showToast("Đã chọn dùng " + selectedPoints + " điểm!");
            navigateTo(InvoiceFragment.newInstanceWithVoucher(selectedVoucher, selectedPoints));
        });
    }

    /**
     * Whole steps the balance and the bill allow (0 until the balance, conversion and bill are known). The bill is
     * the server quote's subtotal minus voucher, the same amount the server caps at 10%.
     */
    private long usablePoints() {
        if (conversion == null || !balanceKnown) return 0;
        com.restaurant.viettable.model.Invoice quote = com.restaurant.viettable.data.MobileRepository.getInstance().currentCheckoutInvoice();
        if (quote == null) return 0;
        return conversion.usable(maxPoints, Math.max(0, quote.getSubtotal() - quote.getVoucherDiscount()));
    }

    private void pickSteps(int steps) {
        if (conversion == null) return;
        selectedPoints = Math.min(steps * conversion.step, usablePoints());
        updatePointsUI();
    }

    /** Quick buttons read "1.000 điểm" / "2.000 điểm" from the server's step; hidden until it is known. */
    private void showQuickButtons() {
        boolean known = conversion != null;
        binding.btnQuick1000.setVisibility(known ? View.VISIBLE : View.GONE);
        binding.btnQuick2000.setVisibility(known ? View.VISIBLE : View.GONE);
        if (!known) return;
        binding.btnQuick1000.setText(String.format(Locale.GERMANY, "%,d điểm", conversion.step));
        binding.btnQuick2000.setText(String.format(Locale.GERMANY, "%,d điểm", 2 * conversion.step));
        binding.btnMinusPoints.setText(String.format(Locale.GERMANY, "-%,d", conversion.step));
        binding.btnPlusPoints.setText(String.format(Locale.GERMANY, "+%,d", conversion.step));
    }

    private void showBalanceLoading() {
        balanceKnown = false;
        binding.tvAvailablePoints.setText("Đang tải…");
        binding.tvAvailableMoneyEquivalent.setVisibility(View.GONE);
        updatePointsUI();
    }

    private void setBalance(long points) {
        balanceKnown = true;
        maxPoints = Math.max(0, points);
        selectedPoints = Math.min(selectedPoints, usablePoints());
        binding.tvAvailablePoints.setText(String.format(Locale.GERMANY, "%,d điểm", maxPoints));
        showMoneyEquivalent();
        updatePointsUI();
    }

    private void showMoneyEquivalent() {
        boolean known = conversion != null && balanceKnown;
        binding.tvAvailableMoneyEquivalent.setVisibility(known ? View.VISIBLE : View.GONE);
        if (known) binding.tvAvailableMoneyEquivalent.setText(String.format(Locale.GERMANY, "~%,d đ",
                conversion.discount(usablePoints())));
    }

    private void updatePointsUI() {
        binding.tvSelectedPoints.setText(String.format(Locale.GERMANY, "%,d điểm", selectedPoints));
        binding.tvCalculatedDiscount.setVisibility(conversion != null ? View.VISIBLE : View.GONE);
        if (conversion != null) binding.tvCalculatedDiscount.setText(String.format(Locale.GERMANY,
                "Giảm tương ứng: -%,d đ", conversion.discount(selectedPoints)));
        // Nothing to confirm until the real balance and conversion are known and some points are picked.
        long step = conversion == null ? 0 : conversion.step;
        binding.btnConfirmPoints.setEnabled(conversion != null && balanceKnown && selectedPoints > 0);
        binding.btnPlusPoints.setEnabled(step > 0 && selectedPoints + step <= usablePoints());
        binding.btnMinusPoints.setEnabled(step > 0 && selectedPoints >= step);
        binding.btnQuickMax.setEnabled(usablePoints() > 0);
        // "Tối đa 10% hoá đơn": the server's cap, shown with the most points this bill allows.
        binding.tvPointsCap.setVisibility(conversion != null && balanceKnown ? View.VISIBLE : View.GONE);
        if (conversion != null) binding.tvPointsCap.setText(String.format(Locale.GERMANY,
                "Tối đa %d%% hoá đơn: dùng được %,d điểm", conversion.maxPercent, usablePoints()));
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
