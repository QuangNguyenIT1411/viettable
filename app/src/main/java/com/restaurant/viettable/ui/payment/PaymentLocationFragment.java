package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentPaymentLocationBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.model.SplitPaymentResult;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.network.ApiException;
import org.json.JSONObject;
import com.restaurant.viettable.ui.base.BaseFragment;

public class PaymentLocationFragment extends BaseFragment {

    private static final String ARG_INVOICE = "arg_invoice";
    private static final String ARG_METHOD = "arg_method";
    private static final String ARG_SPLIT = "arg_split";
    private static final String ARG_PART = "arg_part";

    private FragmentPaymentLocationBinding binding;
    private Invoice invoice;
    private String paymentMethod = "Tiền mặt";
    private String selectedLocation = "Tại bàn";
    private SplitBill splitBill;
    private SplitBill.Part splitPart;

    public static PaymentLocationFragment newInstance(Invoice invoice, String method) {
        PaymentLocationFragment f = new PaymentLocationFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_INVOICE, invoice);
        b.putString(ARG_METHOD, method);
        f.setArguments(b);
        return f;
    }

    public static PaymentLocationFragment newInstanceForSplit(Invoice invoice, String method, SplitBill splitBill, SplitBill.Part part) {
        PaymentLocationFragment fragment = newInstance(invoice, method);
        fragment.requireArguments().putSerializable(ARG_SPLIT, splitBill);
        fragment.requireArguments().putSerializable(ARG_PART, part);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            invoice = (Invoice) getArguments().getSerializable(ARG_INVOICE);
            paymentMethod = getArguments().getString(ARG_METHOD, "Tiền mặt");
            splitBill = (SplitBill) getArguments().getSerializable(ARG_SPLIT);
            splitPart = (SplitBill.Part) getArguments().getSerializable(ARG_PART);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentPaymentLocationBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        binding.cardPayAtTable.setOnClickListener(v -> {
            selectedLocation = "Tại bàn";
            binding.rbPayAtTable.setChecked(true);
            binding.rbPayAtCounter.setChecked(false);
        });

        binding.cardPayAtCounter.setOnClickListener(v -> {
            selectedLocation = "Tại quầy thu ngân";
            binding.rbPayAtTable.setChecked(false);
            binding.rbPayAtCounter.setChecked(true);
        });

        if (invoice == null) {
            binding.btnSubmitLocation.setEnabled(false);
            showToast("Không tìm thấy báo giá thanh toán từ máy chủ.");
        }

        binding.btnSubmitLocation.setOnClickListener(v -> {
            if (invoice == null) return;
            Invoice updated = new Invoice(
                    invoice.getId(), invoice.getDiningSessionId(), invoice.getBranchName(), invoice.getTableId(),
                    invoice.getSubtotal(), invoice.getServiceFee(), invoice.getTax(), invoice.getVoucherDiscount(),
                    invoice.getLoyaltyDiscount(), splitPart == null ? invoice.getFinalTotal() : splitPart.getAmount(),
                    paymentMethod,
                    selectedLocation,
                    "Vừa xong"
            );
            updated.setTableLabel(invoice.getTableLabel());

            String method = "Tiền mặt".equals(paymentMethod) ? "CASH" : ("QR chuyển khoản".equals(paymentMethod) ? "QR" : "TRANSFER");
            String location = "Tại bàn".equals(selectedLocation) ? "TABLE" : "CASHIER";
            binding.btnSubmitLocation.setEnabled(false);
            if (splitPart == null) {
                MobileRepository.getInstance().createPayment("ONSITE", method, location, new MobileRepository.Callback<PaymentInfo>() {
                    @Override public void onSuccess(PaymentInfo value) { if (binding != null) { showToast("Đã gửi yêu cầu thanh toán tới nhân viên phục vụ!"); navigateTo(OnsiteStatusFragment.newInstance(updated, selectedLocation, value)); } }
                    @Override public void onError(ApiException error) { paymentError(error); }
                });
            } else {
                MobileRepository.getInstance().createSplitPayment(splitBill, splitPart, "ONSITE", method, location,
                        new MobileRepository.Callback<SplitPaymentResult>() {
                            @Override public void onSuccess(SplitPaymentResult value) {
                                if (binding != null) { showToast("Đã gửi yêu cầu thanh toán phần hóa đơn."); navigateTo(OnsiteStatusFragment.newInstanceForSplit(updated, selectedLocation, value.getPayment(), value.getSplitBill().getSplitBillId())); }
                            }
                            @Override public void onError(ApiException error) { paymentError(error); }
                        });
            }
        });
    }

    private void paymentError(ApiException error) {
        if (binding != null) { binding.btnSubmitLocation.setEnabled(true); showToast(apiError(error)); }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
