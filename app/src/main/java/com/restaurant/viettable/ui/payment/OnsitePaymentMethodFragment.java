package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentOnsitePaymentBinding;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.ui.base.BaseFragment;

public class OnsitePaymentMethodFragment extends BaseFragment {

    private static final String ARG_INVOICE = "arg_invoice";
    private static final String ARG_SPLIT = "arg_split";
    private static final String ARG_PART = "arg_part";
    private FragmentOnsitePaymentBinding binding;
    private Invoice invoice;
    private SplitBill splitBill;
    private SplitBill.Part splitPart;
    private String selectedMethod = "Tiền mặt";

    public static OnsitePaymentMethodFragment newInstance(Invoice invoice) {
        OnsitePaymentMethodFragment f = new OnsitePaymentMethodFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_INVOICE, invoice);
        f.setArguments(b);
        return f;
    }

    public static OnsitePaymentMethodFragment newInstanceForSplit(Invoice invoice, SplitBill splitBill, SplitBill.Part part) {
        OnsitePaymentMethodFragment fragment = newInstance(invoice);
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
        binding = FragmentOnsitePaymentBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        binding.cardCash.setOnClickListener(v -> selectMethod("Tiền mặt"));
        binding.cardPos.setOnClickListener(v -> selectMethod("Quẹt thẻ POS"));
        binding.cardTransfer.setOnClickListener(v -> selectMethod("QR chuyển khoản"));

        if (invoice == null) {
            binding.btnProceedToLocation.setEnabled(false);
            showToast("Không tìm thấy báo giá thanh toán từ máy chủ.");
        }

        binding.btnProceedToLocation.setOnClickListener(v -> {
            if (invoice == null) return;
            Invoice updated = new Invoice(
                    invoice.getId(), invoice.getDiningSessionId(), invoice.getBranchName(), invoice.getTableId(),
                    invoice.getSubtotal(), invoice.getServiceFee(), invoice.getTax(), invoice.getVoucherDiscount(),
                    invoice.getLoyaltyDiscount(), invoice.getFinalTotal(),
                    selectedMethod,
                    "Chưa chọn",
                    "Vừa xong"
            );
            updated.setTableLabel(invoice.getTableLabel());
            navigateTo(splitPart == null ? PaymentLocationFragment.newInstance(updated, selectedMethod)
                    : PaymentLocationFragment.newInstanceForSplit(updated, selectedMethod, splitBill, splitPart));
        });
    }

    private void selectMethod(String method) {
        selectedMethod = method;
        binding.rbCash.setChecked("Tiền mặt".equals(method));
        binding.rbPos.setChecked("Quẹt thẻ POS".equals(method));
        binding.rbTransfer.setChecked("QR chuyển khoản".equals(method));
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
