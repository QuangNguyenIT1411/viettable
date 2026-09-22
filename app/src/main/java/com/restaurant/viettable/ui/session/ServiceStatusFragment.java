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

import com.restaurant.viettable.adapter.ServiceRequestAdapter;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentServiceStatusBinding;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.ServiceRequest;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.network.RealtimeEventPolicy;
import com.restaurant.viettable.network.SignalRManager;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.List;

public class ServiceStatusFragment extends BaseFragment implements AppStateManager.StateChangeListener, SignalRManager.Listener {

    private FragmentServiceStatusBinding binding;
    private ServiceRequestAdapter adapter;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Runnable fallbackRefresh = this::refreshRequests;
    private boolean refreshing;

    public static ServiceStatusFragment newInstance() {
        return new ServiceStatusFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentServiceStatusBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        appStateManager.addListener(this);
        SignalRManager.getInstance().addListener(this);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        setupList();
        refreshRequests();

        binding.btnNewServiceRequest.setOnClickListener(v -> {
            navigateTo(CallStaffFragment.newInstance());
        });

        binding.btnBackToDiningSession.setOnClickListener(v -> {
            openDiningSession();
        });
    }

    private void refreshRequests() {
        if (binding == null || refreshing || appStateManager.getActiveDiningSession() == null) return;
        refreshing = true;
        handler.removeCallbacks(fallbackRefresh);
        MobileRepository.getInstance().loadServiceRequests(new MobileRepository.Callback<List<ServiceRequest>>() {
            @Override public void onSuccess(List<ServiceRequest> value) {
                refreshing = false;
                if (binding != null) { setupList(); scheduleFallbackPolling(); }
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

    private void setupList() {
        DiningSession session = appStateManager.getActiveDiningSession();
        List<ServiceRequest> requests = (session != null) ? session.getServiceRequests() : new ArrayList<>();
        adapter = new ServiceRequestAdapter(requests, request ->
                MobileRepository.getInstance().cancelServiceRequest(request,
                        new MobileRepository.Callback<ServiceRequest>() {
                            @Override public void onSuccess(ServiceRequest value) {
                                if (binding != null) { showToast("Đã hủy yêu cầu phục vụ."); setupList(); }
                            }
                            @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
                        }));
        binding.rvServiceRequests.setLayoutManager(new LinearLayoutManager(requireContext()));
        binding.rvServiceRequests.setAdapter(adapter);
    }

    @Override
    public void onStateChanged() {
        if (binding != null && adapter != null) {
            setupList();
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
