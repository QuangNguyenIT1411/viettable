package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentResDepositBinding;
import com.restaurant.viettable.model.DepositInfo;
import com.restaurant.viettable.model.DepositPolicy;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.payment.TransferQrImages;
import com.restaurant.viettable.util.DepositRules;
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.util.PdMotion;
import com.restaurant.viettable.util.SafeText;

import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/**
 * "Đặt cọc giữ bàn" (docs/DEPOSIT_PLAN.md): the VietQR of the deposit ("VT DC<id>"), the time left out of 15 minutes,
 * and a read every 5 seconds; SePay confirms by itself and the screen moves on to "Đặt bàn thành công". The server
 * decides every state (paid, expired, cancelled); this screen only shows it.
 */
public class ResDepositFragment extends BaseFragment {

    private static final String ARG_RESERVATION = "arg_reservation";
    private static final long POLL_MS = 5000;

    private FragmentResDepositBinding binding;
    private Reservation reservation;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final List<android.animation.Animator> loops = new ArrayList<>();
    private final Runnable poll = this::load;
    private final Runnable tick = this::tick;
    private long dueAtMillis;
    private boolean qrShown;
    private boolean finished;
    private boolean loading;

    public static ResDepositFragment newInstance(Reservation reservation) {
        ResDepositFragment f = new ResDepositFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_RESERVATION, reservation);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) reservation = (Reservation) getArguments().getSerializable(ARG_RESERVATION);
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResDepositBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        if (reservation == null) { showToast("Không tìm thấy thông tin đặt bàn."); navigateBack(); return; }
        qrShown = false;
        finished = false;
        // Back never returns to the last booking step (it would book a second table): the booking stays in history
        // and its detail screen leads back here.
        binding.btnBack.setOnClickListener(v -> leaveToHistory());
        binding.btnDepositPrimary.setOnClickListener(v -> leaveToHistory());

        binding.tvDepositAmount.setText(DepositRules.money(reservation.getDepositAmount()));
        binding.tvDepositSummary.setText(SafeText.join(" · ", reservation.getGuestCount() + " khách", reservation.getTableLabel(),
                reservation.getTimeSlot() + ", " + DisplayFormatters.date(reservation.getDate())));
        binding.tvDepositCountdown.setVisibility(View.GONE);
        binding.tvDepositPolicy.setText(DepositRules.policyNote(null));
        MobileRepository.getInstance().loadDepositPolicy(new MobileRepository.Callback<DepositPolicy>() {
            @Override public void onSuccess(DepositPolicy value) { if (binding != null) binding.tvDepositPolicy.setText(DepositRules.policyNote(value)); }
            @Override public void onError(ApiException error) { }
        });
        startWaitingMotion();
        load();
    }

    @Override
    public void onResume() {
        super.onResume();
        if (binding != null && !finished) { handler.removeCallbacks(poll); handler.post(poll); }
    }

    @Override
    public void onPause() {
        handler.removeCallbacks(poll);
        handler.removeCallbacks(tick);
        super.onPause();
    }

    private void load() {
        if (binding == null || finished || loading) return;
        loading = true;
        MobileRepository.getInstance().loadDeposit(reservation.getId(), new MobileRepository.Callback<DepositInfo>() {
            @Override public void onSuccess(DepositInfo info) {
                loading = false;
                if (binding != null) apply(info);
            }
            @Override public void onError(ApiException error) {
                loading = false;
                if (binding == null) return;
                binding.tvDepositStatusDesc.setText(apiError(error) + " Đang tự thử lại…");
                schedulePoll();
            }
        });
    }

    private void apply(DepositInfo info) {
        if (info.getAmount() > 0) binding.tvDepositAmount.setText(DepositRules.money(info.getAmount()));
        bindCopy(binding.btnCopyAmount, "Số tiền", String.valueOf(info.getAmount()));
        String status = info.getStatus();
        if (DepositRules.isWaiting(status)) {
            showTransfer(info.getTransfer());
            java.time.Instant due = DisplayFormatters.instant(info.getDueAtUtc());
            dueAtMillis = due == null ? 0 : due.toEpochMilli();
            handler.removeCallbacks(tick);
            tick();
            binding.tvDepositWaitTitle.setText("Đang chờ tiền cọc…");
            binding.tvDepositStatusDesc.setText(info.getMessage());
            schedulePoll();
            return;
        }
        handler.removeCallbacks(tick);
        binding.tvDepositCountdown.setVisibility(View.GONE);
        if (DepositRules.isPaid(status)) {
            finished = true;
            stopLoops();
            showToast("Đã nhận tiền cọc " + DepositRules.money(info.getAmount()) + ".");
            if (info.getReservationStatus() != null)
                reservation.setStatus(MobileRepository.reservationStatus(info.getReservationStatus()));
            navigateTo(ResConfirmationFragment.newInstance(reservation));
            return;
        }
        // Expired, cancelled before paying, or already closed: say so in words; no QR to pay any more.
        finished = true;
        stopLoops();
        binding.cardTransferQr.setVisibility(View.GONE);
        binding.cardDepositWaiting.setVisibility(View.GONE);
        binding.btnSaveTransferQr.setVisibility(View.GONE);
        binding.btnCopyAmount.setVisibility(View.GONE);
        binding.tvDepositResult.setVisibility(View.VISIBLE);
        binding.tvDepositResult.setBackgroundResource(DepositRules.isClosedUnpaid(status) ? R.drawable.pd_card_red_soft : R.drawable.pd_card_yellow_soft);
        binding.tvDepositResult.setText(info.getMessage());
        binding.btnDepositPrimary.setText(DepositRules.isClosedUnpaid(status) ? "Đặt bàn khác" : "Xem đặt bàn của tôi");
        if (DepositRules.isClosedUnpaid(status)) binding.btnDepositPrimary.setOnClickListener(v -> switchTab(R.id.nav_reservation));
    }

    private void showTransfer(@Nullable JSONObject qr) {
        if (qr == null) {
            binding.cardTransferQr.setVisibility(View.VISIBLE);
            binding.tvTransferState.setText("Nhà hàng chưa cấu hình tài khoản nhận chuyển khoản. Vui lòng liên hệ nhà hàng.");
            return;
        }
        if (qrShown) return;
        android.graphics.Bitmap image = TransferQrImages.decode(SafeText.json(qr, "qrPngBase64"));
        binding.cardTransferQr.setVisibility(View.VISIBLE);
        if (image == null) { binding.tvTransferState.setText("Không hiển thị được mã QR."); return; }
        qrShown = true;
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
        binding.tvTransferHint.setVisibility(View.VISIBLE);
        binding.tvTransferHint.setText(qr.optBoolean("autoConfirm")
                ? "Hệ thống sẽ tự xác nhận khi nhận được tiền."
                : "Sau khi chuyển khoản, nhà hàng sẽ kiểm tra và xác nhận.");
        binding.btnSaveTransferQr.setEnabled(true);
        binding.btnSaveTransferQr.setOnClickListener(v -> showToast(
                TransferQrImages.saveToGallery(requireContext(), image, "VietTable-Coc-" + reservation.getId())
                        ? "Đã lưu ảnh QR vào thư mục Ảnh (VietTable)."
                        : "Không lưu được ảnh. Bạn có thể chụp màn hình mã QR."));
        startScanMotion();
    }

    /** Once a second: "Còn 14:05 để đặt cọc"; at zero, ask the server (it cancels the booking and frees the table). */
    private void tick() {
        if (binding == null || finished || dueAtMillis <= 0) return;
        long left = dueAtMillis - System.currentTimeMillis();
        binding.tvDepositCountdown.setVisibility(View.VISIBLE);
        binding.tvDepositCountdown.setText(left > 0 ? "Còn " + DepositRules.countdown(left) + " để đặt cọc" : "Đã hết thời gian đặt cọc");
        if (left > 0) handler.postDelayed(tick, 1000);
        else { handler.removeCallbacks(poll); handler.postDelayed(poll, 1500); }
    }

    private void schedulePoll() {
        handler.removeCallbacks(poll);
        if (!finished && isResumed()) handler.postDelayed(poll, POLL_MS);
    }

    private void leaveToHistory() {
        switchTab(R.id.nav_reservation);
        navigateTo(new ResHistoryFragment());
    }

    private void bindCopy(View button, String what, String value) {
        button.setVisibility(SafeText.isBlank(value) || "0".equals(value) ? View.GONE : View.VISIBLE);
        button.setOnClickListener(v -> showToast(TransferQrImages.copy(requireContext(), what, value)
                ? "Đã sao chép " + what.toLowerCase(java.util.Locale.forLanguageTag("vi")) : "Không sao chép được."));
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

    private void stopLoops() {
        for (android.animation.Animator loop : loops) loop.cancel();
        loops.clear();
        if (binding != null) binding.viewScanLine.setVisibility(View.GONE);
    }

    @Override
    public void onDestroyView() {
        handler.removeCallbacks(poll);
        handler.removeCallbacks(tick);
        stopLoops();
        super.onDestroyView();
        binding = null;
    }
}
