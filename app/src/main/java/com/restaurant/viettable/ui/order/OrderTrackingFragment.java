package com.restaurant.viettable.ui.order;

import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.core.content.ContextCompat;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.TrackedItemAdapter;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentOrderTrackingBinding;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.RealtimeEventPolicy;
import com.restaurant.viettable.network.SignalRManager;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.session.DiningSessionFragment;

import java.util.ArrayList;
import java.util.List;

public class OrderTrackingFragment extends BaseFragment implements AppStateManager.StateChangeListener, SignalRManager.Listener {

    private FragmentOrderTrackingBinding binding;
    private TrackedItemAdapter adapter;
    private final List<OrderItem> allTrackedItems = new ArrayList<>();
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Runnable refreshRunnable = this::refreshOrders;
    private boolean isRefreshing;

    public static OrderTrackingFragment newInstance() {
        return new OrderTrackingFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentOrderTrackingBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        appStateManager.addListener(this);
        SignalRManager.getInstance().addListener(this);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        binding.btnBackToSession.setOnClickListener(v -> {
            navigateTo(DiningSessionFragment.newInstance());
        });

        binding.tvRefreshOrderStatus.setOnClickListener(v -> refreshOrders());
        binding.btnRetryOrderTracking.setOnClickListener(v -> refreshOrders());

        loadTrackedData();
        refreshOrders();
    }

    private void loadTrackedData() {
        DiningSession session = appStateManager.getActiveDiningSession();
        allTrackedItems.clear();

        if (session != null) {
            List<Order> orders = session.getOrders();
            if (!orders.isEmpty()) {
                Order latest = orders.get(orders.size() - 1);
                binding.tvTrackingOrderId.setText("#" + latest.getId() + " • " + latest.getStatusDisplayText().toUpperCase());
                updateStepperState(latest.getStatus());
            }

            for (Order o : orders) {
                allTrackedItems.addAll(o.getItems());
            }

            adapter = new TrackedItemAdapter(allTrackedItems, item -> {
                navigateTo(OrderItemDetailFragment.newInstance(item));
            });
            binding.rvTrackedItems.setLayoutManager(new LinearLayoutManager(requireContext()));
            binding.rvTrackedItems.setAdapter(adapter);
        }
        renderState(false, null);
    }

    private void updateStepperState(Order.Status status) {
        int colorSuccess = ContextCompat.getColor(requireContext(), R.color.colorSuccess);
        int colorSecondary = ContextCompat.getColor(requireContext(), R.color.colorSecondary);
        int colorDivider = ContextCompat.getColor(requireContext(), R.color.colorDivider);
        int colorTextSecondary = ContextCompat.getColor(requireContext(), R.color.colorTextSecondary);

        // Step 1: PLACED
        binding.ivStep1.setBackgroundTintList(ContextCompat.getColorStateList(requireContext(), R.color.colorSuccess));
        binding.tvStep1.setTextColor(colorSuccess);

        // Step 2: CONFIRMED
        boolean isConfirmedOrHigher = (status == Order.Status.CONFIRMED || status == Order.Status.IN_PROGRESS || status == Order.Status.COMPLETED);
        binding.line1To2.setBackgroundColor(isConfirmedOrHigher ? colorSuccess : colorDivider);
        binding.ivStep2.setBackgroundTintList(ContextCompat.getColorStateList(requireContext(), isConfirmedOrHigher ? R.color.colorSuccess : R.color.colorDivider));
        binding.ivStep2.setImageTintList(ContextCompat.getColorStateList(requireContext(), isConfirmedOrHigher ? R.color.colorSurface : R.color.colorTextSecondary));
        binding.tvStep2.setTextColor(isConfirmedOrHigher ? colorSuccess : colorTextSecondary);

        // Step 3: IN_PROGRESS
        boolean isInProgressOrHigher = (status == Order.Status.IN_PROGRESS || status == Order.Status.COMPLETED);
        binding.line2To3.setBackgroundColor(isInProgressOrHigher ? (status == Order.Status.COMPLETED ? colorSuccess : colorSecondary) : colorDivider);
        binding.ivStep3.setBackgroundTintList(ContextCompat.getColorStateList(requireContext(), isInProgressOrHigher ? (status == Order.Status.COMPLETED ? R.color.colorSuccess : R.color.colorSecondary) : R.color.colorDivider));
        binding.ivStep3.setImageTintList(ContextCompat.getColorStateList(requireContext(), isInProgressOrHigher ? R.color.colorSurface : R.color.colorTextSecondary));
        binding.tvStep3.setTextColor(isInProgressOrHigher ? (status == Order.Status.COMPLETED ? colorSuccess : colorSecondary) : colorTextSecondary);

        // Step 4: COMPLETED
        boolean isCompleted = (status == Order.Status.COMPLETED);
        binding.line3To4.setBackgroundColor(isCompleted ? colorSuccess : colorDivider);
        binding.ivStep4.setBackgroundTintList(ContextCompat.getColorStateList(requireContext(), isCompleted ? R.color.colorSuccess : R.color.colorDivider));
        binding.ivStep4.setImageTintList(ContextCompat.getColorStateList(requireContext(), isCompleted ? R.color.colorSurface : R.color.colorTextSecondary));
        binding.tvStep4.setTextColor(isCompleted ? colorSuccess : colorTextSecondary);
    }

