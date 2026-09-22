package com.restaurant.viettable.ui.session;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.SessionOrderAdapter;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentDiningSessionBinding;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.RealtimeEventPolicy;
import com.restaurant.viettable.network.SignalRManager;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.order.CurrentOrdersFragment;
import com.restaurant.viettable.ui.order.OrderDetailFragment;
import com.restaurant.viettable.ui.order.OrderTrackingFragment;
import com.restaurant.viettable.ui.payment.PaymentCheckoutFragment;

public class DiningSessionFragment extends BaseFragment implements AppStateManager.StateChangeListener, SignalRManager.Listener {

    private FragmentDiningSessionBinding binding;
    private SessionOrderAdapter orderAdapter;
    private boolean isRefreshingSession;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Runnable fallbackRefresh = this::refreshSession;

    public static DiningSessionFragment newInstance() {
        return new DiningSessionFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentDiningSessionBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        setStatusBarLightIcons(false); // Dark icons over clean white toolbar
        appStateManager.addListener(this);
        SignalRManager.getInstance().addListener(this);

        binding.toolbar.setNavigationOnClickListener(v -> returnToHome());
        binding.btnDiningReturnHome.setOnClickListener(v -> returnToHome());
        binding.btnEmptySessionReturnHome.setOnClickListener(v -> returnToHome());

        setupActions();
        refreshSession();
    }

    private void returnToHome() {
        if (appStateManager.hasActiveDiningSession()) {
            showToast("Đã về trang chủ. Phiên dùng bữa vẫn đang hoạt động.");
        }
        switchTab(com.restaurant.viettable.R.id.nav_home);
    }

    private void setupActions() {
        binding.cardOrderMore.setOnClickListener(v -> {
            DiningSession session = appStateManager.getActiveDiningSession();
            if (session != null && session.getStatus() == DiningSession.Status.PAYMENT_REQUESTED) {
                com.restaurant.viettable.ui.common.DialogHelper.showConfirmDialog(
                        requireContext(),
                        "Không thể gọi thêm món",
                        "Bàn đang trong trạng thái chờ thanh toán nên không thể gọi thêm món. Bạn có muốn hủy yêu cầu thanh toán để tiếp tục gọi món?",
                        "Hủy yêu cầu thanh toán",
                        (dialog, which) -> {
                            MobileRepository.getInstance().cancelPaymentRequest(new MobileRepository.Callback<DiningSession>() {
                                @Override public void onSuccess(DiningSession value) {
                                    if (!isAdded()) return;
                                    showToast("Đã hủy yêu cầu thanh toán. Bạn có thể gọi thêm món.");
                                    navigateTo(DineinMenuFragment.newInstance());
                                }

                                @Override public void onError(ApiException error) {
                                    if (binding != null) showToast(apiError(error));
                                }
                            });
                        },
                        "Đóng",
                        null
                );
                return;
            }
            navigateTo(DineinMenuFragment.newInstance());
        });

        binding.cardTrackOrder.setOnClickListener(v -> {
            navigateTo(OrderTrackingFragment.newInstance());
        });

        binding.cardCallStaff.setOnClickListener(v -> {
            navigateTo(CallStaffFragment.newInstance());
        });

        binding.cardRequestPayment.setOnClickListener(v -> {
            navigateTo(PaymentCheckoutFragment.newInstance());
        });

        binding.tvViewAllCurrentOrders.setOnClickListener(v -> {
            navigateTo(CurrentOrdersFragment.newInstance());
        });
    }

    private void refreshSession() {
        if (binding == null || isRefreshingSession) return;
        isRefreshingSession = true;

        DiningSession cachedSession = appStateManager.getActiveDiningSession();
        if (cachedSession == null) showLoading();
        else bindSessionData(cachedSession);

        MobileRepository.getInstance().loadActiveSession(new MobileRepository.Callback<DiningSession>() {
            @Override public void onSuccess(DiningSession session) {
                isRefreshingSession = false;
                if (binding == null) return;
                if (session == null) showNoActiveSession();
                else bindSessionData(session);
                scheduleFallbackPolling();
            }

            @Override public void onError(ApiException error) {
                isRefreshingSession = false;
                if (binding == null) return;
                DiningSession cached = appStateManager.getActiveDiningSession();
                if (cached == null) showSessionLoadError(apiError(error));
                else {
                    bindSessionData(cached);
                    showToast(apiError(error));
                }
                scheduleFallbackPolling();
            }
        });
    }

