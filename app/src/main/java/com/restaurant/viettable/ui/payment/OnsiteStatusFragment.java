package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentOnsiteStatusBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

public class OnsiteStatusFragment extends BaseFragment {

    private static final String ARG_INVOICE = "arg_invoice";
    private static final String ARG_LOCATION = "arg_location";
    private static final String ARG_SPLIT_ID = "arg_split_id";
    private static final String ARG_PAYMENT = "arg_payment";

    private FragmentOnsiteStatusBinding binding;
    private Invoice invoice;
    private String location = "Tại bàn";
    private int splitBillId;
    private PaymentInfo payment;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private Runnable pollRunnable;
    private boolean requestInFlight;
    private boolean finishing;
    private int viewGeneration;

    public static OnsiteStatusFragment newInstance(Invoice invoice, String location, PaymentInfo payment) {
        OnsiteStatusFragment f = new OnsiteStatusFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_INVOICE, invoice);
        b.putString(ARG_LOCATION, location);
        b.putSerializable(ARG_PAYMENT, payment);
        f.setArguments(b);
        return f;
    }

    public static OnsiteStatusFragment newInstanceForSplit(Invoice invoice, String location, PaymentInfo payment, int splitBillId) {
        OnsiteStatusFragment fragment = newInstance(invoice, location, payment);
        fragment.requireArguments().putInt(ARG_SPLIT_ID, splitBillId);
        return fragment;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            invoice = (Invoice) getArguments().getSerializable(ARG_INVOICE);
            location = getArguments().getString(ARG_LOCATION, "Tại bàn");
            splitBillId = getArguments().getInt(ARG_SPLIT_ID, 0);
            payment = (PaymentInfo) getArguments().getSerializable(ARG_PAYMENT);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentOnsiteStatusBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (invoice != null) {
            binding.tvOnsiteLocation.setText(location + " (" + invoice.getTableId() + ")");
            binding.tvOnsiteMethod.setText(invoice.getPaymentMethod());
            binding.tvOnsiteTotal.setText(invoice.getFormattedFinalTotal());
        }

        binding.btnRefreshOnsiteStatus.setOnClickListener(v -> schedulePoll(0));
        pollRunnable = this::refreshStatus;

        binding.btnCancelOnsiteRequest.setOnClickListener(v -> {
            com.restaurant.viettable.ui.common.DialogHelper.showDestructiveDialog(
                    requireContext(),
                    "Hủy yêu cầu thanh toán?",
                    "Bạn có chắc muốn hủy yêu cầu thanh toán và tiếp tục dùng bữa?",
                    "Hủy yêu cầu",
                    (dialog, which) -> {
                        MobileRepository.getInstance().cancelPaymentRequest(new MobileRepository.Callback<DiningSession>() {
                            @Override public void onSuccess(DiningSession value) { showToast("Đã hủy yêu cầu thanh toán."); navigateBack(); }
                            @Override public void onError(ApiException error) { showToast(apiError(error)); }
                        });
                    },
                    "Không",
                    null
            );
        });
    }

    private void refreshStatus() {
        if (binding == null || !isResumed() || requestInFlight || finishing) return;
        requestInFlight = true;
        int generation = viewGeneration;
        int paymentId = payment == null ? 0 : payment.getPaymentId();
        MobileRepository.getInstance().refreshPayment(paymentId, new MobileRepository.Callback<PaymentInfo>() {
            @Override public void onSuccess(PaymentInfo value) {
                if (generation != viewGeneration) return;
                requestInFlight = false;
                if (binding == null || !isResumed()) return;
                payment = value;
                binding.tvOnsiteStatusDesc.setText("Yêu cầu thanh toán của bạn đã được gửi tới nhân viên phụ trách khu vực.");
                String status = value.getStatus();
                if ("SUCCEEDED".equals(status)) { finishing = true; handleSucceeded(); }
                else if ("FAILED".equals(status) || "CANCELLED".equals(status) || "EXPIRED".equals(status)) {
                    finishing = true;
                    if (splitBillId > 0) { showToast("Phần thanh toán chưa thành công. Bạn có thể thử lại."); navigateTo(SplitBillFragment.newInstance(invoice)); }
                    else navigateTo(PaymentFailedFragment.newInstance(invoice, value.getPaymentId()));
                }
                else schedulePoll(3000);
            }
            @Override public void onError(ApiException error) {
                if (generation != viewGeneration) return;
                requestInFlight = false;
                if (binding == null || !isResumed()) return;
                binding.tvOnsiteStatusDesc.setText(apiError(error) + " Đang tự thử lại...");
                schedulePoll(5000);
            }
        });
    }

    private void schedulePoll(long delayMs) {
        if (pollRunnable == null || binding == null || !isResumed() || finishing) return;
        handler.removeCallbacks(pollRunnable);
        handler.postDelayed(pollRunnable, delayMs);
    }

    @Override public void onResume() { super.onResume(); schedulePoll(0); }
    @Override public void onPause() {
        if (pollRunnable != null) handler.removeCallbacks(pollRunnable);
        finishing = false;
        super.onPause();
    }

    private void handleSucceeded() {
        if (splitBillId <= 0) {
            MobileRepository.getInstance().loadIssuedInvoice(new MobileRepository.Callback<Invoice>() {
                @Override public void onSuccess(Invoice issued) { if (binding != null && isResumed()) navigateTo(PaymentSuccessFragment.newInstance(issued)); }
                @Override public void onError(ApiException error) { retryAfterFinalizationError(error); }
            });
            return;
        }
        MobileRepository.getInstance().loadSplitBill(splitBillId, new MobileRepository.Callback<SplitBill>() {
            @Override public void onSuccess(SplitBill value) {
                if (binding == null || !isResumed()) return;
                if (value.areAllPartsPaid()) MobileRepository.getInstance().loadIssuedInvoice(new MobileRepository.Callback<Invoice>() {
                    @Override public void onSuccess(Invoice issued) { if (binding != null && isResumed()) navigateTo(PaymentSuccessFragment.newInstance(issued)); }
                    @Override public void onError(ApiException error) { retryAfterFinalizationError(error); }
                });
                else navigateTo(SplitBillFragment.newInstance(invoice));
            }
            @Override public void onError(ApiException error) { retryAfterFinalizationError(error); }
        });
    }

    private void retryAfterFinalizationError(ApiException error) {
        if (binding == null || !isResumed()) return;
        finishing = false;
        binding.tvOnsiteStatusDesc.setText(apiError(error) + " Đang tự thử lại...");
        schedulePoll(5000);
    }

    @Override
    public void onDestroyView() {
        if (pollRunnable != null) handler.removeCallbacks(pollRunnable);
        viewGeneration++;
        requestInFlight = false;
        finishing = false;
        binding = null;
        super.onDestroyView();
    }
}
