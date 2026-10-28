package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemVoucherSelectableBinding;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.List;
import com.restaurant.viettable.util.BillDiscountText;

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

    /** Pre-selects the voucher already on the bill. */
    public void select(Voucher voucher) {
        int index = vouchers == null ? -1 : vouchers.indexOf(voucher);
        int prev = selectedPosition;
        selectedPosition = index;
        if (prev >= 0) notifyItemChanged(prev);
        if (index >= 0) notifyItemChanged(index);
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
            binding.tvVoucherDesc.setText(BillDiscountText.voucherDescription(voucher));
            binding.tvVoucherExpiry.setText("HSD: " + DisplayFormatters.date(voucher.getExpiryDate()));
            String status = BillDiscountText.voucherStatus(voucher);
            binding.tvVoucherStatus.setVisibility(status == null ? android.view.View.GONE : android.view.View.VISIBLE);
            binding.tvVoucherStatus.setText(status == null ? "" : status);
            binding.tvVoucherStatus.setTextColor(androidx.core.content.ContextCompat.getColor(itemView.getContext(),
                    voucher.isUsable() ? R.color.pd_mint : R.color.pd_muted));
            // A voucher that cannot be used stays listed, greyed, with its reason (owner 28/10); it cannot be picked.
            itemView.setAlpha(voucher.isUsable() ? 1f : 0.5f);
            binding.rbVoucherSelect.setVisibility(voucher.isUsable() ? android.view.View.VISIBLE : android.view.View.INVISIBLE);
            binding.rbVoucherSelect.setChecked(isSelected);
            // Selected offer gets the yellow 2dp border (DESIGN_PHO_DEM.md section 6 "Chọn phương thức thanh toán").
            binding.cardVoucherSelectable.setStrokeColor(androidx.core.content.ContextCompat.getColor(itemView.getContext(),
                    isSelected ? R.color.pd_yellow : R.color.transparent));

            itemView.setOnClickListener(v -> {
                if (!voucher.isUsable()) return;
                int prev = selectedPosition;
                selectedPosition = getAdapterPosition();
                notifyItemChanged(prev);
                notifyItemChanged(selectedPosition);
                if (listener != null) listener.onVoucherSelected(voucher);
            });
        }
    }
}
