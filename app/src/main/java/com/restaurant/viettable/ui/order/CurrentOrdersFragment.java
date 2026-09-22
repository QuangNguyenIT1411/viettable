package com.restaurant.viettable.ui.order;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.adapter.SessionOrderAdapter;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentCurrentOrdersBinding;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.RealtimeEventPolicy;
import com.restaurant.viettable.network.SignalRManager;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.session.DineinMenuFragment;

import java.util.ArrayList;
import java.util.List;

public class CurrentOrdersFragment extends BaseFragment implements AppStateManager.StateChangeListener, SignalRManager.Listener {

    private FragmentCurrentOrdersBinding binding;
    private SessionOrderAdapter adapter;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Runnable fallbackRefresh = this::refreshOrders;
    private boolean refreshing;

    public static CurrentOrdersFragment newInstance() {
        return new CurrentOrdersFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentCurrentOrdersBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        appStateManager.addListener(this);
        SignalRManager.getInstance().addListener(this);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        binding.btnOrderMoreDishes.setOnClickListener(v -> {
            DiningSession session = appStateManager.getActiveDiningSession();
            if (session != null && session.getStatus() == DiningSession.Status.PAYMENT_REQUESTED) {
                com.restaurant.viettable.ui.common.DialogHelper.showConfirmDialog(
                        requireContext(),
                        "Không thể gọi thêm món",
                        "Bàn đang trong trạng thái chờ thanh toán nên không thể gọi thêm món. Bạn có muốn hủy yêu cầu thanh toán để tiếp tục gọi món?",
                        "Hủy yêu cầu thanh toán",
                        (dialog, which) -> {
                            session.setStatus(DiningSession.Status.ACTIVE);
                            appStateManager.notifyStateChanged();
                            showToast("Đã hủy yêu cầu thanh toán. Bạn có thể gọi thêm món.");
                            navigateTo(DineinMenuFragment.newInstance());
                        },
                        "Đóng",
                        null
                );
                return;
            }
            navigateTo(DineinMenuFragment.newInstance());
        });

        binding.btnTrackItemsProgress.setOnClickListener(v -> {
            navigateTo(OrderTrackingFragment.newInstance());
        });

        loadOrders();
        refreshOrders();
    }

    private void refreshOrders() {
        if (binding == null || refreshing) return;
        refreshing = true;
        handler.removeCallbacks(fallbackRefresh);
        MobileRepository.getInstance().loadOrders("active", new MobileRepository.Callback<List<Order>>() {
            @Override public void onSuccess(List<Order> value) {
                refreshing = false;
                if (binding != null) { loadOrders(); scheduleFallbackPolling(); }
            }
            @Override public void onError(ApiException error) {
                refreshing = false;
                if (binding != null) scheduleFallbackPolling();
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

    private void loadOrders() {
        DiningSession session = appStateManager.getActiveDiningSession();
        if (session != null) {
            binding.tvCurrentOrdersTable.setText(session.getTableId() + " • " + session.getBranchName());

            int totalItems = 0;
            for (Order o : session.getOrders()) {
                for (OrderItem oi : o.getItems()) {
                    totalItems += oi.getQuantity();
                }
            }
            binding.tvTotalItems.setText(totalItems + " món");
            binding.tvTotalSubtotal.setText(session.getFormattedSubtotal());

            adapter = new SessionOrderAdapter(session.getOrders(), order -> {
                navigateTo(OrderDetailFragment.newInstance(order));
            });
            binding.rvCurrentOrders.setLayoutManager(new LinearLayoutManager(requireContext()));
            binding.rvCurrentOrders.setAdapter(adapter);
        }
    }

    @Override
    public void onStateChanged() {
        if (binding != null) {
            loadOrders();
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        handler.removeCallbacks(fallbackRefresh);
        refreshing = false;
        SignalRManager.getInstance().removeListener(this);
        appStateManager.removeListener(this);
        binding = null;
    }
}
