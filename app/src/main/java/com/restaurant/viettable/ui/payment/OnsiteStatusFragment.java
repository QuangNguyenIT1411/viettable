package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentOnsiteStatusBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.SplitBill;
import com.restaurant.viettable.model.PaymentInfo;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.PdMotion;
import com.restaurant.viettable.util.SafeText;

public class OnsiteStatusFragment extends BaseFragment implements com.restaurant.viettable.ui.lock.LockExempt {

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
    private final java.util.List<android.animation.Animator> loops = new java.util.ArrayList<>();

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
            binding.tvOnsiteLocation.setText(location + " (" + invoice.getTableLabel() + ")");
            binding.tvOnsiteMethod.setText(invoice.getPaymentMethod());
            binding.tvOnsiteTotal.setText(invoice.getFormattedFinalTotal());
        }

        // A finished payment skips the QR; onResume's first status check moves straight to its result screen.
        if (invoice != null && PaymentTypeFragment.TRANSFER_LABEL.equals(invoice.getPaymentMethod()) && payment != null
                && com.restaurant.viettable.util.PaymentPollPolicy.showsTransferQr(payment.getStatus())) {
            binding.cardTransferQr.setVisibility(View.VISIBLE);
            binding.btnSaveTransferQr.setVisibility(View.VISIBLE);
            binding.tvOnsiteHeading.setText(R.string.pd_onsite_heading_transfer);
            binding.tvOnsiteWaitTitle.setText(R.string.pd_onsite_wait_transfer);
            binding.tvTransferHint.setVisibility(View.VISIBLE);
            loadTransferQr();
        }

        startWaitingMotion();
        binding.btnRefreshOnsiteStatus.setOnClickListener(v -> { waitStartedAt = android.os.SystemClock.elapsedRealtime(); schedulePoll(0); });
        if (waitStartedAt == 0) waitStartedAt = android.os.SystemClock.elapsedRealtime();
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
                            @Override public void onError(ApiException error) {
                                showToast(PaymentResume.isPaymentBlocked(error)
                                        ? "Giao dịch đang chờ xác nhận nên không thể hủy. Nếu cần đổi cách thanh toán, vui lòng gọi thu ngân."
                                        : apiError(error));
                            }
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
                else if ("CANCELLED".equals(status) && splitBillId <= 0) { finishing = true; handleCancelledByCashier(); }
                else if ("FAILED".equals(status) || "CANCELLED".equals(status) || "EXPIRED".equals(status)) {
                    finishing = true;
                    if (splitBillId > 0) { showToast("Phần thanh toán chưa thành công. Bạn có thể thử lại."); navigateTo(SplitBillFragment.newInstance(invoice)); }
                    else navigateTo(PaymentFailedFragment.newInstance(invoice, value.getPaymentId()));
                }
                else scheduleNextPoll();
            }
            @Override public void onError(ApiException error) {
                if (generation != viewGeneration) return;
                requestInFlight = false;
                if (binding == null || !isResumed()) return;
                binding.tvOnsiteStatusDesc.setText(apiError(error) + " Đang tự thử lại...");
                scheduleNextPoll();
            }
        });
    }

    private long waitStartedAt;

    /** Realtime announces the result; polling only backs it up, slower over time, stopping after 15 minutes. */
    private void scheduleNextPoll() {
        long delay = com.restaurant.viettable.util.PaymentPollPolicy.nextDelayMs(android.os.SystemClock.elapsedRealtime() - waitStartedAt);
        if (delay < 0) {
            if (binding != null) binding.tvOnsiteStatusDesc.setText("Đã tạm dừng tự kiểm tra. Bấm \"Kiểm tra lại\" để cập nhật trạng thái.");
            return;
        }
        schedulePoll(delay);
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

    /**
     * Only the cashier cancels a waiting onsite payment (the guest cannot while it waits). The server has unlocked
     * the session, so start checkout again from Trang chủ: a new quote and request, e.g. by cash. The old payment
     * screens are cleared so Back never returns to the cancelled QR.
     */
    private void handleCancelledByCashier() {
        showToast("Thu ngân đã hủy giao dịch này. Vui lòng chọn lại cách thanh toán.");
        MobileRepository.getInstance().loadActiveSession(new MobileRepository.Callback<DiningSession>() {
            @Override public void onSuccess(DiningSession value) { restartCheckout(); }
            @Override public void onError(ApiException error) { restartCheckout(); }
        });
    }

    private void restartCheckout() {
        if (binding == null || !isResumed()) return;
        switchTab(com.restaurant.viettable.R.id.nav_home);
        navigateTo(PaymentCheckoutFragment.newInstance());
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

    private void bindCopy(View button, String what, String value) {
        button.setVisibility(SafeText.isBlank(value) ? View.GONE : View.VISIBLE);
        button.setOnClickListener(v -> showToast(TransferQrImages.copy(requireContext(), what, value)
                ? "Đã sao chép " + what.toLowerCase(java.util.Locale.forLanguageTag("vi")) : "Không sao chép được."));
    }

    private void loadTransferQr() {
        MobileRepository.getInstance().loadTransferQr(payment.getPaymentId(), new MobileRepository.Callback<org.json.JSONObject>() {
            @Override public void onSuccess(org.json.JSONObject qr) {
                if (binding == null) return;
                android.graphics.Bitmap image = TransferQrImages.decode(SafeText.json(qr, "qrPngBase64"));
                if (image == null) { binding.tvTransferState.setText("Không hiển thị được mã QR."); return; }
                binding.ivTransferQr.setImageBitmap(image);
                binding.tvTransferState.setVisibility(View.GONE);
                binding.tvTransferBankAccount.setText(SafeText.join(" · ", SafeText.json(qr, "bankName"), SafeText.json(qr, "accountNumber")));
                binding.tvTransferAccountName.setText(SafeText.json(qr, "accountName"));
                binding.layoutTransferAccount.setVisibility(View.VISIBLE);
                binding.tvTransferContent.setText(SafeText.json(qr, "content"));
                binding.layoutTransferTear.setVisibility(View.VISIBLE);
                binding.layoutTransferContent.setVisibility(View.VISIBLE);
                bindCopy(binding.btnCopyAccount, "Số tài khoản", SafeText.json(qr, "accountNumber"));
                bindCopy(binding.btnCopyAmount, "Số tiền", TransferQrImages.amountDigits(qr.optDouble("amount")));
                bindCopy(binding.btnCopyContent, "Nội dung chuyển khoản", SafeText.json(qr, "content"));
                startScanMotion();
                binding.tvTransferHint.setText(qr.optBoolean("autoConfirm")
                        ? "Hệ thống sẽ tự xác nhận khi nhận được tiền."
                        : "Sau khi chuyển khoản, vui lòng chờ thu ngân kiểm tra và xác nhận.");
                binding.btnSaveTransferQr.setEnabled(true);
                binding.btnSaveTransferQr.setOnClickListener(v -> showToast(
                        TransferQrImages.saveToGallery(requireContext(), image, "VietTable-QR-" + payment.getPaymentId())
                                ? "Đã lưu ảnh QR vào thư mục Ảnh (VietTable)."
                                : "Không lưu được ảnh. Bạn có thể chụp màn hình mã QR."));
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                // 409: the payment is no longer waiting (paid or cancelled meanwhile); let the status check decide.
                if (error.getStatusCode() == 409) { binding.cardTransferQr.setVisibility(View.GONE); binding.btnSaveTransferQr.setVisibility(View.GONE); schedulePoll(0); }
                else binding.tvTransferState.setText(apiError(error));
            }
        });
    }

    /** The "waiting" dot sends out a mint ring every 1.2s (DESIGN_PHO_DEM.md section 7). */
    private void startWaitingMotion() {
        if (!PdMotion.enabled(requireContext())) return;
        android.animation.ObjectAnimator ring = android.animation.ObjectAnimator.ofPropertyValuesHolder(binding.viewWaitPulse,
                android.animation.PropertyValuesHolder.ofFloat(View.SCALE_X, 0.45f, 1f),
                android.animation.PropertyValuesHolder.ofFloat(View.SCALE_Y, 0.45f, 1f),
                android.animation.PropertyValuesHolder.ofFloat(View.ALPHA, 0.6f, 0f));
        ring.setDuration(1200);
        ring.setRepeatCount(android.animation.ValueAnimator.INFINITE);
        ring.start();
        loops.add(ring);
    }

    /** The scan bar runs up and down over the QR (1.8s, repeating). */
    private void startScanMotion() {
        binding.viewScanLine.setVisibility(View.VISIBLE);
        if (!PdMotion.enabled(requireContext())) { binding.viewScanLine.setVisibility(View.GONE); return; }
        float travel = (220 - 56) * getResources().getDisplayMetrics().density;
        android.animation.ObjectAnimator scan = android.animation.ObjectAnimator.ofFloat(binding.viewScanLine, View.TRANSLATION_Y, 0f, travel);
        scan.setDuration(1800);
        scan.setRepeatMode(android.animation.ValueAnimator.REVERSE);
        scan.setRepeatCount(android.animation.ValueAnimator.INFINITE);
        scan.start();
        loops.add(scan);
    }

    @Override
    public void onDestroyView() {
        for (android.animation.Animator loop : loops) loop.cancel();
        loops.clear();
        if (pollRunnable != null) handler.removeCallbacks(pollRunnable);
        viewGeneration++;
        requestInFlight = false;
        finishing = false;
        binding = null;
        super.onDestroyView();
    }
}
