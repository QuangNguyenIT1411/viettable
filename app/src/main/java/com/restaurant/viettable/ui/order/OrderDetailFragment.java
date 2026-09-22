package com.restaurant.viettable.ui.order;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.adapter.TrackedItemAdapter;
import com.restaurant.viettable.databinding.FragmentOrderDetailBinding;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.profile.ReviewHistoryFragment;

public class OrderDetailFragment extends BaseFragment {

    private static final String ARG_ORDER = "arg_order";
    private FragmentOrderDetailBinding binding;
    private Order order;

    public static OrderDetailFragment newInstance(Order order) {
        OrderDetailFragment f = new OrderDetailFragment();
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
        binding = FragmentOrderDetailBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        if (order != null) {
            binding.tvOrderDetailId.setText("Mã đơn: #" + order.getId());
            binding.tvOrderDetailStatus.setText(order.getStatusDisplayText().toUpperCase());
            binding.tvOrderDetailMeta.setText(order.getTableNumber() + " • Gọi lúc: " + order.getCreatedAt() + " • " + order.getBranchName());
            binding.tvOrderSubtotalPrice.setText(order.getFormattedTotalAmount());

            TrackedItemAdapter adapter = new TrackedItemAdapter(order.getItems(), item -> {
                navigateTo(OrderItemDetailFragment.newInstance(item));
            });
            binding.rvOrderItemsDetail.setLayoutManager(new LinearLayoutManager(requireContext()));
            binding.rvOrderItemsDetail.setAdapter(adapter);

            if (order.getStatus() == Order.Status.COMPLETED) {
                binding.btnViewProgressFromDetail.setText("Xem đánh giá của tôi");
                binding.btnViewProgressFromDetail.setOnClickListener(v -> {
                    navigateTo(ReviewHistoryFragment.newInstance());
                });
            } else {
                binding.btnViewProgressFromDetail.setText("Theo dõi tiến độ món");
                binding.btnViewProgressFromDetail.setOnClickListener(v -> {
                    navigateTo(OrderTrackingFragment.newInstance());
                });
            }
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