    private void refreshOrders() {
        if (!isAdded() || isRefreshing) return;
        isRefreshing = true;
        renderState(true, null);
        handler.removeCallbacks(refreshRunnable);
        MobileRepository.getInstance().loadOrders("active", new MobileRepository.Callback<List<Order>>() {
            @Override public void onSuccess(List<Order> value) {
                isRefreshing = false;
                if (binding != null) { loadTrackedData(); scheduleFallbackPolling(); }
            }
            @Override public void onError(ApiException error) {
                isRefreshing = false;
                if (binding != null) {
                    renderState(false, apiError(error));
                    scheduleFallbackPolling();
                }
            }
        });
    }

    private void renderState(boolean loading, @Nullable String error) {
        if (binding == null) return;
        boolean hasContent = !allTrackedItems.isEmpty();
        binding.contentOrderTracking.setVisibility(hasContent ? View.VISIBLE : View.GONE);
        binding.layoutOrderTrackingState.setVisibility(hasContent ? View.GONE : View.VISIBLE);
        binding.progressOrderTracking.setVisibility(loading && !hasContent ? View.VISIBLE : View.GONE);
        binding.tvOrderTrackingState.setVisibility(!loading && !hasContent ? View.VISIBLE : View.GONE);
        binding.btnRetryOrderTracking.setVisibility(!loading && error != null ? View.VISIBLE : View.GONE);
        if (!loading && !hasContent) binding.tvOrderTrackingState.setText(error != null ? error
                : "Chưa có món ăn nào để theo dõi trong phiên này.");
    }

    private void scheduleFallbackPolling() {
        handler.removeCallbacks(refreshRunnable);
        if (binding != null && RealtimeEventPolicy.shouldFallbackPoll(SignalRManager.getInstance().getState())) {
            handler.postDelayed(refreshRunnable, 5000);
        }
    }

    @Override
    public void onConnectionStateChanged(SignalRManager.ConnectionState state) {
        if (binding == null) return;
        if (state == SignalRManager.ConnectionState.CONNECTED) handler.removeCallbacks(refreshRunnable);
        else scheduleFallbackPolling();
    }

    @Override
    public void onStateChanged() {
        if (binding != null) {
            loadTrackedData();
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        handler.removeCallbacks(refreshRunnable);
        isRefreshing = false;
        SignalRManager.getInstance().removeListener(this);
        appStateManager.removeListener(this);
        binding = null;
    }
}
