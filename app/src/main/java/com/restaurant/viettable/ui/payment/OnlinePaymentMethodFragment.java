package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentOnlinePaymentBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.model.SplitPaymentResult;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

public class OnlinePaymentMethodFragment extends BaseFragment {

    private static final String ARG_INVOICE = "arg_invoice";
    private static final String ARG_SPLIT = "arg_split";
    private static final String ARG_PART = "arg_part";
    private FragmentOnlinePaymentBinding binding;
    private Invoice invoice;
    private SplitBill splitBill;
    private SplitBill.Part splitPart;
    private String selectedMethod = "VNPAY Sandbox";

    public static OnlinePaymentMethodFragment newInstance(Invoice invoice) {
        OnlinePaymentMethodFragment f = new OnlinePaymentMethodFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_INVOICE, invoice);
        f.setArguments(b);
        return f;
    }

    public static OnlinePaymentMethodFragment newInstanceForSplit(Invoice invoice, SplitBill splitBill, SplitBill.Part part) {
        OnlinePaymentMethodFragment fragment = newInstance(invoice);
        fragment.requireArguments().putSerializable(ARG_SPLIT, splitBill);
        fragment.requireArguments().putSerializable(ARG_PART, part);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            invoice = (Invoice) getArguments().getSerializable(ARG_INVOICE);
            splitBill = (SplitBill) getArguments().getSerializable(ARG_SPLIT);
            splitPart = (SplitBill.Part) getArguments().getSerializable(ARG_PART);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentOnlinePaymentBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        if (invoice != null) {
            binding.btnSubmitOnlinePayment.setText("Thanh toán " + (splitPart == null ? invoice.getFormattedFinalTotal() : splitPart.getFormattedAmount()));
        } else {
            binding.btnSubmitOnlinePayment.setEnabled(false);
            showToast("Không tìm thấy báo giá thanh toán từ máy chủ.");
        }

        binding.cardVietQr.setOnClickListener(v -> select("VNPAY Sandbox"));
        disableUnavailableMethods();

        binding.btnSubmitOnlinePayment.setOnClickListener(v -> {
            if (invoice == null) return;
            if (!"VNPAY Sandbox".equals(selectedMethod)) { showToast("Kênh thanh toán này chưa được tích hợp."); return; }
            Invoice updated = new Invoice(
                    invoice.getId(), invoice.getDiningSessionId(), invoice.getBranchName(), invoice.getTableId(),
                    invoice.getSubtotal(), invoice.getServiceFee(), invoice.getTax(), invoice.getVoucherDiscount(),
                    invoice.getLoyaltyDiscount(), splitPart == null ? invoice.getFinalTotal() : splitPart.getAmount(),
                    selectedMethod,
                    "Online",
                    "Vừa xong"
            );
            updated.setTableLabel(invoice.getTableLabel());
            binding.btnSubmitOnlinePayment.setEnabled(false);
            if (splitPart == null) {
                MobileRepository.getInstance().createPayment("ONLINE", "QR", null, new MobileRepository.Callback<PaymentInfo>() {
                    @Override public void onSuccess(PaymentInfo value) { if (binding != null) navigateTo(PaymentProcessingFragment.newInstance(updated, selectedMethod, value)); }
                    @Override public void onError(ApiException error) { paymentError(error); }
                });
            } else {
                MobileRepository.getInstance().createSplitPayment(splitBill, splitPart, "ONLINE", "QR", null,
                        new MobileRepository.Callback<SplitPaymentResult>() {
                            @Override public void onSuccess(SplitPaymentResult value) {
                                if (binding != null) navigateTo(PaymentProcessingFragment.newInstanceForSplit(updated, selectedMethod, value.getPayment(), value.getSplitBill().getSplitBillId()));
                            }
                            @Override public void onError(ApiException error) { paymentError(error); }
                        });
            }
        });
    }

    private void paymentError(ApiException error) {
        if (binding != null) { binding.btnSubmitOnlinePayment.setEnabled(true); showToast(apiError(error)); }
    }

    private void select(String method) {
        selectedMethod = method;
        binding.rbVietQr.setChecked("VNPAY Sandbox".equals(method));
        binding.rbMomo.setChecked("Ví MoMo".equals(method));
        binding.rbZaloPay.setChecked("Ví ZaloPay".equals(method));
        binding.rbVisaMaster.setChecked("Thẻ Quốc tế / ATM".equals(method));
    }

    private void disableUnavailableMethods() {
        selectedMethod = "VNPAY Sandbox";
        binding.rbVietQr.setChecked(true);
        binding.cardMomo.setEnabled(false);
        binding.cardZaloPay.setEnabled(false);
        binding.cardVisaMaster.setEnabled(false);
        binding.rbMomo.setEnabled(false);
        binding.rbZaloPay.setEnabled(false);
        binding.rbVisaMaster.setEnabled(false);
        binding.cardMomo.setAlpha(0.45f);
        binding.cardZaloPay.setAlpha(0.45f);
        binding.cardVisaMaster.setAlpha(0.45f);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
