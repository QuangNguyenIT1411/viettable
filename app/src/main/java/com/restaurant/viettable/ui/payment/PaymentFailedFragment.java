package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentPaymentFailedBinding;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.session.DiningSessionFragment;

public class PaymentFailedFragment extends BaseFragment {

    private static final String ARG_INVOICE = "arg_invoice";
    private static final String ARG_PAYMENT_ID = "arg_payment_id";
    private FragmentPaymentFailedBinding binding;
    private Invoice invoice;
    private int paymentId;

    public static PaymentFailedFragment newInstance(Invoice invoice, int paymentId) {
        PaymentFailedFragment f = new PaymentFailedFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_INVOICE, invoice);
        b.putInt(ARG_PAYMENT_ID, paymentId);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            invoice = (Invoice) getArguments().getSerializable(ARG_INVOICE);
            paymentId = getArguments().getInt(ARG_PAYMENT_ID, 0);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentPaymentFailedBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnRetryPayment.setOnClickListener(v -> {
            if (invoice == null || paymentId <= 0) { navigateBack(); return; }
            binding.btnRetryPayment.setEnabled(false);
            MobileRepository.getInstance().retryPayment(paymentId, new MobileRepository.Callback<PaymentInfo>() {
                @Override public void onSuccess(PaymentInfo value) {
                    if (binding != null) navigateTo(PaymentProcessingFragment.newInstance(invoice, invoice.getPaymentMethod(), value));
                }
                @Override public void onError(ApiException error) {
                    if (binding != null) { binding.btnRetryPayment.setEnabled(true); showToast(apiError(error)); }
                }
            });
        });

        binding.btnChangePaymentMethod.setOnClickListener(v -> {
            if (invoice != null) {
                navigateTo(PaymentTypeFragment.newInstance(invoice));
            } else {
                navigateBack();
            }
        });

        binding.btnBackToSessionFromFailed.setOnClickListener(v -> {
            navigateTo(DiningSessionFragment.newInstance());
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
