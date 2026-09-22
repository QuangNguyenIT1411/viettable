package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.databinding.ItemCartEntryBinding;
import com.restaurant.viettable.model.CartItem;

import java.util.List;

public class CartAdapter extends RecyclerView.Adapter<CartAdapter.CartViewHolder> {

    public interface OnCartItemActionListener {
        void onQuantityChanged(CartItem item, int newQuantity);
        void onRemoveItem(CartItem item);
    }

    private final List<CartItem> cartList;
    private final OnCartItemActionListener listener;

    public CartAdapter(List<CartItem> cartList, OnCartItemActionListener listener) {
        this.cartList = cartList;
        this.listener = listener;
    }

    @NonNull
    @Override
    public CartViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        ItemCartEntryBinding binding = ItemCartEntryBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false
        );
        return new CartViewHolder(binding);
    }

    @Override
    public void onBindViewHolder(@NonNull CartViewHolder holder, int position) {
        holder.bind(cartList.get(position), listener);
    }

    @Override
    public int getItemCount() {
        return cartList != null ? cartList.size() : 0;
    }

    public static class CartViewHolder extends RecyclerView.ViewHolder {
        private final ItemCartEntryBinding binding;

        public CartViewHolder(ItemCartEntryBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        public void bind(CartItem item, OnCartItemActionListener listener) {
            binding.tvCartFoodName.setText(item.getFoodItem().getName());
            binding.tvCartItemOptions.setText(item.getOptionsSummary());
            binding.tvCartQuantity.setText(String.valueOf(item.getQuantity()));
            binding.tvCartItemTotal.setText(item.getFormattedTotalPrice());

            binding.btnPlusCart.setOnClickListener(v -> {
                int newQ = item.getQuantity() + 1;
                item.setQuantity(newQ);
                binding.tvCartQuantity.setText(String.valueOf(newQ));
                binding.tvCartItemTotal.setText(item.getFormattedTotalPrice());
                if (listener != null) listener.onQuantityChanged(item, newQ);
            });

            binding.btnMinusCart.setOnClickListener(v -> {
                if (item.getQuantity() > 1) {
                    int newQ = item.getQuantity() - 1;
                    item.setQuantity(newQ);
                    binding.tvCartQuantity.setText(String.valueOf(newQ));
                    binding.tvCartItemTotal.setText(item.getFormattedTotalPrice());
                    if (listener != null) listener.onQuantityChanged(item, newQ);
                } else {
                    if (listener != null) listener.onRemoveItem(item);
                }
            });

            binding.btnRemoveItem.setOnClickListener(v -> {
                if (listener != null) listener.onRemoveItem(item);
            });
        }
    }
}
