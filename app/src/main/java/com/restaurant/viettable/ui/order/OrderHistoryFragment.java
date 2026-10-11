package com.restaurant.viettable.ui.order;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.google.android.material.tabs.TabLayout;
import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.SessionOrderAdapter;
import com.restaurant.viettable.adapter.DiningSessionHistoryAdapter;
import com.restaurant.viettable.adapter.InvoiceHistoryAdapter;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentOrderHistoryBinding;
import com.restaurant.viettable.databinding.ViewActiveDiningCardBinding;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.DiningSessionHistoryGroup;
import com.restaurant.viettable.model.InvoiceHistoryItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.common.ActiveDiningCardBinder;
import com.restaurant.viettable.ui.session.DiningSessionFragment;
import com.restaurant.viettable.ui.session.DineinMenuFragment;
import com.restaurant.viettable.ui.payment.PaymentCheckoutFragment;
import com.restaurant.viettable.ui.payment.InvoiceHistoryDetailFragment;
import com.restaurant.viettable.util.DiningSessionHistoryGrouper;

import java.util.ArrayList;
import java.util.List;

public class OrderHistoryFragment extends BaseFragment implements AppStateManager.StateChangeListener {

    private FragmentOrderHistoryBinding binding;
    private final List<InvoiceHistoryItem> invoiceHistory = new ArrayList<>();
    private boolean invoiceHistoryLoading;
    private boolean invoiceHistoryLoaded;
    private String invoiceHistoryError;
    private boolean sessionHistoryLoading;
    private boolean sessionHistoryLoaded;
    private String sessionHistoryError;

    public static OrderHistoryFragment newInstance() {
        return new OrderHistoryFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentOrderHistoryBinding.inflate(inflater, container, false);
        setBottomNavVisible(true);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        appStateManager.addListener(this);

        binding.tabLayoutOrderHistory.addOnTabSelectedListener(new TabLayout.OnTabSelectedListener() {
            @Override
            public void onTabSelected(TabLayout.Tab tab) {
                renderCurrentTab();
                if (tab.getPosition() == 1 && !sessionHistoryLoaded && !sessionHistoryLoading) loadSessionHistory();
                if (tab.getPosition() == 2 && !invoiceHistoryLoaded && !invoiceHistoryLoading) loadInvoiceHistory();
            }

            @Override
            public void onTabUnselected(TabLayout.Tab tab) {}

            @Override
            public void onTabReselected(TabLayout.Tab tab) {}
        });

        binding.btnEmptyScanQr.setOnClickListener(v -> {
            switchTab(R.id.nav_qr);
        });
        binding.btnRetryHistoryInvoices.setOnClickListener(v -> {
            if (binding.tabLayoutOrderHistory.getSelectedTabPosition() == 1) loadSessionHistory();
            else loadInvoiceHistory();
        });

        renderCurrentTab();
        MobileRepository.getInstance().loadActiveSession(new MobileRepository.Callback<DiningSession>() {
            @Override public void onSuccess(DiningSession value) { if (binding != null) renderCurrentTab(); }
            @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
        });
        // Do not fetch closed-session history while the customer is viewing the active tab.
    }

