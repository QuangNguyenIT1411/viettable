package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentInvoiceBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.Locale;

public class InvoiceFragment extends BaseFragment {

    private static final String ARG_VOUCHER = "arg_voucher";
    private static final String ARG_POINTS = "arg_points";

    private FragmentInvoiceBinding binding;
    private Voucher selectedVoucher;
    private long redeemedPoints = 0;

    private long subtotal = 0;
    private long serviceFee = 0;
    private long tax = 0;
    private long voucherDiscount = 0;
    private long loyaltyDiscount = 0;
    private long finalTotal = 0;
    private boolean lockingPayment;
    private boolean applyingAdjustments;

    public static InvoiceFragment newInstance() {
        return new InvoiceFragment();
    }

    public static InvoiceFragment newInstanceWithVoucher(Voucher voucher, long points) {
        InvoiceFragment f = new InvoiceFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_VOUCHER, voucher);
        b.putLong(ARG_POINTS, points);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            selectedVoucher = (Voucher) getArguments().getSerializable(ARG_VOUCHER);
            redeemedPoints = getArguments().getLong(ARG_POINTS, 0);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentInvoiceBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        DiningSession session = appStateManager.getActiveDiningSession();
        Invoice serverQuote = MobileRepository.getInstance().currentCheckoutInvoice();
        if (session == null || serverQuote == null) {
            binding.btnProceedToPaymentType.setEnabled(false);
            binding.btnSplitBill.setEnabled(false);
            showToast("Không tìm thấy báo giá thanh toán từ máy chủ.");
            return;
        }

        binding.tvInvoiceBranch.setText(session.getBranchName());
        binding.tvInvoiceSessionInfo.setText(session.getTableId() + " • Phiên #" + session.getId() + " • " + session.getStartTime());

        subtotal = serverQuote.getSubtotal();
        serviceFee = serverQuote.getServiceFee();
        tax = serverQuote.getTax();

        binding.layoutInvoiceItems.removeAllViews();
        for (Order o : session.getOrders()) {
            for (OrderItem oi : o.getItems()) {
                    LinearLayout row = new LinearLayout(requireContext());
                    row.setOrientation(LinearLayout.HORIZONTAL);
                    row.setPadding(0, 4, 0, 4);

                    TextView tvName = new TextView(requireContext());
                    tvName.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.0f));
                    tvName.setText(oi.getQuantity() + "x " + oi.getFoodName());
                    tvName.setTextColor(androidx.core.content.ContextCompat.getColor(requireContext(), R.color.colorTextPrimary));
                    tvName.setTextSize(13);

                    TextView tvPrice = new TextView(requireContext());
                    tvPrice.setText(oi.getFormattedTotalPrice());
                    tvPrice.setTextColor(androidx.core.content.ContextCompat.getColor(requireContext(), R.color.colorTextSecondary));
                    tvPrice.setTextSize(13);

                    row.addView(tvName);
                    row.addView(tvPrice);
                binding.layoutInvoiceItems.addView(row);
            }
        }

        recalculateInvoice();
        applyServerSelections();

        binding.btnSelectVoucher.setOnClickListener(v -> {
            navigateTo(VoucherSelectFragment.newInstance(redeemedPoints));
        });

        binding.btnSelectLoyalty.setOnClickListener(v -> {
            navigateTo(LoyaltySelectFragment.newInstance(selectedVoucher));
        });

        binding.btnProceedToPaymentType.setOnClickListener(v -> {
            lockPaymentAndContinue(false);
        });
        binding.btnSplitBill.setOnClickListener(v ->
                lockPaymentAndContinue(true));
    }

    private void lockPaymentAndContinue(boolean split) {
        if (lockingPayment || applyingAdjustments) return;
        lockingPayment = true;
        binding.btnProceedToPaymentType.setEnabled(false); binding.btnSplitBill.setEnabled(false);
        MobileRepository.getInstance().requestCheckoutPayment(new MobileRepository.Callback<Invoice>() {
            @Override public void onSuccess(Invoice value) {
                lockingPayment = false;
                if (binding == null) return;
                if (split) navigateTo(SplitBillFragment.newInstance(value));
                else navigateTo(PaymentTypeFragment.newInstance(value));
            }
            @Override public void onError(ApiException error) {
                lockingPayment = false;
                if (binding != null) {
                    binding.btnProceedToPaymentType.setEnabled(true); binding.btnSplitBill.setEnabled(true);
                    showToast(apiError(error));
                }
            }
        });
    }

    private void applyServerSelections() {
        applyingAdjustments = selectedVoucher != null || redeemedPoints > 0;
        if (applyingAdjustments) setCheckoutActionsEnabled(false);
        if (selectedVoucher != null) {
            MobileRepository.getInstance().applyVoucher(selectedVoucher, new MobileRepository.Callback<Invoice>() {
                @Override public void onSuccess(Invoice value) {
                    if (binding == null) return;
                    if (redeemedPoints > 0) applyPoints(); else { recalculateInvoice(); finishAdjustments(); }
                }
                @Override public void onError(ApiException error) { if (binding != null) { finishAdjustments(); showToast(apiError(error)); } }
            });
        } else if (redeemedPoints > 0) applyPoints();
    }

    private void applyPoints() {
        MobileRepository.getInstance().applyLoyalty((int) redeemedPoints, new MobileRepository.Callback<Invoice>() {
            @Override public void onSuccess(Invoice value) { if (binding != null) { recalculateInvoice(); finishAdjustments(); } }
            @Override public void onError(ApiException error) { if (binding != null) { finishAdjustments(); showToast(apiError(error)); } }
        });
    }

    private void finishAdjustments() { applyingAdjustments = false; setCheckoutActionsEnabled(true); }

    private void setCheckoutActionsEnabled(boolean enabled) {
        if (binding == null) return;
        binding.btnProceedToPaymentType.setEnabled(enabled);
        binding.btnSplitBill.setEnabled(enabled);
    }

    private void recalculateInvoice() {
        Invoice quote = MobileRepository.getInstance().currentCheckoutInvoice();
        if (quote != null) {
            voucherDiscount = quote.getVoucherDiscount(); loyaltyDiscount = quote.getLoyaltyDiscount();
            finalTotal = quote.getFinalTotal();
            binding.layoutVoucherDiscount.setVisibility(voucherDiscount > 0 ? View.VISIBLE : View.GONE);
            binding.layoutLoyaltyDiscount.setVisibility(loyaltyDiscount > 0 ? View.VISIBLE : View.GONE);
            binding.tvVoucherDiscountAmount.setText(String.format(Locale.GERMANY, "-%,dđ", voucherDiscount));
            binding.tvLoyaltyDiscountAmount.setText(String.format(Locale.GERMANY, "-%,dđ", loyaltyDiscount));
        } else {
            binding.btnProceedToPaymentType.setEnabled(false);
            binding.btnSplitBill.setEnabled(false);
            return;
        }

        binding.tvInvoiceSubtotal.setText(String.format(Locale.GERMANY, "%,dđ", subtotal));
        binding.tvInvoiceServiceFee.setText(String.format(Locale.GERMANY, "%,dđ", serviceFee));
        binding.tvInvoiceVat.setText(String.format(Locale.GERMANY, "%,dđ", tax));
        binding.tvInvoiceGrandTotal.setText(String.format(Locale.GERMANY, "%,dđ", finalTotal));
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
