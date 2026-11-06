package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentWaitlistBinding;
import com.restaurant.viettable.databinding.ItemWaitlistEntryBinding;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.model.WaitlistEntry;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.reminder.WaitlistPollWorker;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.common.DialogHelper;
import com.restaurant.viettable.util.WaitlistText;

import java.time.Instant;
import java.time.ZoneId;
import java.util.ArrayList;
import java.util.List;

/** "Danh sách chờ của tôi" (owner 06/11). Reading it also lets the server offer freed tables. */
public class WaitlistFragment extends BaseFragment {
    private FragmentWaitlistBinding binding;
    private final Handler ticker = new Handler(Looper.getMainLooper());
    private final List<Runnable> tickers = new ArrayList<>();

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentWaitlistBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnReloadWaitlist.setOnClickListener(v -> load());
        load();
    }

    private void load() {
        binding.btnReloadWaitlist.setEnabled(false);
        MobileRepository.getInstance().loadWaitlist(new MobileRepository.Callback<List<WaitlistEntry>>() {
            @Override public void onSuccess(List<WaitlistEntry> value) {
                WaitlistPollWorker.handle(requireContext().getApplicationContext(), value);
                if (binding == null) return;
                binding.btnReloadWaitlist.setEnabled(true);
                show(value);
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                binding.btnReloadWaitlist.setEnabled(true);
                binding.tvWaitlistState.setVisibility(View.VISIBLE);
                binding.tvWaitlistState.setText(apiError(error) + " Bấm “Tải lại” để thử lại.");
            }
        });
    }

    private void show(List<WaitlistEntry> entries) {
        stopTickers();
        binding.layoutWaitlistItems.removeAllViews();
        binding.tvWaitlistState.setVisibility(entries.isEmpty() ? View.VISIBLE : View.GONE);
        binding.tvWaitlistState.setText("Bạn chưa chờ bàn nào. Khi khung giờ hết bàn, chọn “Vào danh sách chờ”.");
        ZoneId zone = ZoneId.systemDefault();
        for (WaitlistEntry entry : entries) {
            ItemWaitlistEntryBinding row = ItemWaitlistEntryBinding.inflate(getLayoutInflater(), binding.layoutWaitlistItems, false);
            row.tvEntryStatus.setText(WaitlistText.status(entry));
            row.tvEntryBranch.setText(entry.branchName);
            row.tvEntryWindow.setText(WaitlistText.window(entry.windowStartUtc, entry.windowEndUtc, zone) + " · " + entry.partySize + " khách"
                    + (entry.closedReason == null || entry.isActive() ? "" : " · " + entry.closedReason));
            boolean rejoin = WaitlistText.canRejoin(entry, entries, Instant.now());
            row.layoutEntryActions.setVisibility(entry.isActive() || rejoin ? View.VISIBLE : View.GONE);
            row.btnEntryAccept.setVisibility(entry.isOffered() || rejoin ? View.VISIBLE : View.GONE);
            row.btnEntryAccept.setText(rejoin ? "Xếp lại hàng chờ" : "Nhận bàn");
            row.btnEntryCancel.setVisibility(entry.isActive() ? View.VISIBLE : View.GONE);
            row.btnEntryCancel.setText(entry.isOffered() ? "Bỏ qua" : "Huỷ chờ");
            if ("Expired".equals(entry.status)) {
                row.tvEntryOffer.setVisibility(View.VISIBLE);
                row.tvEntryOffer.setText(WaitlistText.lapsedDetail(entry)
                        + (rejoin ? " Bạn có thể xếp lại hàng chờ (đứng sau những người đang chờ)." : ""));
                row.tvEntryOffer.setTextColor(requireContext().getColor(com.restaurant.viettable.R.color.pd_red_text));
            }
            if (entry.isOffered()) {
                row.tvEntryOffer.setVisibility(View.VISIBLE);
                Runnable tick = new Runnable() {
                    @Override public void run() {
                        if (binding == null) return;
                        String left = WaitlistText.countdown(entry.offerExpiresAtUtc, Instant.now());
                        row.tvEntryOffer.setText("Bàn lúc " + WaitlistText.clock(entry.offeredReservedAtUtc, zone) + " · " + left);
                        if ("hết giờ".equals(left)) {
                            row.btnEntryAccept.setEnabled(false);
                            row.tvEntryStatus.setText("Lời mời đã hết hạn");
                            row.tvEntryOffer.setText(WaitlistText.LAPSED_DETAIL);
                            ticker.postDelayed(() -> { if (binding != null) load(); }, 3000); // the server closes it on read
                            return;
                        }
                        ticker.postDelayed(this, 1000);
                    }
                };
                tickers.add(tick);
                tick.run();
            }
            row.btnEntryAccept.setOnClickListener(v -> { if (rejoin) rejoin(entry); else accept(entry); });
            row.btnEntryCancel.setOnClickListener(v -> DialogHelper.showDestructiveDialog(requireContext(),
                    entry.isOffered() ? "Bỏ qua bàn này?" : "Huỷ chờ?",
                    entry.isOffered() ? "Bàn sẽ được mời cho khách kế tiếp." : "Bạn sẽ không được báo khi có bàn.",
                    entry.isOffered() ? "Bỏ qua" : "Huỷ chờ", (d, w) -> cancel(entry), "Không", null));
            binding.layoutWaitlistItems.addView(row.getRoot());
        }
    }

    private void accept(WaitlistEntry entry) {
        MobileRepository.getInstance().acceptWaitlist(entry.id, new MobileRepository.Callback<Reservation>() {
            @Override public void onSuccess(Reservation value) {
                clearOfferNotification(entry); // taken: the "Có bàn cho bạn" notification goes away
                if (binding == null) return;
                if (value.getStatus() == Reservation.Status.PENDING_DEPOSIT) navigateTo(ResDepositFragment.newInstance(value));
                else navigateTo(ResDetailFragment.newInstance(value));
            }
            @Override public void onError(ApiException error) {
                if (binding == null) return;
                showToast(apiError(error));
                load();
            }
        });
    }

    private void clearOfferNotification(WaitlistEntry entry) {
        if (getContext() != null) WaitlistPollWorker.clearOffer(requireContext().getApplicationContext(), entry.id);
    }

    private void rejoin(WaitlistEntry entry) {
        MobileRepository.getInstance().rejoinWaitlist(entry.id, new MobileRepository.Callback<WaitlistEntry>() {
            @Override public void onSuccess(WaitlistEntry value) {
                if (binding == null) return;
                showToast(value.isOffered() ? "Có bàn cho bạn ngay!" : "Đã xếp lại hàng chờ" + (value.position > 0 ? ", vị trí thứ " + value.position : "") + ".");
                load();
            }
            @Override public void onError(ApiException error) { if (binding != null) { showToast(apiError(error)); load(); } }
        });
    }

    private void cancel(WaitlistEntry entry) {
        MobileRepository.getInstance().cancelWaitlist(entry.id, new MobileRepository.Callback<Void>() {
            @Override public void onSuccess(Void value) { clearOfferNotification(entry); if (binding != null) load(); }
            @Override public void onError(ApiException error) { if (binding != null) { showToast(apiError(error)); load(); } }
        });
    }

    private void stopTickers() {
        for (Runnable r : tickers) ticker.removeCallbacks(r);
        tickers.clear();
    }

    @Override
    public void onDestroyView() {
        stopTickers();
        super.onDestroyView();
        binding = null;
    }
}
