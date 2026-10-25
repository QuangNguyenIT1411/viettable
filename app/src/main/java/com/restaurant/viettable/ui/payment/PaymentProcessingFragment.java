package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.content.Intent;
import android.net.Uri;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentPaymentProcessingBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.DisplayFormatters;

public class PaymentProcessingFragment extends BaseFragment implements com.restaurant.viettable.ui.lock.LockExempt {

    private static final String ARG_INVOICE = "arg_invoice";
    private static final String ARG_METHOD = "arg_method";
    private static final String ARG_SPLIT_ID = "arg_split_id";
    private static final String ARG_PAYMENT = "arg_payment";
    private static final String STATE_PROVIDER_OPENED = "provider_opened";

    private FragmentPaymentProcessingBinding binding;
    private Invoice invoice;
    private String method = "VietQR";
    private int splitBillId;
    private PaymentInfo payment;
    private boolean providerOpened;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private Runnable pollRunnable;

    public static PaymentProcessingFragment newInstance(Invoice invoice, String method, PaymentInfo payment) {
        PaymentProcessingFragment f = new PaymentProcessingFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_INVOICE, invoice);
        b.putString(ARG_METHOD, method);
        b.putSerializable(ARG_PAYMENT, payment);
        f.setArguments(b);
        return f;
    }

    public static PaymentProcessingFragment newInstanceForSplit(Invoice invoice, String method, PaymentInfo payment, int splitBillId) {
        PaymentProcessingFragment fragment = newInstance(invoice, method, payment);
        fragment.requireArguments().putInt(ARG_SPLIT_ID, splitBillId);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            invoice = (Invoice) getArguments().getSerializable(ARG_INVOICE);
            method = getArguments().getString(ARG_METHOD, "VietQR");
            splitBillId = getArguments().getInt(ARG_SPLIT_ID, 0);
            payment = (PaymentInfo) getArguments().getSerializable(ARG_PAYMENT);
        }
        if (savedInstanceState != null) providerOpened = savedInstanceState.getBoolean(STATE_PROVIDER_OPENED, false);
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentPaymentProcessingBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.tvProcessingMethod.setText("Kết nối cổng: " + method);
        if (invoice != null) {
            binding.tvProcessingAmount.setText(invoice.getFormattedFinalTotal());
        }
        renderProviderDetails();

        pollRunnable = this::refreshStatus;
        binding.btnRefreshPaymentStatus.setOnClickListener(v -> refreshStatus());
        binding.btnOpenProvider.setOnClickListener(v -> openProvider());
        if (payment != null && payment.hasPaymentUrl() && !providerOpened)
            binding.getRoot().post(this::openProvider);
    }

    private void renderProviderDetails() {
        if (binding == null || payment == null) return;
        String provider = payment.getProviderName();
        binding.tvProcessingMethod.setText(provider == null ? "Phương thức: " + method : "Cổng thanh toán: " + provider);
        binding.tvProviderReference.setText(payment.getProviderReference() == null ? "" : "Mã tham chiếu: " + payment.getProviderReference());
        binding.tvProviderReference.setVisibility(payment.getProviderReference() == null ? View.GONE : View.VISIBLE);
        binding.tvProviderExpiry.setText(payment.getProviderExpiresAtUtc() == null ? "" : "Hiệu lực đến: " + DisplayFormatters.dateTime(payment.getProviderExpiresAtUtc()));
        binding.tvProviderExpiry.setVisibility(payment.getProviderExpiresAtUtc() == null ? View.GONE : View.VISIBLE);
        binding.btnOpenProvider.setVisibility(payment.hasPaymentUrl() ? View.VISIBLE : View.GONE);
    }

    private void openProvider() {
        if (payment == null || !payment.hasPaymentUrl() || !isAdded()) return;
        Uri uri = Uri.parse(payment.getPaymentUrl());
        String scheme = uri.getScheme();
        if (!"https".equalsIgnoreCase(scheme) && !"http".equalsIgnoreCase(scheme)) { showToast("Liên kết thanh toán không hợp lệ."); return; }
        try {
            providerOpened = true;
            com.restaurant.viettable.ui.lock.AppLock.get().expectExternalReturn();
            startActivity(new Intent(Intent.ACTION_VIEW, uri));
        } catch (Exception error) { showToast("Không tìm thấy ứng dụng có thể mở cổng thanh toán."); }
    }

    private void refreshStatus() {
        if (!isAdded()) return;
        int paymentId = payment == null ? 0 : payment.getPaymentId();
        MobileRepository.getInstance().refreshPayment(paymentId, new MobileRepository.Callback<PaymentInfo>() {
            @Override public void onSuccess(PaymentInfo value) {
                if (binding == null) return;
                payment = value; renderProviderDetails(); String status = value.getStatus();
                if ("SUCCEEDED".equals(status)) handleSucceeded();
                else if ("FAILED".equals(status) || "CANCELLED".equals(status) || "EXPIRED".equals(status)) {
                    if (splitBillId > 0) { showToast("Phần thanh toán chưa thành công. Bạn có thể thử lại."); navigateTo(SplitBillFragment.newInstance(invoice)); }
                    else navigateTo(PaymentFailedFragment.newInstance(invoice, value.getPaymentId()));
                }
                else {
                    long delay = com.restaurant.viettable.util.PaymentPollPolicy.nextDelayMs(android.os.SystemClock.elapsedRealtime() - waitStartedAt);
                    if (delay > 0) schedulePoll(delay); // stops after 15 min; onResume checks again
                }
            }
            @Override public void onError(ApiException error) { if (binding != null) { showToast(apiError(error)); schedulePoll(5000); } }
        });
    }

    private final long waitStartedAt = android.os.SystemClock.elapsedRealtime();

    private void schedulePoll(long delayMs) {
        if (pollRunnable == null) return;
        handler.removeCallbacks(pollRunnable); handler.postDelayed(pollRunnable, delayMs);
    }

    @Override public void onResume() { super.onResume(); if (binding != null && payment != null) refreshStatus(); }
    @Override public void onPause() { if (pollRunnable != null) handler.removeCallbacks(pollRunnable); super.onPause(); }

    @Override public void onSaveInstanceState(@NonNull Bundle outState) {
        super.onSaveInstanceState(outState); outState.putBoolean(STATE_PROVIDER_OPENED, providerOpened);
    }

    private void handleSucceeded() {
        if (splitBillId <= 0) {
            MobileRepository.getInstance().loadIssuedInvoice(new MobileRepository.Callback<Invoice>() {
                @Override public void onSuccess(Invoice issued) { if (binding != null) navigateTo(PaymentSuccessFragment.newInstance(issued)); }
                @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
            });
            return;
        }
        MobileRepository.getInstance().loadSplitBill(splitBillId, new MobileRepository.Callback<SplitBill>() {
            @Override public void onSuccess(SplitBill value) {
                if (binding == null) return;
                if (value.areAllPartsPaid()) MobileRepository.getInstance().loadIssuedInvoice(new MobileRepository.Callback<Invoice>() {
                    @Override public void onSuccess(Invoice issued) { if (binding != null) navigateTo(PaymentSuccessFragment.newInstance(issued)); }
                    @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
                });
                else navigateTo(SplitBillFragment.newInstance(invoice));
            }
            @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        if (pollRunnable != null) {
            handler.removeCallbacks(pollRunnable);
        }
        binding = null;
    }
}
