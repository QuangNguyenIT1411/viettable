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
import java.util.HashSet;
import java.util.Set;

public class TrackedItemAdapter extends RecyclerView.Adapter<TrackedItemAdapter.ViewHolder> {

    public interface OnTrackedItemClickListener {
        void onItemClick(OrderItem item);
    }

    private final List<OrderItem> items;
    private final OnTrackedItemClickListener listener;
    public interface OnReceiveClickListener { void onReceive(OrderItem item); }
    private final OnReceiveClickListener receiveListener;
    private final Set<String> pendingReceiveIds = new HashSet<>();

    public TrackedItemAdapter(List<OrderItem> items, OnTrackedItemClickListener listener) {
        this(items, listener, null);
    }

    public TrackedItemAdapter(List<OrderItem> items, OnTrackedItemClickListener listener, OnReceiveClickListener receiveListener) {
        this.items = items;
        this.listener = listener;
        this.receiveListener = receiveListener;
    }

    public void setReceivePending(OrderItem item, boolean pending) {
        String id = item.getId();
        if (pending) pendingReceiveIds.add(id); else pendingReceiveIds.remove(id);
        int index = items.indexOf(item);
        if (index >= 0) notifyItemChanged(index);
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
        holder.bind(items.get(position), listener, receiveListener, pendingReceiveIds.contains(items.get(position).getId()));
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

        public void bind(OrderItem item, OnTrackedItemClickListener listener, OnReceiveClickListener receiveListener, boolean pending) {
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
            boolean canReceive = item.getStatus() == OrderItem.Status.READY && receiveListener != null;
            binding.btnConfirmReceived.setVisibility(canReceive ? android.view.View.VISIBLE : android.view.View.GONE);
            binding.btnConfirmReceived.setEnabled(!pending);
            binding.btnConfirmReceived.setText(pending ? "Đang cập nhật…" : "Tôi đã nhận món");
            binding.btnConfirmReceived.setOnClickListener(canReceive && !pending ? v -> receiveListener.onReceive(item) : null);
        }
    }
}
