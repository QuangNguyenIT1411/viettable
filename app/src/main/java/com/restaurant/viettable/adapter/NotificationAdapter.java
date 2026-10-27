package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemNotificationBinding;
import com.restaurant.viettable.model.NotificationItem;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.List;
import com.restaurant.viettable.util.SafeText;

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
            binding.tvNotifMessage.setText(SafeText.clean(item.getMessage()));
            binding.tvNotifTimestamp.setText(DisplayFormatters.dateTime(item.getTimestamp()));
            binding.viewUnreadDot.setVisibility(item.isRead() ? View.GONE : View.VISIBLE);

            // Round icon by type (DESIGN_PHO_DEM.md section 6 "Thông báo"): booking yellow, order red, payment mint, offer plum.
            int circle, tint;
            if (item.getType() == NotificationItem.Type.RESERVATION) {
                binding.ivNotifIcon.setImageResource(R.drawable.ic_reservation);
                circle = R.drawable.pd_circle_yellow; tint = R.color.pd_on_color;
            } else if (item.getType() == NotificationItem.Type.ORDER) {
                binding.ivNotifIcon.setImageResource(R.drawable.ic_orders);
                circle = R.drawable.pd_circle_red; tint = R.color.pd_on_color;
            } else if (item.getType() == NotificationItem.Type.PAYMENT) {
                binding.ivNotifIcon.setImageResource(R.drawable.ic_card);
                circle = R.drawable.pd_circle_mint; tint = R.color.pd_on_color;
            } else {
                binding.ivNotifIcon.setImageResource(R.drawable.ic_voucher);
                circle = R.drawable.pd_circle_plum; tint = R.color.pd_yellow;
            }
            binding.ivNotifIcon.setBackgroundResource(circle);
            binding.ivNotifIcon.setImageTintList(androidx.core.content.ContextCompat.getColorStateList(itemView.getContext(), tint));

            binding.cardNotification.setOnClickListener(v -> {
                item.setRead(true);
                binding.viewUnreadDot.setVisibility(View.GONE);
                if (listener != null) listener.onNotificationClick(item);
            });
        }
    }
}
