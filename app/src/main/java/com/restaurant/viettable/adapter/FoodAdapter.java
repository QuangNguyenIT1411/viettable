package com.restaurant.viettable.adapter;

import android.content.res.ColorStateList;
import android.graphics.ColorMatrix;
import android.graphics.ColorMatrixColorFilter;
import android.view.HapticFeedbackConstants;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageView;
import android.widget.TextView;

import androidx.annotation.LayoutRes;
import androidx.annotation.NonNull;
import androidx.core.content.ContextCompat;
import androidx.recyclerview.widget.GridLayoutManager;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.google.android.material.card.MaterialCardView;
import com.restaurant.viettable.R;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.util.HomeDisplayRules;
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
    @LayoutRes private final int layout;
    private boolean inGrid;

    public FoodAdapter(List<FoodItem> foodList, OnFoodActionListener listener) {
        this(foodList, listener, R.layout.item_food_card);
    }

    /** {@code layout} is item_food_card (photo card) or item_food_plate ("Phố đêm" plate card); both share view IDs. */
    public FoodAdapter(List<FoodItem> foodList, OnFoodActionListener listener, @LayoutRes int layout) {
        this.foodList = foodList;
        this.listener = listener;
        this.layout = layout;
    }

    @NonNull
    @Override
    public FoodViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        View view = LayoutInflater.from(parent.getContext()).inflate(layout, parent, false);
        RecyclerView.LayoutManager manager = parent instanceof RecyclerView ? ((RecyclerView) parent).getLayoutManager() : null;
        inGrid = manager instanceof GridLayoutManager;
        // Plate cards fill a grid cell; in a horizontal row they need a fixed width.
        if (layout == R.layout.item_food_plate && !inGrid && manager instanceof LinearLayoutManager
                && ((LinearLayoutManager) manager).getOrientation() == RecyclerView.HORIZONTAL) {
            view.getLayoutParams().width = parent.getResources().getDimensionPixelSize(R.dimen.pd_food_card_row_width);
        }
        return new FoodViewHolder(view);
    }

    @Override
    public void onBindViewHolder(@NonNull FoodViewHolder holder, int position) {
        holder.bind(foodList.get(position), listener);
        if (layout == R.layout.item_food_plate) holder.tintPlateCard(position, inGrid);
    }

    @Override
    public int getItemCount() {
        return foodList != null ? foodList.size() : 0;
    }

    public static class FoodViewHolder extends RecyclerView.ViewHolder {
        private static final ColorMatrixColorFilter GREY;
        static {
            ColorMatrix matrix = new ColorMatrix();
            matrix.setSaturation(0f);
            GREY = new ColorMatrixColorFilter(matrix);
        }

        private final MaterialCardView cardFood;
        private final ImageView ivFoodImage;
        private final TextView tvFoodName;
        private final TextView tvFoodPrice;
        private final TextView tvFoodRating;
        private final View layoutFoodRating;
        private final ImageView btnFavorite;
        private final View btnAddFood;
        private final TextView tvFoodSoldOut; // plate card only
        private final boolean plate;

        public FoodViewHolder(View root) {
            super(root);
            cardFood = root.findViewById(R.id.cardFood);
            ivFoodImage = root.findViewById(R.id.ivFoodImage);
            tvFoodName = root.findViewById(R.id.tvFoodName);
            tvFoodPrice = root.findViewById(R.id.tvFoodPrice);
            tvFoodRating = root.findViewById(R.id.tvFoodRating);
            layoutFoodRating = root.findViewById(R.id.layoutFoodRating);
            btnFavorite = root.findViewById(R.id.btnFavorite);
            btnAddFood = root.findViewById(R.id.btnAddFood);
            tvFoodSoldOut = root.findViewById(R.id.tvFoodSoldOut);
            plate = tvFoodSoldOut != null;
        }

        public void bind(FoodItem item, OnFoodActionListener listener) {
            ImageLoader.getInstance().load(item, ivFoodImage);
            // The loader puts a square backdrop behind the photo; a plate needs a round one.
            if (plate) ivFoodImage.setBackgroundResource(R.drawable.pd_plate_empty);
            tvFoodName.setText(item.getName());
            tvFoodPrice.setText(plate ? HomeDisplayRules.shortPrice(item.getPrice()) : item.getFormattedPrice());
            tvFoodRating.setText(String.valueOf(item.getRating()));
            layoutFoodRating.setVisibility(item.getRating() > 0 ? View.VISIBLE : View.GONE);

            if (plate) {
                // Sold out: grey plate, "Hết món" and a disabled "+" (DESIGN_PHO_DEM.md section 5).
                boolean available = item.isAvailable();
                tvFoodSoldOut.setVisibility(available ? View.GONE : View.VISIBLE);
                ivFoodImage.setColorFilter(available ? null : GREY);
                btnAddFood.setEnabled(available);
                btnAddFood.setAlpha(available ? 1f : 0.4f);
                btnAddFood.setContentDescription(itemView.getContext().getString(R.string.pd_add_to_cart) + " " + item.getName());
            }

            if (item.isFavorite()) {
                btnFavorite.setImageResource(R.drawable.ic_favorite);
            } else {
                btnFavorite.setImageResource(R.drawable.ic_favorite_border);
            }
            updateFavoriteDescription(item);

            cardFood.setOnClickListener(v -> {
                if (listener != null) listener.onFoodClicked(item);
            });

            btnAddFood.setOnClickListener(v -> {
                if (plate) bounce(v);
                if (listener != null) listener.onAddToCart(item);
            });

            btnFavorite.setOnClickListener(v -> {
                item.setFavorite(!item.isFavorite());
                if (item.isFavorite()) {
                    btnFavorite.setImageResource(R.drawable.ic_favorite);
                } else {
                    btnFavorite.setImageResource(R.drawable.ic_favorite_border);
                }
                updateFavoriteDescription(item);
                if (listener != null) listener.onToggleFavorite(item);
            });
        }

        /** Alternate surface and plum like the design's 2-column grid (checkerboard), or every other card in a row. */
        void tintPlateCard(int position, boolean grid) {
            boolean plum = grid ? (position % 4 == 1 || position % 4 == 2) : position % 2 == 1;
            cardFood.setCardBackgroundColor(ColorStateList.valueOf(ContextCompat.getColor(itemView.getContext(),
                    plum ? R.color.pd_plum : R.color.pd_surface)));
        }

        /** "+" pops 1 -> 1.15 -> 1 with a light haptic tick (section 7). */
        private static void bounce(View v) {
            v.performHapticFeedback(HapticFeedbackConstants.KEYBOARD_TAP);
            v.animate().cancel();
            v.animate().scaleX(1.15f).scaleY(1.15f).setDuration(110).withEndAction(() ->
                    v.animate().scaleX(1f).scaleY(1f).setDuration(140).start()).start();
        }

        private void updateFavoriteDescription(FoodItem item) {
            btnFavorite.setContentDescription(itemView.getContext().getString(
                    item.isFavorite() ? R.string.a11y_remove_favorite : R.string.a11y_add_favorite));
        }
    }
}
