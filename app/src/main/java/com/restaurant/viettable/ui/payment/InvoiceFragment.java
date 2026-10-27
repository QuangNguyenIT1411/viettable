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
import com.restaurant.viettable.util.DisplayFormatters;

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

        binding.btnBack.setOnClickListener(v -> navigateBack());

        DiningSession session = appStateManager.getActiveDiningSession();
        Invoice serverQuote = MobileRepository.getInstance().currentCheckoutInvoice();
        if (session == null || serverQuote == null) {
            binding.btnProceedToPaymentType.setEnabled(false);
            binding.btnSplitBill.setEnabled(false);
            showToast("Không tìm thấy báo giá thanh toán từ máy chủ.");
            return;
        }

        binding.tvInvoiceBranch.setText(session.getBranchName());
        binding.tvInvoiceSessionInfo.setText(session.getTableLabel() + " • Phiên #" + session.getId() + " • " + DisplayFormatters.dateTime(session.getStartTime()));

        subtotal = serverQuote.getSubtotal();
        serviceFee = serverQuote.getServiceFee();
        tax = serverQuote.getTax();

        binding.layoutInvoiceItems.removeAllViews();
        for (Order o : session.getOrders()) {
            for (OrderItem oi : o.getItems()) {
                    LinearLayout row = new LinearLayout(requireContext());
                    row.setOrientation(LinearLayout.HORIZONTAL);
                    int rowPadding = Math.round(6 * getResources().getDisplayMetrics().density);
                    row.setPadding(0, rowPadding, 0, rowPadding);

                    TextView tvName = new TextView(requireContext());
                    tvName.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.0f));
                    tvName.setText(oi.getQuantity() + "x " + oi.getFoodName());
                    androidx.core.widget.TextViewCompat.setTextAppearance(tvName, R.style.TextAppearance_VietTable_Body);
                    tvName.setTextColor(androidx.core.content.ContextCompat.getColor(requireContext(), R.color.pd_cream));

                    TextView tvPrice = new TextView(requireContext());
                    tvPrice.setText(oi.getFormattedTotalPrice());
                    androidx.core.widget.TextViewCompat.setTextAppearance(tvPrice, R.style.TextAppearance_VietTable_Body);
                    tvPrice.setTextColor(androidx.core.content.ContextCompat.getColor(requireContext(), R.color.pd_muted));
                    tvPrice.setPaddingRelative(rowPadding * 2, 0, 0, 0);

                    row.addView(tvName);
                    row.addView(tvPrice);
                binding.layoutInvoiceItems.addView(row);
            }
        }

        recalculateInvoice();
        applyServerSelections();
        refreshQuoteFromServer();

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

    /**
     * Coming back here (Back from the payment or VietQR screen) must not trust what this screen drew earlier: ask the
     * server for the session's waiting payment, which carries its quote with the real status and total (owner, 28/10).
     * With no waiting payment the quote kept from the last server answer stays.
     */
    private void refreshQuoteFromServer() {
        MobileRepository.getInstance().loadActivePayment(new MobileRepository.Callback<com.restaurant.viettable.model.PaymentInfo>() {
            @Override public void onSuccess(com.restaurant.viettable.model.PaymentInfo payment) {
                if (binding == null) return;
                recalculateInvoice();
            }
            @Override public void onError(ApiException error) { }
        });
    }

    /** Voucher and points are frozen once the payment was requested: grey out the two buttons and say why. */
    private void applyLockState() {
        boolean locked = com.restaurant.viettable.util.LoyaltyApplyRules.isQuoteLocked(
                MobileRepository.getInstance().currentCheckoutQuoteStatus());
        binding.btnSelectVoucher.setEnabled(!locked);
        binding.btnSelectLoyalty.setEnabled(!locked);
        binding.tvAdjustLocked.setVisibility(locked ? View.VISIBLE : View.GONE);
        if (locked) binding.tvAdjustLocked.setText(com.restaurant.viettable.util.LoyaltyApplyRules.LOCKED_EXPLANATION);
    }

    private void setCheckoutActionsEnabled(boolean enabled) {
        if (binding == null) return;
        binding.btnProceedToPaymentType.setEnabled(enabled);
        binding.btnSplitBill.setEnabled(enabled);
    }

    private void recalculateInvoice() {
        Invoice quote = MobileRepository.getInstance().currentCheckoutInvoice();
        if (quote != null) {
            subtotal = quote.getSubtotal();
            voucherDiscount = quote.getVoucherDiscount(); loyaltyDiscount = quote.getLoyaltyDiscount();
            applyLockState();
            finalTotal = quote.getFinalTotal();
            binding.layoutVoucherDiscount.setVisibility(voucherDiscount > 0 ? View.VISIBLE : View.GONE);
            binding.layoutLoyaltyDiscount.setVisibility(loyaltyDiscount > 0 ? View.VISIBLE : View.GONE);
            binding.tvVoucherDiscountAmount.setText(String.format(Locale.GERMANY, "-%,dđ", voucherDiscount));
            binding.tvLoyaltyDiscountAmount.setText(String.format(Locale.GERMANY, "-%,dđ", loyaltyDiscount));
            binding.layoutPromotionDiscount.setVisibility(quote.getPromotionDiscount() > 0 ? View.VISIBLE : View.GONE);
            binding.tvPromotionLabel.setText(quote.getPromotionName() == null ? "Khuyến mãi" : "Khuyến mãi: " + quote.getPromotionName());
            binding.tvPromotionDiscountAmount.setText(String.format(Locale.GERMANY, "-%,dđ", quote.getPromotionDiscount()));
            boolean hasNote = quote.getDiscountNote() != null && !quote.getDiscountNote().trim().isEmpty();
            binding.tvDiscountNote.setVisibility(hasNote ? View.VISIBLE : View.GONE);
            binding.tvDiscountNote.setText(hasNote ? quote.getDiscountNote() : "");
            binding.layoutDepositApplied.setVisibility(quote.getDepositApplied() > 0 ? View.VISIBLE : View.GONE);
            binding.tvDepositAppliedAmount.setText(String.format(Locale.GERMANY, "-%,dđ", quote.getDepositApplied()));
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
