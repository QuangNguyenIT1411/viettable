package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemSessionOrderBinding;
import com.restaurant.viettable.ui.common.PdStatusPill;
import com.restaurant.viettable.model.Order;
import com.restaurant.viettable.util.DisplayFormatters;
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
        holder.bind(orders.get(position), listener, position + 1);
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

        public void bind(Order order, OnOrderClickListener listener, int roundNumber) {
            binding.tvOrderTitle.setText("Đợt " + roundNumber + " • Đơn #" + order.getId()
                    + " • " + DisplayFormatters.dateTime(order.getCreatedAt()));

            StringBuilder sb = new StringBuilder();
            int itemCount = 0;
            for (int i = 0; i < order.getItems().size(); i++) {
                OrderItem item = order.getItems().get(i);
                itemCount += item.getQuantity();
                if (i > 0) sb.append("\n");
                sb.append(item.getQuantity()).append("x ").append(item.getFoodName())
                        .append(" • ").append(item.getStatusDisplayText());
            }

            binding.tvOrderItemsSummary.setText(sb.toString());
            binding.tvOrderItemCount.setText("Tổng " + itemCount + " món");
            binding.tvOrderSubtotal.setText(order.getFormattedTotalAmount());

            // Status pill (DESIGN_PHO_DEM.md section 5): sent = neutral, confirmed / done = mint, cooking = yellow, cancelled = red.
            switch (order.getStatus()) {
                case PLACED:
                    PdStatusPill.apply(binding.tvOrderStatusBadge, "ĐÃ GỬI", PdStatusPill.Tone.NEUTRAL);
                    break;
                case CONFIRMED:
                    PdStatusPill.apply(binding.tvOrderStatusBadge, "ĐÃ XÁC NHẬN", PdStatusPill.Tone.SUCCESS);
                    break;
                case IN_PROGRESS:
                    PdStatusPill.apply(binding.tvOrderStatusBadge, "ĐANG XỬ LÝ", PdStatusPill.Tone.WAITING);
                    break;
                case COMPLETED:
                    PdStatusPill.apply(binding.tvOrderStatusBadge, "HOÀN THÀNH", PdStatusPill.Tone.SUCCESS);
                    break;
                case CANCELLED:
                    PdStatusPill.apply(binding.tvOrderStatusBadge, "ĐÃ HỦY", PdStatusPill.Tone.ERROR);
                    break;
            }

            itemView.setOnClickListener(v -> {
                if (listener != null) listener.onOrderClick(order);
            });
        }
    }
}
