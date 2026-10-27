package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemResHistoryBinding;
import com.restaurant.viettable.ui.common.PdStatusPill;
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
            String table = r.getTableLabel() == null || r.getTableLabel().trim().isEmpty()
                    || "0".equals(r.getTableLabel()) ? "Chưa xếp bàn" : r.getTableLabel();
            binding.tvHistDetails.setText(table + " • " + r.getGuestCount() + " khách"
                    + (r.getAreaName() == null || r.getAreaName().isEmpty() ? "" : " • " + r.getAreaName()));
            PdStatusPill.apply(binding.tvHistStatusBadge, r.getStatusDisplayText(), PdStatusPill.toneFor(r.getStatus()));

            binding.cardResItem.setOnClickListener(v -> {
                if (listener != null) listener.onReservationClick(r);
            });
        }
    }
}
