package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemSessionOrderBinding;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.model.OrderItem;

import java.util.List;

public class SessionOrderAdapter extends RecyclerView.Adapter<SessionOrderAdapter.ViewHolder> {

    public interface OnOrderClickListener {
        void onOrderClick(Order order);
    }

    private final List<Order> orders;
    private final OnOrderClickListener listener;

    public SessionOrderAdapter(List<Order> orders, OnOrderClickListener listener) {
        this.orders = orders;
        this.listener = listener;
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        ItemSessionOrderBinding binding = ItemSessionOrderBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false
        );
        return new ViewHolder(binding);
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        holder.bind(orders.get(position), listener);
    }

    @Override
    public int getItemCount() {
        return orders != null ? orders.size() : 0;
    }

    public static class ViewHolder extends RecyclerView.ViewHolder {
        private final ItemSessionOrderBinding binding;

        public ViewHolder(ItemSessionOrderBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        public void bind(Order order, OnOrderClickListener listener) {
            binding.tvOrderTitle.setText("Đơn #" + order.getId() + " • " + order.getCreatedAt());

            StringBuilder sb = new StringBuilder();
            int itemCount = 0;
            for (int i = 0; i < order.getItems().size(); i++) {
                OrderItem item = order.getItems().get(i);
                itemCount += item.getQuantity();
                if (i > 0) sb.append(", ");
                sb.append(item.getQuantity()).append("x ").append(item.getFoodName());
            }

            binding.tvOrderItemsSummary.setText(sb.toString());
            binding.tvOrderItemCount.setText("Tổng " + itemCount + " món");
            binding.tvOrderSubtotal.setText(order.getFormattedTotalAmount());

            // Status chip with guaranteed readable contrast
            binding.tvOrderStatusBadge.setBackgroundResource(R.drawable.bg_status_badge);
            switch (order.getStatus()) {
                case PLACED:
                    binding.tvOrderStatusBadge.setText("ĐÃ GỬI");
                    binding.tvOrderStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFE1F5FE)
                    );
                    binding.tvOrderStatusBadge.setTextColor(0xFF01579B);
                    break;
                case CONFIRMED:
                    binding.tvOrderStatusBadge.setText("ĐÃ XÁC NHẬN");
                    binding.tvOrderStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFF3E5F5)
                    );
                    binding.tvOrderStatusBadge.setTextColor(0xFF4A148C);
                    break;
                case IN_PROGRESS:
                    binding.tvOrderStatusBadge.setText("ĐANG XỬ LÝ");
                    binding.tvOrderStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFFFF3E0)
                    );
                    binding.tvOrderStatusBadge.setTextColor(0xFFD84315);
                    break;
                case COMPLETED:
                    binding.tvOrderStatusBadge.setText("HOÀN THÀNH");
                    binding.tvOrderStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFE8F5E9)
                    );
                    binding.tvOrderStatusBadge.setTextColor(0xFF1B5E20);
                    break;
                case CANCELLED:
                    binding.tvOrderStatusBadge.setText("ĐÃ HỦY");
                    binding.tvOrderStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFFFEBEE)
                    );
                    binding.tvOrderStatusBadge.setTextColor(0xFFB71C1C);
                    break;
            }

            itemView.setOnClickListener(v -> {
                if (listener != null) listener.onOrderClick(order);
            });
        }
    }
}
