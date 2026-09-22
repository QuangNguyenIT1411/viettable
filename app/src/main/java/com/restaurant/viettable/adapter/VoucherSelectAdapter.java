package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.databinding.ItemVoucherSelectableBinding;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.List;

public class VoucherSelectAdapter extends RecyclerView.Adapter<VoucherSelectAdapter.ViewHolder> {

    public interface OnVoucherSelectedListener {
        void onVoucherSelected(Voucher voucher);
    }

    private final List<Voucher> vouchers;
    private int selectedPosition = -1;
    private final OnVoucherSelectedListener listener;

    public VoucherSelectAdapter(List<Voucher> vouchers, OnVoucherSelectedListener listener) {
        this.vouchers = vouchers;
        this.listener = listener;
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        ItemVoucherSelectableBinding binding = ItemVoucherSelectableBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false
        );
        return new ViewHolder(binding);
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        holder.bind(vouchers.get(position), position == selectedPosition);
    }

    @Override
    public int getItemCount() {
        return vouchers != null ? vouchers.size() : 0;
    }

    public Voucher getSelectedVoucher() {
        if (selectedPosition >= 0 && selectedPosition < vouchers.size()) {
            return vouchers.get(selectedPosition);
        }
        return null;
    }

    public class ViewHolder extends RecyclerView.ViewHolder {
        private final ItemVoucherSelectableBinding binding;

        public ViewHolder(ItemVoucherSelectableBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        public void bind(Voucher voucher, boolean isSelected) {
            binding.tvVoucherTitle.setText(voucher.getTitle());
            binding.tvVoucherDesc.setText(voucher.getDescription());
            binding.tvVoucherExpiry.setText("HSD: " + DisplayFormatters.date(voucher.getExpiryDate()));
            binding.rbVoucherSelect.setChecked(isSelected);

            itemView.setOnClickListener(v -> {
                int prev = selectedPosition;
                selectedPosition = getAdapterPosition();
                notifyItemChanged(prev);
                notifyItemChanged(selectedPosition);
                if (listener != null) listener.onVoucherSelected(voucher);
            });
        }
    }
}
