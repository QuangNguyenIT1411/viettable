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
import com.restaurant.viettable.util.SafeText;

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
            binding.tvTrackedFoodName.setText(item.getQuantity() > 1
                    ? item.getFoodName() + " ×" + item.getQuantity() : item.getFoodName());
            String options = SafeText.clean(item.getOptionsSummary());
            binding.tvTrackedFoodOptions.setText(options);
            binding.tvTrackedFoodOptions.setVisibility(options.isEmpty() ? android.view.View.GONE : android.view.View.VISIBLE);
            binding.tvTrackedFoodTime.setText("Gọi lúc " + DisplayFormatters.dateTime(item.getCreatedAt()));
            bindPlate(item);

            switch (item.getStatus()) {
                case PENDING:
                    pill("Chờ bếp", R.color.pd_surface_2, R.color.pd_muted, R.drawable.ic_clock);
                    break;
                case CONFIRMED:
                    pill("Đã nhận", R.color.pd_surface_2, R.color.pd_muted, R.drawable.ic_check);
                    break;
                case PREPARING:
                    pill("Đang nấu", R.color.pd_yellow, R.color.pd_on_color, R.drawable.ic_flame);
                    break;
                case READY:
                    pill("Sẵn sàng", R.color.pd_mint, R.color.pd_on_color, R.drawable.ic_cloche);
                    break;
                case SERVED:
                    pill("Đã phục vụ", R.color.pd_mint, R.color.pd_on_color, R.drawable.ic_check);
                    break;
                case CANCELLED:
                    pill("Đã hủy", R.color.pd_red, R.color.pd_on_color, R.drawable.ic_close);
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

        /** Status pill as in design P04: coloured fill, on_color text, 14dp icon tinted like the text. */
        private void pill(String text, int fillColor, int textColor, int iconRes) {
            android.content.Context context = itemView.getContext();
            android.widget.TextView badge = binding.tvTrackedItemStatusBadge;
            badge.setText(text);
            badge.setBackgroundTintList(ContextCompat.getColorStateList(context, fillColor));
            badge.setTextColor(ContextCompat.getColor(context, textColor));
            android.graphics.drawable.Drawable icon = ContextCompat.getDrawable(context, iconRes).mutate();
            int size = (int) (14 * context.getResources().getDisplayMetrics().density);
            icon.setBounds(0, 0, size, size);
            badge.setCompoundDrawablesRelative(icon, null, null, null);
            badge.setCompoundDrawableTintList(ContextCompat.getColorStateList(context, textColor));
        }

        /** The order line only carries the dish name, so the photo comes from the menu entry with the same name (empty plate when none). */
        private void bindPlate(OrderItem item) {
            com.restaurant.viettable.model.FoodItem match = null;
            String name = SafeText.clean(item.getFoodName());
            for (com.restaurant.viettable.model.FoodItem food : com.restaurant.viettable.data.AppStateManager.getInstance().getFoodItems()) {
                if (food.getName() != null && food.getName().trim().equalsIgnoreCase(name.trim())) { match = food; break; }
            }
            if (match != null) {
                com.restaurant.viettable.util.ImageLoader.getInstance().load(match, binding.ivTrackedFoodImage);
            } else {
                binding.ivTrackedFoodImage.setImageDrawable(null);
            }
            binding.ivTrackedFoodImage.setBackgroundResource(R.drawable.pd_plate_empty);
        }
    }
}