    private void scheduleFallbackPolling() {
        handler.removeCallbacks(fallbackRefresh);
        if (binding != null && RealtimeEventPolicy.shouldFallbackPoll(SignalRManager.getInstance().getState()))
            handler.postDelayed(fallbackRefresh, 10_000);
    }

    @Override public void onConnectionStateChanged(SignalRManager.ConnectionState state) {
        if (binding == null) return;
        if (state == SignalRManager.ConnectionState.CONNECTED) handler.removeCallbacks(fallbackRefresh);
        else scheduleFallbackPolling();
    }

    private void showLoading() {
        binding.sessionContent.setVisibility(View.GONE);
        binding.emptySessionState.setVisibility(View.GONE);
        binding.progressSession.setVisibility(View.VISIBLE);
    }

    private void showNoActiveSession() {
        binding.progressSession.setVisibility(View.GONE);
        binding.sessionContent.setVisibility(View.GONE);
        binding.emptySessionState.setVisibility(View.VISIBLE);
        binding.tvEmptySessionTitle.setText("Chưa có phiên dùng bữa");
        binding.tvEmptySessionMessage.setText("Hãy quét hoặc nhập mã QR trên bàn để bắt đầu phiên phục vụ.");
        binding.btnEmptySessionPrimary.setText("Quét hoặc nhập mã bàn");
        binding.btnEmptySessionPrimary.setOnClickListener(v -> switchTab(R.id.nav_qr));
    }

    private void showSessionLoadError(String message) {
        binding.progressSession.setVisibility(View.GONE);
        binding.sessionContent.setVisibility(View.GONE);
        binding.emptySessionState.setVisibility(View.VISIBLE);
        binding.tvEmptySessionTitle.setText("Không thể tải phiên dùng bữa");
        binding.tvEmptySessionMessage.setText(message);
        binding.btnEmptySessionPrimary.setText("Thử lại");
        binding.btnEmptySessionPrimary.setOnClickListener(v -> refreshSession());
    }

    private void bindSessionData(@NonNull DiningSession session) {
            binding.progressSession.setVisibility(View.GONE);
            binding.emptySessionState.setVisibility(View.GONE);
            binding.sessionContent.setVisibility(View.VISIBLE);
            binding.tvSessionTable.setText(session.getTableId());
            binding.tvSessionBranch.setText(session.getBranchName());
            binding.tvSessionId.setText("Phiên: #" + session.getId() + " • Bắt đầu: " + session.getStartTime());

            binding.tvSessionStatusBadge.setBackgroundResource(com.restaurant.viettable.R.drawable.bg_status_badge);
            if (session.getStatus() == DiningSession.Status.PAYMENT_REQUESTED) {
                binding.tvSessionStatusBadge.setText("CHỜ THANH TOÁN");
                binding.tvSessionStatusBadge.setBackgroundTintList(
                        android.content.res.ColorStateList.valueOf(0xFFFFF3E0)
                );
                binding.tvSessionStatusBadge.setTextColor(0xFFD84315);
            } else {
                binding.tvSessionStatusBadge.setText("ĐANG PHỤC VỤ");
                binding.tvSessionStatusBadge.setBackgroundTintList(
                        android.content.res.ColorStateList.valueOf(0xFFE8F5E9)
                );
                binding.tvSessionStatusBadge.setTextColor(0xFF2E7D5B);
            }

            int totalDishes = 0;
            for (Order o : session.getOrders()) {
                for (OrderItem oi : o.getItems()) {
                    totalDishes += oi.getQuantity();
                }
            }
            binding.tvTotalDishesCount.setText(totalDishes + " món");
            binding.tvOrderRoundsCount.setText(session.getOrders().size() + " đợt");
            binding.tvEstimatedTotalAmount.setText(session.getFormattedSubtotal());

            // Orders list
            orderAdapter = new SessionOrderAdapter(session.getOrders(), order -> {
                navigateTo(OrderDetailFragment.newInstance(order));
            });
            binding.rvSessionOrders.setLayoutManager(new LinearLayoutManager(requireContext()));
            binding.rvSessionOrders.setAdapter(orderAdapter);
    }

    @Override
    public void onStateChanged() {
        if (binding != null) {
            DiningSession session = appStateManager.getActiveDiningSession();
            if (session != null) bindSessionData(session);
            else if (!isRefreshingSession) showNoActiveSession();
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        isRefreshingSession = false;
        handler.removeCallbacks(fallbackRefresh);
        SignalRManager.getInstance().removeListener(this);
        appStateManager.removeListener(this);
        binding = null;
    }
}
