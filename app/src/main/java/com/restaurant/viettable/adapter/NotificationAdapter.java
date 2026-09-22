package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemNotificationBinding;
import com.restaurant.viettable.model.NotificationItem;

import java.util.List;

public class NotificationAdapter extends RecyclerView.Adapter<NotificationAdapter.NotificationViewHolder> {

    public interface OnNotificationClickListener {
        void onNotificationClick(NotificationItem item);
    }

    private final List<NotificationItem> items;
    private final OnNotificationClickListener listener;

    public NotificationAdapter(List<NotificationItem> items, OnNotificationClickListener listener) {
        this.items = items;
        this.listener = listener;
    }

    @NonNull
    @Override
    public NotificationViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        ItemNotificationBinding binding = ItemNotificationBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false
        );
        return new NotificationViewHolder(binding);
    }

    @Override
    public void onBindViewHolder(@NonNull NotificationViewHolder holder, int position) {
        holder.bind(items.get(position), listener);
    }

    @Override
    public int getItemCount() {
        return items != null ? items.size() : 0;
    }

    public static class NotificationViewHolder extends RecyclerView.ViewHolder {
        private final ItemNotificationBinding binding;

        public NotificationViewHolder(ItemNotificationBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        public void bind(NotificationItem item, OnNotificationClickListener listener) {
            binding.tvNotifTitle.setText(item.getTitle());
            binding.tvNotifMessage.setText(item.getMessage());
            binding.tvNotifTimestamp.setText(item.getTimestamp());
            binding.viewUnreadDot.setVisibility(item.isRead() ? View.GONE : View.VISIBLE);

            if (item.getType() == NotificationItem.Type.RESERVATION) {
                binding.ivNotifIcon.setImageResource(R.drawable.ic_reservation);
            } else if (item.getType() == NotificationItem.Type.ORDER) {
                binding.ivNotifIcon.setImageResource(R.drawable.ic_orders);
            } else if (item.getType() == NotificationItem.Type.PAYMENT) {
                binding.ivNotifIcon.setImageResource(R.drawable.ic_card);
            } else {
                binding.ivNotifIcon.setImageResource(R.drawable.ic_voucher);
            }

            binding.cardNotification.setOnClickListener(v -> {
                item.setRead(true);
                binding.viewUnreadDot.setVisibility(View.GONE);
                if (listener != null) listener.onNotificationClick(item);
            });
        }
    }
}