    private void renderCurrentTab() {
        if (binding == null) return;
        int tabPos = binding.tabLayoutOrderHistory.getSelectedTabPosition();

        if (tabPos == 0) {
            // "Đang diễn ra"
            binding.rvPastOrders.setVisibility(View.GONE);
            binding.layoutEmptyPast.setVisibility(View.GONE);

            if (appStateManager.hasActiveDiningSession()) {
                binding.containerActiveSession.setVisibility(View.VISIBLE);
                binding.layoutEmptyOngoing.setVisibility(View.GONE);

                binding.containerActiveSession.removeAllViews();
                ViewActiveDiningCardBinding cardBinding = ViewActiveDiningCardBinding.inflate(
                        getLayoutInflater(), binding.containerActiveSession, true
                );
                ActiveDiningCardBinder.bind(cardBinding, appStateManager.getActiveDiningSession(), new ActiveDiningCardBinder.OnActionListener() {
                    @Override
                    public void onCardClicked() {
                        navigateTo(DiningSessionFragment.newInstance());
                    }

                    @Override
                    public void onOrderMoreClicked() {
                        navigateTo(new DineinMenuFragment());
                    }

                    @Override
                    public void onTrackOrdersClicked() {
                        navigateTo(new CurrentOrdersFragment());
                    }

                    @Override
                    public void onPayClicked() {
                        navigateTo(new PaymentCheckoutFragment());
                    }
                });
            } else {
                binding.containerActiveSession.setVisibility(View.GONE);
                binding.layoutEmptyOngoing.setVisibility(View.VISIBLE);
            }
        } else if (tabPos == 1) {
            // Past orders are grouped under the DiningSession that owns each round.
            binding.containerActiveSession.setVisibility(View.GONE);
            binding.layoutEmptyOngoing.setVisibility(View.GONE);

            List<Order> past = appStateManager.getPastOrders();
            if (past != null && !past.isEmpty()) {
                binding.rvPastOrders.setVisibility(View.VISIBLE);
                binding.layoutEmptyPast.setVisibility(View.GONE);
                List<DiningSessionHistoryGroup> groups = DiningSessionHistoryGrouper.group(past);
                binding.rvPastOrders.setAdapter(new DiningSessionHistoryAdapter(groups, order -> {
                    navigateTo(OrderDetailFragment.newInstance(order));
                }));
                binding.rvPastOrders.setLayoutManager(new LinearLayoutManager(requireContext()));
            } else {
                binding.rvPastOrders.setVisibility(View.GONE);
                binding.layoutEmptyPast.setVisibility(View.VISIBLE);
                binding.ivHistoryEmptyIcon.setVisibility(sessionHistoryLoading ? View.GONE : View.VISIBLE);
                binding.progressHistoryInvoices.setVisibility(sessionHistoryLoading ? View.VISIBLE : View.GONE);
                binding.tvHistoryEmptyMessage.setText(sessionHistoryLoading ? "Đang tải lịch sử phiên dùng bữa..."
                        : sessionHistoryError != null ? sessionHistoryError : "Chưa có lịch sử phiên dùng bữa");
                binding.btnRetryHistoryInvoices.setVisibility(sessionHistoryError != null ? View.VISIBLE : View.GONE);
            }
        } else {
            binding.containerActiveSession.setVisibility(View.GONE);
            binding.layoutEmptyOngoing.setVisibility(View.GONE);
            if (!invoiceHistory.isEmpty()) {
                binding.rvPastOrders.setVisibility(View.VISIBLE);
                binding.layoutEmptyPast.setVisibility(View.GONE);
                binding.rvPastOrders.setLayoutManager(new LinearLayoutManager(requireContext()));
                binding.rvPastOrders.setAdapter(new InvoiceHistoryAdapter(invoiceHistory,
                        invoice -> navigateTo(InvoiceHistoryDetailFragment.newInstance(invoice.getInvoiceId()))));
            } else {
                binding.rvPastOrders.setVisibility(View.GONE);
                binding.layoutEmptyPast.setVisibility(View.VISIBLE);
                binding.ivHistoryEmptyIcon.setVisibility(invoiceHistoryLoading ? View.GONE : View.VISIBLE);
                binding.progressHistoryInvoices.setVisibility(invoiceHistoryLoading ? View.VISIBLE : View.GONE);
                binding.tvHistoryEmptyMessage.setText(invoiceHistoryLoading ? "Đang tải lịch sử hóa đơn..."
                        : invoiceHistoryError != null ? invoiceHistoryError : "Chưa có hóa đơn đã phát hành");
                binding.btnRetryHistoryInvoices.setVisibility(invoiceHistoryError != null ? View.VISIBLE : View.GONE);
            }
        }
    }

    private void loadSessionHistory() {
        if (binding == null || sessionHistoryLoading) return;
        sessionHistoryLoading = true;
        sessionHistoryError = null;
        renderCurrentTab();
        MobileRepository.getInstance().loadOrders("history", new MobileRepository.Callback<List<Order>>() {
            @Override public void onSuccess(List<Order> value) {
                sessionHistoryLoading = false;
                sessionHistoryLoaded = true;
                sessionHistoryError = null;
                if (binding != null) renderCurrentTab();
            }
            @Override public void onError(ApiException error) {
                sessionHistoryLoading = false;
                sessionHistoryError = apiError(error);
                if (binding != null) renderCurrentTab();
            }
        });
    }

    private void loadInvoiceHistory() {
        if (binding == null || invoiceHistoryLoading) return;
        invoiceHistoryLoading = true;
        invoiceHistoryError = null;
        renderCurrentTab();
        MobileRepository.getInstance().loadInvoiceHistory(new MobileRepository.Callback<List<InvoiceHistoryItem>>() {
            @Override public void onSuccess(List<InvoiceHistoryItem> value) {
                invoiceHistoryLoading = false;
                invoiceHistoryLoaded = true;
                invoiceHistoryError = null;
                invoiceHistory.clear(); invoiceHistory.addAll(value);
                if (binding != null) renderCurrentTab();
            }
            @Override public void onError(ApiException error) {
                invoiceHistoryLoading = false;
                invoiceHistoryError = apiError(error);
                if (binding != null) renderCurrentTab();
            }
        });
    }

    @Override
    public void onStateChanged() {
        if (binding != null) {
            renderCurrentTab();
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        appStateManager.removeListener(this);
        binding = null;
    }
}
