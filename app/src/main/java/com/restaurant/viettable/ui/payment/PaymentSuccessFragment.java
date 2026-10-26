package com.restaurant.viettable.ui.payment;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentPaymentSuccessBinding;
import com.restaurant.viettable.model.Invoice;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.profile.ReviewFragment;
import com.restaurant.viettable.util.SafeText;

public class PaymentSuccessFragment extends BaseFragment implements com.restaurant.viettable.data.AppStateManager.StateChangeListener {

    private static final String ARG_INVOICE = "arg_invoice";
    private FragmentPaymentSuccessBinding binding;
    private Invoice invoice;
    // The points arrive in the inbox a little after the payment settles: look again a few times, then stay hidden.
    private static final long[] POINTS_RETRY_MS = {0, 3000, 8000, 15000};
    private final android.os.Handler handler = new android.os.Handler(android.os.Looper.getMainLooper());

    public static PaymentSuccessFragment newInstance(Invoice invoice) {
        PaymentSuccessFragment f = new PaymentSuccessFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_INVOICE, invoice);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            invoice = (Invoice) getArguments().getSerializable(ARG_INVOICE);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentPaymentSuccessBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        // Project the server-owned closed state instead of closing the session locally.
        MobileRepository.getInstance().loadActiveSession(new MobileRepository.Callback<DiningSession>() {
            @Override public void onSuccess(DiningSession value) { }
            @Override public void onError(ApiException error) { }
        });

        String name = com.restaurant.viettable.data.GreetingNameStore.getInstance().currentName(appStateManager);
        binding.tvSuccessTitle.setText(name.isEmpty() ? "Ngon miệng nhé!" : "Ngon miệng nhé, " + name + "!");
        appStateManager.addListener(this);
        showEarnedPoints();
        if (invoice != null && invoice.getBackendPaymentId() > 0) {
            for (long delay : POINTS_RETRY_MS) handler.postDelayed(this::reloadInbox, delay);
        }

        if (invoice != null) {
            binding.tvSuccessAmount.setText(invoice.getFormattedFinalTotal());
            binding.tvSuccessInvoiceId.setText(com.restaurant.viettable.util.StatusLabels.invoiceTitle(invoice.getBackendInvoiceId(), invoice.getId()));
            binding.tvSuccessMethod.setText(invoice.getPaymentMethod());
            binding.tvSuccessBranch.setText(SafeText.join(" • ", invoice.getTableLabel(), invoice.getBranchName()));
        }

        binding.btnReviewMeal.setOnClickListener(v -> {
            if (invoice != null && invoice.getBackendInvoiceId() > 0) navigateTo(ReviewFragment.newInstanceForInvoice(invoice.getBackendInvoiceId()));
            else showToast("Không tìm thấy hóa đơn hợp lệ để đánh giá.");
        });

        // switchTab clears the back stack. Back must do the same: the screens underneath are the paid payment's
        // waiting/QR screens and must never open again (25/10: Back reopened TT33's QR after it was paid).
        binding.btnBackToHomeFromPayment.setOnClickListener(v -> switchTab(R.id.nav_home));
        requireActivity().getOnBackPressedDispatcher().addCallback(getViewLifecycleOwner(),
                new androidx.activity.OnBackPressedCallback(true) {
                    @Override public void handleOnBackPressed() { switchTab(R.id.nav_home); }
                });
    }

    private void reloadInbox() {
        if (binding == null || showEarnedPoints()) return;
        MobileRepository.getInstance().loadNotifications(new MobileRepository.Callback<java.util.List<com.restaurant.viettable.model.NotificationItem>>() {
            @Override public void onSuccess(java.util.List<com.restaurant.viettable.model.NotificationItem> value) { showEarnedPoints(); }
            @Override public void onError(ApiException error) { }
        });
    }

    /** "+N điểm tích lũy mới!" with N from this payment's own inbox message; hidden while there is none. */
    private boolean showEarnedPoints() {
        if (binding == null || invoice == null) return false;
        Integer earned = com.restaurant.viettable.util.LoyaltyDisplayRules.earnedForPayment(
                appStateManager.getNotifications(), invoice.getBackendPaymentId());
        boolean show = earned != null && earned > 0;
        binding.layoutPointsEarned.setVisibility(show ? View.VISIBLE : View.GONE);
        if (show) binding.tvPointsEarned.setText("+" + com.restaurant.viettable.util.LoyaltyDisplayRules.points(earned) + " điểm tích lũy mới!");
        return show;
    }

    @Override
    public void onStateChanged() {
        if (isAdded()) showEarnedPoints();
    }

    @Override
    public void onDestroyView() {
        handler.removeCallbacksAndMessages(null);
        appStateManager.removeListener(this);
        super.onDestroyView();
        binding = null;
    }
}
