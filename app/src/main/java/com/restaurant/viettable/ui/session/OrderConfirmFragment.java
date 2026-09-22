package com.restaurant.viettable.ui.session;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.restaurant.viettable.databinding.FragmentOrderConfirmBinding;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.order.OrderTrackingFragment;

public class OrderConfirmFragment extends BaseFragment {

    private static final String ARG_ORDER = "arg_order";
    private FragmentOrderConfirmBinding binding;
    private Order order;

    public static OrderConfirmFragment newInstance(Order order) {
        OrderConfirmFragment f = new OrderConfirmFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_ORDER, order);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            order = (Order) getArguments().getSerializable(ARG_ORDER);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentOrderConfirmBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        if (order != null) {
            binding.tvOrderConfirmId.setText("#" + order.getId());
            int count = 0;
            for (OrderItem oi : order.getItems()) {
                count += oi.getQuantity();
            }
            binding.tvOrderConfirmItemCount.setText(count + " món");
        }

        binding.btnTrackOrderTimeline.setOnClickListener(v -> {
            navigateTo(OrderTrackingFragment.newInstance());
        });

        binding.btnBackToSessionHub.setOnClickListener(v -> {
            navigateTo(DiningSessionFragment.newInstance());
        });
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
