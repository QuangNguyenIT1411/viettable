package com.restaurant.viettable.ui.reservation;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.google.android.material.tabs.TabLayout;
import com.restaurant.viettable.adapter.ResHistoryAdapter;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentResHistoryBinding;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.List;

public class ResHistoryFragment extends BaseFragment {

    public static ResHistoryFragment newInstance() {
        return new ResHistoryFragment();
    }

    private FragmentResHistoryBinding binding;
    private final List<Reservation> filteredList = new ArrayList<>();
    private ResHistoryAdapter adapter;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentResHistoryBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.tabLayoutResHistory.addTab(binding.tabLayoutResHistory.newTab().setText("Sắp tới"));
        binding.tabLayoutResHistory.addTab(binding.tabLayoutResHistory.newTab().setText("Đang diễn ra"));
        binding.tabLayoutResHistory.addTab(binding.tabLayoutResHistory.newTab().setText("Hoàn thành"));
        binding.tabLayoutResHistory.addTab(binding.tabLayoutResHistory.newTab().setText("Đã hủy"));

        adapter = new ResHistoryAdapter(filteredList, res -> {
            navigateTo(ResDetailFragment.newInstance(res));
        });

        binding.rvResHistory.setLayoutManager(new LinearLayoutManager(getContext()));
        binding.rvResHistory.setAdapter(adapter);
        binding.btnRetryReservations.setOnClickListener(v -> loadReservations());

        filterByTab(0);
        loadReservations();

        binding.tabLayoutResHistory.addOnTabSelectedListener(new TabLayout.OnTabSelectedListener() {
            @Override
            public void onTabSelected(TabLayout.Tab tab) {
                filterByTab(tab.getPosition());
            }
            @Override public void onTabUnselected(TabLayout.Tab tab) {}
            @Override public void onTabReselected(TabLayout.Tab tab) {}
        });
    }

    private void loadReservations() {
        renderState(true, null);
        MobileRepository.getInstance().loadReservations(new MobileRepository.Callback<List<Reservation>>() {
            @Override public void onSuccess(List<Reservation> value) {
                if (binding != null) filterByTab(binding.tabLayoutResHistory.getSelectedTabPosition());
            }
            @Override public void onError(ApiException error) {
                if (binding != null) renderState(false, apiError(error));
            }
        });
    }

    private void filterByTab(int tabIndex) {
        filteredList.clear();
        List<Reservation> all = appStateManager.getReservations();

        // 0: Sắp tới (CONFIRMED)
        // 1: Đang diễn ra (CHECKED_IN)
        // 2: Hoàn thành (COMPLETED)
        // 3: Đã hủy (CANCELLED)
        for (Reservation r : all) {
            if (tabIndex == 0 && r.getStatus() == Reservation.Status.CONFIRMED) {
                filteredList.add(r);
            } else if (tabIndex == 1 && r.getStatus() == Reservation.Status.CHECKED_IN) {
                filteredList.add(r);
            } else if (tabIndex == 2 && r.getStatus() == Reservation.Status.COMPLETED) {
                filteredList.add(r);
            } else if (tabIndex == 3 && (r.getStatus() == Reservation.Status.CANCELLED || r.getStatus() == Reservation.Status.NO_SHOW)) {
                filteredList.add(r);
            }
        }

        adapter.notifyDataSetChanged();
        renderState(false, null);
    }

    private void renderState(boolean loading, @Nullable String error) {
        if (binding == null) return;
        boolean hasContent = !filteredList.isEmpty();
        binding.rvResHistory.setVisibility(hasContent ? View.VISIBLE : View.GONE);
        binding.progressReservations.setVisibility(loading && !hasContent ? View.VISIBLE : View.GONE);
        binding.tvReservationState.setVisibility(!loading && !hasContent ? View.VISIBLE : View.GONE);
        binding.btnRetryReservations.setVisibility(!loading && error != null ? View.VISIBLE : View.GONE);
        if (!loading && !hasContent) {
            binding.tvReservationState.setText(error != null ? error : getString(com.restaurant.viettable.R.string.state_no_reservations));
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
