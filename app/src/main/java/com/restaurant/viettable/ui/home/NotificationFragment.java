package com.restaurant.viettable.ui.home;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.google.android.material.tabs.TabLayout;
import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.NotificationAdapter;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentNotificationBinding;
import com.restaurant.viettable.model.NotificationItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.profile.CustomerVoucherFragment;
import com.restaurant.viettable.ui.order.CurrentOrdersFragment;
import com.restaurant.viettable.ui.order.OrderHistoryFragment;
import com.restaurant.viettable.ui.reservation.ResHistoryFragment;

import java.util.ArrayList;
import java.util.List;

public class NotificationFragment extends BaseFragment {

    private FragmentNotificationBinding binding;
    private final List<NotificationItem> filteredList = new ArrayList<>();
    private NotificationAdapter adapter;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentNotificationBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.tabLayoutNotif.addTab(binding.tabLayoutNotif.newTab().setText("Tất cả"));
        binding.tabLayoutNotif.addTab(binding.tabLayoutNotif.newTab().setText("Đặt bàn"));
        binding.tabLayoutNotif.addTab(binding.tabLayoutNotif.newTab().setText("Đơn hàng"));
        binding.tabLayoutNotif.addTab(binding.tabLayoutNotif.newTab().setText("Thanh toán"));
        binding.tabLayoutNotif.addTab(binding.tabLayoutNotif.newTab().setText("Ưu đãi")); // points, tiers, promotions

        adapter = new NotificationAdapter(filteredList, item -> {
            if (!item.isRead()) {
                MobileRepository.getInstance().markNotificationRead(item, new MobileRepository.Callback<Void>() {
                    @Override public void onSuccess(Void value) { if (binding != null) adapter.notifyDataSetChanged(); }
                    @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
                });
            }
            if (item.getType() == NotificationItem.Type.RESERVATION) {
                openReservation(item.getRefId());
            } else if (item.getType() == NotificationItem.Type.ORDER) {
                navigateTo(new CurrentOrdersFragment());
            } else if (item.getType() == NotificationItem.Type.PAYMENT) {
                navigateTo(OrderHistoryFragment.newInstance(OrderHistoryFragment.TAB_INVOICES));
            } else if (item.getType() == NotificationItem.Type.PROMOTION) {
                navigateTo(new CustomerVoucherFragment());
            }
        });

        binding.rvNotifications.setLayoutManager(new LinearLayoutManager(getContext()));
        binding.rvNotifications.setAdapter(adapter);
        binding.btnRetryNotifications.setOnClickListener(v -> loadNotifications());

        filterNotifications(0);
        loadNotifications();

        binding.tabLayoutNotif.addOnTabSelectedListener(new TabLayout.OnTabSelectedListener() {
            @Override
            public void onTabSelected(TabLayout.Tab tab) {
                filterNotifications(tab.getPosition());
            }
            @Override public void onTabUnselected(TabLayout.Tab tab) {}
            @Override public void onTabReselected(TabLayout.Tab tab) {}
        });

        binding.btnMarkAllRead.setOnClickListener(v -> {
            binding.btnMarkAllRead.setEnabled(false);
            MobileRepository.getInstance().markAllNotificationsRead(new MobileRepository.Callback<Void>() {
                @Override public void onSuccess(Void value) {
                    if (binding == null) return;
                    binding.btnMarkAllRead.setEnabled(true);
                    adapter.notifyDataSetChanged();
                    showToast("Đã đánh dấu đọc tất cả thông báo.");
                }
                @Override public void onError(ApiException error) {
                    if (binding == null) return;
                    binding.btnMarkAllRead.setEnabled(true);
                    showToast(apiError(error));
                }
            });
        });
    }

    /** The reservation the notification is about; the history list when it is not loaded (or was deleted). */
    private void openReservation(long reservationId) {
        com.restaurant.viettable.model.Reservation match = findReservation(reservationId);
        if (match != null) { navigateTo(com.restaurant.viettable.ui.reservation.ResDetailFragment.newInstance(match)); return; }
        MobileRepository.getInstance().loadReservations(new MobileRepository.Callback<List<com.restaurant.viettable.model.Reservation>>() {
            @Override public void onSuccess(List<com.restaurant.viettable.model.Reservation> value) {
                if (binding == null) return;
                com.restaurant.viettable.model.Reservation loaded = findReservation(reservationId);
                navigateTo(loaded != null ? com.restaurant.viettable.ui.reservation.ResDetailFragment.newInstance(loaded) : new ResHistoryFragment());
            }
            @Override public void onError(ApiException error) { if (binding != null) navigateTo(new ResHistoryFragment()); }
        });
    }

    private com.restaurant.viettable.model.Reservation findReservation(long reservationId) {
        if (reservationId <= 0) return null;
        for (com.restaurant.viettable.model.Reservation r : appStateManager.getReservations())
            if (String.valueOf(reservationId).equals(r.getId())) return r;
        return null;
    }

    private void loadNotifications() {
        renderState(true, null);
        MobileRepository.getInstance().loadNotifications(new MobileRepository.Callback<List<NotificationItem>>() {
            @Override public void onSuccess(List<NotificationItem> value) {
                if (binding != null) filterNotifications(binding.tabLayoutNotif.getSelectedTabPosition());
            }
            @Override public void onError(ApiException error) {
                if (binding != null) renderState(false, apiError(error));
            }
        });
    }

    private void filterNotifications(int tabIndex) {
        filteredList.clear();
        List<NotificationItem> all = appStateManager.getNotifications();
        if (tabIndex == 0) {
            filteredList.addAll(all);
        } else if (tabIndex == 1) {
            for (NotificationItem ni : all) if (ni.getType() == NotificationItem.Type.RESERVATION) filteredList.add(ni);
        } else if (tabIndex == 2) {
            for (NotificationItem ni : all) if (ni.getType() == NotificationItem.Type.ORDER) filteredList.add(ni);
        } else if (tabIndex == 3) {
            for (NotificationItem ni : all) if (ni.getType() == NotificationItem.Type.PAYMENT) filteredList.add(ni);
        } else if (tabIndex == 4) {
            for (NotificationItem ni : all) if (ni.getType() == NotificationItem.Type.PROMOTION) filteredList.add(ni);
        }
        adapter.notifyDataSetChanged();
        renderState(false, null);
    }

    private void renderState(boolean loading, @Nullable String error) {
        if (binding == null) return;
        boolean hasContent = !filteredList.isEmpty();
        binding.rvNotifications.setVisibility(hasContent ? View.VISIBLE : View.GONE);
        binding.progressNotifications.setVisibility(loading && !hasContent ? View.VISIBLE : View.GONE);
        binding.tvNotificationState.setVisibility(!loading && !hasContent ? View.VISIBLE : View.GONE);
        binding.btnRetryNotifications.setVisibility(!loading && error != null ? View.VISIBLE : View.GONE);
        binding.btnMarkAllRead.setVisibility(hasContent ? View.VISIBLE : View.GONE);
        if (!loading && !hasContent) {
            binding.tvNotificationState.setText(error != null ? error : getString(R.string.state_no_notifications));
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
