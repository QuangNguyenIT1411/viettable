package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentPaymentTypeBinding;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.model.SplitPaymentResult;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.SafeText;

public class PaymentTypeFragment extends BaseFragment {
    static final String CASH_LABEL = "Tiền mặt";
    static final String TRANSFER_LABEL = "Chuyển khoản";
    static final String TRANSFER_LOCATION_LABEL = "Chuyển khoản qua ngân hàng";

    private static final String ARG_INVOICE = "arg_invoice";
    private static final String ARG_SPLIT = "arg_split";
    private static final String ARG_PART = "arg_part";
    private FragmentPaymentTypeBinding binding;
    private Invoice invoice;
    private SplitBill splitBill;
    private SplitBill.Part splitPart;

    public static PaymentTypeFragment newInstance(Invoice invoice) {
        PaymentTypeFragment f = new PaymentTypeFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_INVOICE, invoice);
        f.setArguments(b);
        return f;
    }

    public static PaymentTypeFragment newInstanceForSplit(Invoice invoice, SplitBill splitBill, SplitBill.Part part) {
        PaymentTypeFragment fragment = newInstance(invoice);
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
        binding = FragmentPaymentTypeBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        if (invoice != null) {
            binding.tvPaymentTypeGrandTotal.setText(splitPart == null ? invoice.getFormattedFinalTotal() : splitPart.getFormattedAmount());
            binding.tvPaymentTypeTable.setText(SafeText.join(" • ", invoice.getTableLabel(), invoice.getBranchName()));
        }

        binding.cardPayOnline.setOnClickListener(v -> {
            navigateTo(splitPart == null ? OnlinePaymentMethodFragment.newInstance(invoice)
                    : OnlinePaymentMethodFragment.newInstanceForSplit(invoice, splitBill, splitPart));
        });

        // Guests pay cash or by bank transfer (VietQR). The online gateway choice sits behind a build flag (off by
        // default) and, when on, still needs a gateway the server reports as configured.
        binding.cardPayOnline.setVisibility(View.GONE);
        if (com.restaurant.viettable.BuildConfig.ONLINE_PAYMENT_ENABLED) {
            MobileRepository.getInstance().loadOnlinePaymentMethods(new MobileRepository.Callback<java.util.List<String>>() {
                @Override public void onSuccess(java.util.List<String> methods) {
                    if (binding != null) binding.cardPayOnline.setVisibility(methods.isEmpty() ? View.GONE : View.VISIBLE);
                }
                @Override public void onError(ApiException error) { }
            });
        }
        MobileRepository.getInstance().loadTransferAvailable(new MobileRepository.Callback<Boolean>() {
            @Override public void onSuccess(Boolean available) {
                if (binding != null) binding.cardPayTransfer.setVisibility(Boolean.TRUE.equals(available) ? View.VISIBLE : View.GONE);
            }
            @Override public void onError(ApiException error) {
                if (binding != null) binding.cardPayTransfer.setVisibility(View.GONE);
            }
        });

        binding.cardPayOnsite.setOnClickListener(v -> {
            navigateTo(splitPart == null ? PaymentLocationFragment.newInstance(invoice, CASH_LABEL)
                    : PaymentLocationFragment.newInstanceForSplit(invoice, CASH_LABEL, splitBill, splitPart));
        });

        binding.cardPayTransfer.setOnClickListener(v -> startTransfer());
    }

    /** Creates the bank-transfer payment, then shows its VietQR while the cashier confirms receipt. */
    private void startTransfer() {
        if (invoice == null || binding == null) return;
        binding.cardPayTransfer.setEnabled(false);
        Invoice updated = new Invoice(
                invoice.getId(), invoice.getDiningSessionId(), invoice.getBranchName(), invoice.getTableId(),
                invoice.getSubtotal(), invoice.getServiceFee(), invoice.getTax(), invoice.getVoucherDiscount(),
                invoice.getLoyaltyDiscount(), splitPart == null ? invoice.getFinalTotal() : splitPart.getAmount(),
                TRANSFER_LABEL, TRANSFER_LOCATION_LABEL, "Vừa xong");
        updated.setTableLabel(invoice.getTableLabel());
        if (splitPart == null) {
            MobileRepository.getInstance().createPayment("ONSITE", "TRANSFER", "TABLE", new MobileRepository.Callback<PaymentInfo>() {
                @Override public void onSuccess(PaymentInfo value) {
                    if (binding != null) navigateTo(OnsiteStatusFragment.newInstance(updated, TRANSFER_LOCATION_LABEL, value));
                }
                @Override public void onError(ApiException error) { transferError(error); }
            });
        } else {
            MobileRepository.getInstance().createSplitPayment(splitBill, splitPart, "ONSITE", "TRANSFER", "TABLE",
                    new MobileRepository.Callback<SplitPaymentResult>() {
                        @Override public void onSuccess(SplitPaymentResult value) {
                            if (binding != null) navigateTo(OnsiteStatusFragment.newInstanceForSplit(updated, TRANSFER_LOCATION_LABEL,
                                    value.getPayment(), value.getSplitBill().getSplitBillId()));
                        }
                        @Override public void onError(ApiException error) { transferError(error); }
                    });
        }
    }

    private void transferError(ApiException error) {
        if (binding != null) { binding.cardPayTransfer.setEnabled(true); showToast(apiError(error)); }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
