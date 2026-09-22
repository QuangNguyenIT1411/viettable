package com.restaurant.viettable.adapter;

import android.view.LayoutInflater;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.ItemFoodCardBinding;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.util.ImageLoader;

import java.util.List;

public class FoodAdapter extends RecyclerView.Adapter<FoodAdapter.FoodViewHolder> {

    public interface OnFoodActionListener {
        void onFoodClicked(FoodItem food);
        void onAddToCart(FoodItem food);
        void onToggleFavorite(FoodItem food);
    }

    private final List<FoodItem> foodList;
    private final OnFoodActionListener listener;

    public FoodAdapter(List<FoodItem> foodList, OnFoodActionListener listener) {
        this.foodList = foodList;
        this.listener = listener;
    }

    @NonNull
    @Override
    public FoodViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        ItemFoodCardBinding binding = ItemFoodCardBinding.inflate(
                LayoutInflater.from(parent.getContext()), parent, false
        );
        return new FoodViewHolder(binding);
    }

    @Override
    public void onBindViewHolder(@NonNull FoodViewHolder holder, int position) {
        holder.bind(foodList.get(position), listener);
    }

    @Override
    public int getItemCount() {
        return foodList != null ? foodList.size() : 0;
    }

    public static class FoodViewHolder extends RecyclerView.ViewHolder {
        private final ItemFoodCardBinding binding;

        public FoodViewHolder(ItemFoodCardBinding binding) {
            super(binding.getRoot());
            this.binding = binding;
        }

        public void bind(FoodItem item, OnFoodActionListener listener) {
            ImageLoader.getInstance().load(item, binding.ivFoodImage);
            binding.tvFoodName.setText(item.getName());
            binding.tvFoodPrice.setText(item.getFormattedPrice());
            binding.tvFoodRating.setText(String.valueOf(item.getRating()));

            if (item.isFavorite()) {
                binding.btnFavorite.setImageResource(R.drawable.ic_favorite);
            } else {
                binding.btnFavorite.setImageResource(R.drawable.ic_favorite_border);
            }

            binding.cardFood.setOnClickListener(v -> {
                if (listener != null) listener.onFoodClicked(item);
            });

            binding.btnAddFood.setOnClickListener(v -> {
                if (listener != null) listener.onAddToCart(item);
            });

            binding.btnFavorite.setOnClickListener(v -> {
                item.setFavorite(!item.isFavorite());
                if (item.isFavorite()) {
                    binding.btnFavorite.setImageResource(R.drawable.ic_favorite);
                } else {
                    binding.btnFavorite.setImageResource(R.drawable.ic_favorite_border);
                }
                if (listener != null) listener.onToggleFavorite(item);
            });
        }
    }
}
