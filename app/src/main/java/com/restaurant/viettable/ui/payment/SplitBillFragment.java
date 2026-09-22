package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;
import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import com.google.android.material.button.MaterialButton;
import com.google.android.material.card.MaterialCardView;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.data.SplitBillAmounts;
import com.restaurant.viettable.databinding.FragmentSplitBillBinding;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import java.util.Locale;

public class SplitBillFragment extends BaseFragment {
    private static final String ARG_INVOICE = "arg_invoice";
    private static final String STATE_PERSON_COUNT = "person_count";
    private FragmentSplitBillBinding binding;
    private Invoice invoice;
    private SplitBill splitBill;
    private int personCount = 3;
    private boolean submitting;

    public static SplitBillFragment newInstance(Invoice invoice) {
        SplitBillFragment fragment = new SplitBillFragment();
        Bundle args = new Bundle(); args.putSerializable(ARG_INVOICE, invoice); fragment.setArguments(args);
        return fragment;
    }

    @Override public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) invoice = (Invoice) getArguments().getSerializable(ARG_INVOICE);
        if (savedInstanceState != null) personCount = savedInstanceState.getInt(STATE_PERSON_COUNT, 3);
    }

    @Nullable @Override public View onCreateView(@NonNull LayoutInflater inflater,
                                                  @Nullable ViewGroup container,
                                                  @Nullable Bundle savedInstanceState) {
        binding = FragmentSplitBillBinding.inflate(inflater, container, false);
        setBottomNavVisible(false); return binding.getRoot();
    }

    @Override public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());
        if (invoice == null) invoice = MobileRepository.getInstance().currentCheckoutInvoice();
        binding.btnModeCustom.setOnClickListener(v -> showToast("Backend B6 hiện chỉ hỗ trợ chia theo số tiền."));
        binding.btnMinusSplit.setOnClickListener(v -> { if (!submitting && personCount > 2) { personCount--; updatePreview(); } });
        binding.btnPlusSplit.setOnClickListener(v -> { if (!submitting && personCount < 20) { personCount++; updatePreview(); } });
        binding.btnDoneSplit.setOnClickListener(v -> submitSplit());
        updatePreview(); loadExistingSplit();
    }

    private void loadExistingSplit() {
        setLoading(true);
        MobileRepository.getInstance().loadSessionSplitBill(new MobileRepository.Callback<SplitBill>() {
            @Override public void onSuccess(SplitBill value) {
                if (binding == null) return; setLoading(false); renderSplit(value);
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return; setLoading(false);
                if (error.getStatusCode() == 404 && invoice != null) showEditor();
                else {
                    binding.tvSplitStatus.setVisibility(View.VISIBLE);
                    binding.tvSplitStatus.setText(apiError(error)); binding.btnDoneSplit.setEnabled(false);
                }
            }
        });
    }

    private void submitSplit() {
        if (invoice == null || submitting) return;
        long[] amounts;
        try { amounts = SplitBillAmounts.equalParts(invoice.getFinalTotal(), personCount); }
        catch (IllegalArgumentException error) { showToast("Số phần chia không hợp lệ."); return; }
        submitting = true; setLoading(true); binding.btnDoneSplit.setEnabled(false);
        MobileRepository.getInstance().createSplitBill(amounts, new MobileRepository.Callback<SplitBill>() {
            @Override public void onSuccess(SplitBill value) {
                submitting = false; if (binding == null) return; setLoading(false); renderSplit(value);
            }
            @Override public void onError(ApiException error) {
                submitting = false; if (binding == null) return; setLoading(false); binding.btnDoneSplit.setEnabled(true);
                if ("split_bill_exists".equals(error.getCode())) loadExistingSplit(); else showToast(apiError(error));
            }
        });
    }

    private void showEditor() {
        binding.cardEqualController.setVisibility(View.VISIBLE); binding.toggleSplitMode.setVisibility(View.VISIBLE);
        binding.btnDoneSplit.setVisibility(View.VISIBLE); binding.btnDoneSplit.setEnabled(invoice != null);
        binding.layoutSplitParts.removeAllViews(); binding.tvSplitStatus.setVisibility(View.VISIBLE);
        binding.tvSplitStatus.setText("Chia đều theo số người. Phần dư VND được cộng lần lượt vào các phần đầu tiên.");
        updatePreview();
    }

    private void renderSplit(SplitBill value) {
        splitBill = value; binding.cardEqualController.setVisibility(View.GONE);
        binding.toggleSplitMode.setVisibility(View.GONE); binding.btnDoneSplit.setVisibility(View.GONE);
        binding.tvSplitBillTotal.setText(formatMoney(value.getTotalAmount())); binding.tvSplitStatus.setVisibility(View.VISIBLE);
        binding.tvSplitStatus.setText(value.isPaid() ? "Tất cả các phần đã thanh toán. Hóa đơn đang được phát hành."
                : "Cấu hình đã được máy chủ xác nhận. Chọn phần chưa thanh toán để tiếp tục.");
        binding.layoutSplitParts.removeAllViews();
        for (SplitBill.Part part : value.getParts()) addPartView(part);
    }

    private void addPartView(SplitBill.Part part) {
        MaterialCardView card = new MaterialCardView(requireContext());
        LinearLayout.LayoutParams cardParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        cardParams.bottomMargin = dp(10); card.setLayoutParams(cardParams); card.setRadius(dp(14));
        card.setStrokeWidth(dp(1)); card.setStrokeColor(requireContext().getColor(R.color.colorDivider));
        LinearLayout row = new LinearLayout(requireContext()); row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(android.view.Gravity.CENTER_VERTICAL); row.setPadding(dp(16), dp(14), dp(16), dp(14));
        TextView summary = new TextView(requireContext()); summary.setLayoutParams(new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        summary.setText(part.getLabel() + "\n" + part.getFormattedAmount() + " • " + displayStatus(part));
        summary.setTextColor(requireContext().getColor(R.color.colorTextPrimary)); summary.setTextSize(15); row.addView(summary);
        MaterialButton action = new MaterialButton(requireContext());
        boolean retryable = part.canRetryPayment();
        action.setText(part.canStartPayment() ? "Thanh toán" : (retryable ? "Thử lại" : (part.isPaid() ? "Đã trả" : "Đang xử lý")));
        action.setEnabled(part.canStartPayment() || retryable);
        action.setOnClickListener(v -> {
            if (retryable) retryPartPayment(part);
            else navigateTo(PaymentTypeFragment.newInstanceForSplit(invoice, splitBill, part));
        });
        row.addView(action); card.addView(row); binding.layoutSplitParts.addView(card);
    }

    private void retryPartPayment(SplitBill.Part part) {
        if (part.getPaymentId() == null || submitting) return;
        submitting = true; setLoading(true);
        MobileRepository.getInstance().retryPayment(part.getPaymentId(), new MobileRepository.Callback<com.restaurant.viettable.model.PaymentInfo>() {
            @Override public void onSuccess(com.restaurant.viettable.model.PaymentInfo value) {
                submitting = false;
                if (binding != null) navigateTo(PaymentProcessingFragment.newInstanceForSplit(invoice, value.getMethod(), value, splitBill.getSplitBillId()));
            }
            @Override public void onError(ApiException error) {
                submitting = false;
                if (binding != null) { setLoading(false); showToast(apiError(error)); loadExistingSplit(); }
            }
        });
    }

    private void updatePreview() {
        if (binding == null) return; binding.tvSplitPersonCount.setText(String.valueOf(personCount));
        if (invoice == null) {
            binding.tvSplitBillTotal.setText("--"); binding.tvPerPersonShare.setText("--");
            binding.tvQrShareInstruction.setText("Không tìm thấy báo giá máy chủ."); return;
        }
        long[] amounts = SplitBillAmounts.equalParts(invoice.getFinalTotal(), personCount);
        binding.tvSplitBillTotal.setText(invoice.getFormattedFinalTotal());
        binding.tvPerPersonShare.setText(formatMoney(amounts[0]) + " / phần đầu");
        String remainder = amounts[0] == amounts[amounts.length - 1] ? "Các phần bằng nhau."
                : "Phần đầu lớn hơn phần cuối " + (amounts[0] - amounts[amounts.length - 1]) + "đ do số dư.";
        binding.tvQrShareInstruction.setText(remainder + " Máy chủ sẽ xác nhận toàn bộ số tiền.");
    }

    private void setLoading(boolean loading) { binding.progressSplitBill.setVisibility(loading ? View.VISIBLE : View.GONE); }
    private int dp(int value) { return Math.round(value * getResources().getDisplayMetrics().density); }
    private static String formatMoney(long amount) { return String.format(Locale.GERMANY, "%,dđ", amount); }
    private static String displayStatus(SplitBill.Part part) {
        if (part.canRetryPayment()) return "Thanh toán chưa thành công";
        if ("PAID".equalsIgnoreCase(part.getStatus())) return "Đã thanh toán";
        if ("PENDING".equalsIgnoreCase(part.getStatus())) return "Đang chờ thanh toán";
        return "Chưa thanh toán";
    }

    @Override public void onSaveInstanceState(@NonNull Bundle outState) {
        super.onSaveInstanceState(outState); outState.putInt(STATE_PERSON_COUNT, personCount);
    }
    @Override public void onDestroyView() { super.onDestroyView(); binding = null; }
}
