package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.core.content.ContextCompat;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemTrackedFoodBinding;
import com.restaurant.viettable.model.OrderItem;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.List;

public class TrackedItemAdapter extends RecyclerView.Adapter<TrackedItemAdapter.ViewHolder> {

    public interface OnTrackedItemClickListener {
        void onItemClick(OrderItem item);
    }

    private final List<OrderItem> items;
    private final OnTrackedItemClickListener listener;

    public TrackedItemAdapter(List<OrderItem> items, OnTrackedItemClickListener listener) {
        this.items = items;
        this.listener = listener;
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        ItemTrackedFoodBinding binding = ItemTrackedFoodBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false
        );
        return new ViewHolder(binding);
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        holder.bind(items.get(position), listener);
    }

    @Override
    public int getItemCount() {
        return items != null ? items.size() : 0;
    }

    public static class ViewHolder extends RecyclerView.ViewHolder {
        private final ItemTrackedFoodBinding binding;

        public ViewHolder(ItemTrackedFoodBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        public void bind(OrderItem item, OnTrackedItemClickListener listener) {
            binding.tvTrackedFoodName.setText(item.getFoodName());
            binding.tvTrackedFoodOptions.setText("SL: " + item.getQuantity() + " • " + item.getOptionsSummary());
            binding.tvTrackedFoodTime.setText("Gọi lúc " + DisplayFormatters.dateTime(item.getCreatedAt()));

            switch (item.getStatus()) {
                case PENDING:
                    binding.tvTrackedItemStatusBadge.setText("CHỜ BẾP");
                    binding.tvTrackedItemStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFEEEEEE)
                    );
                    binding.tvTrackedItemStatusBadge.setTextColor(0xFF616161);
                    break;
                case CONFIRMED:
                    binding.tvTrackedItemStatusBadge.setText("ĐÃ NHẬN");
                    binding.tvTrackedItemStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFE1F5FE)
                    );
                    binding.tvTrackedItemStatusBadge.setTextColor(0xFF0288D1);
                    break;
                case PREPARING:
                    binding.tvTrackedItemStatusBadge.setText("ĐANG NẤU");
                    binding.tvTrackedItemStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFFFF3E0)
                    );
                    binding.tvTrackedItemStatusBadge.setTextColor(0xFFED6C02);
                    break;
                case READY:
                    binding.tvTrackedItemStatusBadge.setText("SẴN SÀNG");
                    binding.tvTrackedItemStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFFBE9E7)
                    );
                    binding.tvTrackedItemStatusBadge.setTextColor(0xFFD84315);
                    break;
                case SERVED:
                    binding.tvTrackedItemStatusBadge.setText("ĐÃ PHỤC VỤ");
                    binding.tvTrackedItemStatusBadge.setBackgroundTintList(
                            android.content.res.ColorStateList.valueOf(0xFFE8F5E9)
                    );
                    binding.tvTrackedItemStatusBadge.setTextColor(0xFF2E7D32);
                    break;
            }

            itemView.setOnClickListener(v -> {
                if (listener != null) listener.onItemClick(item);
            });
        }
    }
}
