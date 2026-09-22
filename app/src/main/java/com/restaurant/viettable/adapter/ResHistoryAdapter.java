package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemResHistoryBinding;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.List;

public class ResHistoryAdapter extends RecyclerView.Adapter<ResHistoryAdapter.ResHistoryViewHolder> {

    public interface OnResHistoryClickListener {
        void onReservationClick(Reservation res);
    }

    private final List<Reservation> list;
    private final OnResHistoryClickListener listener;

    public ResHistoryAdapter(List<Reservation> list, OnResHistoryClickListener listener) {
        this.list = list;
        this.listener = listener;
    }

    @NonNull
    @Override
    public ResHistoryViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        ItemResHistoryBinding binding = ItemResHistoryBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false
        );
        return new ResHistoryViewHolder(binding);
    }

    @Override
    public void onBindViewHolder(@NonNull ResHistoryViewHolder holder, int position) {
        holder.bind(list.get(position), listener);
    }

    @Override
    public int getItemCount() {
        return list != null ? list.size() : 0;
    }

    public static class ResHistoryViewHolder extends RecyclerView.ViewHolder {
        private final ItemResHistoryBinding binding;

        public ResHistoryViewHolder(ItemResHistoryBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        public void bind(Reservation r, OnResHistoryClickListener listener) {
            binding.tvHistResId.setText("#" + r.getId());
            binding.tvHistBranch.setText(r.getBranchName());
            binding.tvHistDateTime.setText(r.getTimeSlot() + " • " + DisplayFormatters.date(r.getDate()));
            binding.tvHistDetails.setText("Bàn " + r.getTableId() + " • " + r.getGuestCount() + " khách • " + r.getAreaName());
            binding.tvHistStatusBadge.setText(r.getStatusDisplayText());
            binding.tvHistStatusBadge.setBackgroundResource(R.drawable.bg_status_badge);
            switch (r.getStatus()) {
                case CONFIRMED:
                    binding.tvHistStatusBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFE8F5E9));
                    binding.tvHistStatusBadge.setTextColor(0xFF1B5E20);
                    break;
                case CHECKED_IN:
                    binding.tvHistStatusBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFE1F5FE));
                    binding.tvHistStatusBadge.setTextColor(0xFF01579B);
                    break;
                case COMPLETED:
                    binding.tvHistStatusBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFF5F5F5));
                    binding.tvHistStatusBadge.setTextColor(0xFF616161);
                    break;
                case CANCELLED:
                    binding.tvHistStatusBadge.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFFFFEBEE));
                    binding.tvHistStatusBadge.setTextColor(0xFFB71C1C);
                    break;
            }

            binding.cardResItem.setOnClickListener(v -> {
                if (listener != null) listener.onReservationClick(r);
            });
        }
    }
}
