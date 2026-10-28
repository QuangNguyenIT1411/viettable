package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.databinding.ItemVoucherCardBinding;
import com.restaurant.viettable.model.Voucher;
import com.restaurant.viettable.util.DisplayFormatters;

import java.util.List;

public class VoucherCardAdapter extends RecyclerView.Adapter<VoucherCardAdapter.ViewHolder> {

    public interface OnVoucherActionListener {
        void onUseVoucher(Voucher voucher);
    }

    private final List<Voucher> vouchers;
    private final OnVoucherActionListener listener;

    public VoucherCardAdapter(List<Voucher> vouchers, OnVoucherActionListener listener) {
        this.vouchers = vouchers;
        this.listener = listener;
    }

    @NonNull
    @Override
    public ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        ItemVoucherCardBinding binding = ItemVoucherCardBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false
        );
        return new ViewHolder(binding);
    }

    @Override
    public void onBindViewHolder(@NonNull ViewHolder holder, int position) {
        holder.bind(vouchers.get(position), listener);
    }

    @Override
    public int getItemCount() {
        return vouchers != null ? vouchers.size() : 0;
    }

    public static class ViewHolder extends RecyclerView.ViewHolder {
        private final ItemVoucherCardBinding binding;

        public ViewHolder(ItemVoucherCardBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        public void bind(Voucher voucher, OnVoucherActionListener listener) {
            binding.tvCardVoucherTitle.setText(voucher.getTitle());
            String status = com.restaurant.viettable.util.BillDiscountText.voucherStatus(voucher);
            String description = com.restaurant.viettable.util.BillDiscountText.voucherDescription(voucher);
            binding.tvCardVoucherDesc.setText(status == null ? description : description + "\n" + status);
            itemView.setAlpha(voucher.isUsable() ? 1f : 0.6f);
            binding.tvCardVoucherCode.setText("Mã: " + voucher.getCode());
            binding.tvCardVoucherExpiry.setText("HSD: " + DisplayFormatters.date(voucher.getExpiryDate()));

            binding.btnUseVoucherNow.setOnClickListener(v -> {
                if (listener != null) listener.onUseVoucher(voucher);
            });
            binding.btnUseVoucherNow.setVisibility(listener == null ? View.GONE : View.VISIBLE);
        }
    }
}
