package com.restaurant.viettable.ui.food;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentFoodDetailBinding;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.ImageLoader;

import java.util.ArrayList;
import java.util.List;

public class FoodDetailFragment extends BaseFragment {

    private static final String ARG_FOOD = "arg_food";
    private FragmentFoodDetailBinding binding;
    private FoodItem foodItem;
    private int quantity = 1;
    private String selectedSize = "Tiêu chuẩn";
    private final List<String> selectedToppings = new ArrayList<>();
    private int selectedSpicy = 0;

    public static FoodDetailFragment newInstance(FoodItem food) {
        FoodDetailFragment f = new FoodDetailFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_FOOD, food);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            foodItem = (FoodItem) getArguments().getSerializable(ARG_FOOD);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentFoodDetailBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    protected void onApplyInsets(@NonNull View root, androidx.core.graphics.Insets statusBars, androidx.core.graphics.Insets navigationBars) {
        super.onApplyInsets(root, statusBars, navigationBars);
        setStatusBarLightIcons(false); // Dark icons over warm light food hero

        int baseMargin = (int) (16 * getResources().getDisplayMetrics().density);
        ViewGroup.MarginLayoutParams backLp = (ViewGroup.MarginLayoutParams) binding.btnBack.getLayoutParams();
        if (backLp != null) {
            backLp.topMargin = statusBars.top + baseMargin;
            binding.btnBack.setLayoutParams(backLp);
        }

        ViewGroup.MarginLayoutParams favLp = (ViewGroup.MarginLayoutParams) binding.btnFavorite.getLayoutParams();
        if (favLp != null) {
            favLp.topMargin = statusBars.top + baseMargin;
            binding.btnFavorite.setLayoutParams(favLp);
        }

        binding.bottomBarAddToCart.setPadding(
                binding.bottomBarAddToCart.getPaddingLeft(),
                binding.bottomBarAddToCart.getPaddingTop(),
                binding.bottomBarAddToCart.getPaddingRight(),
                navigationBars.bottom + (int) (12 * getResources().getDisplayMetrics().density)
        );
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        if (foodItem == null) {
            showToast("Không tìm thấy thông tin món ăn.");
            navigateBack();
            return;
        }

        if (foodItem != null) {
            ImageLoader.getInstance().load(foodItem, binding.ivFoodHero);
            binding.tvFoodName.setText(foodItem.getName());
            binding.tvFoodPrice.setText(foodItem.getFormattedPrice());
            binding.tvFoodRating.setText(foodItem.getRating() + " (" + foodItem.getSoldCount() + " đã bán)");
            binding.layoutFoodRating.setVisibility(foodItem.getRating() > 0 ? View.VISIBLE : View.GONE);
            binding.tvDescription.setText(foodItem.getDescription());
            binding.tvDescription.setVisibility(TextUtils.isEmpty(foodItem.getDescription()) ? View.GONE : View.VISIBLE);
            binding.tvIngredients.setText("Thành phần: " + foodItem.getIngredients());
            binding.tvIngredients.setVisibility(TextUtils.isEmpty(foodItem.getIngredients()) ? View.GONE : View.VISIBLE);
            binding.tvAllergy.setText("Dị ứng: " + foodItem.getAllergyInfo());
            binding.tvAllergy.setVisibility(TextUtils.isEmpty(foodItem.getAllergyInfo()) ? View.GONE : View.VISIBLE);
            binding.tvNutrition.setText("Dinh dưỡng: " + foodItem.getNutrition());
            binding.tvNutrition.setVisibility(TextUtils.isEmpty(foodItem.getNutrition()) ? View.GONE : View.VISIBLE);
            boolean hasDietaryInfo = binding.tvIngredients.getVisibility() == View.VISIBLE
                    || binding.tvAllergy.getVisibility() == View.VISIBLE
                    || binding.tvNutrition.getVisibility() == View.VISIBLE;
            binding.layoutDietaryInfo.setVisibility(hasDietaryInfo ? View.VISIBLE : View.GONE);

            updateFavoriteIcon();
            binding.btnFavorite.setOnClickListener(v -> {
                foodItem.setFavorite(!foodItem.isFavorite());
                updateFavoriteIcon();
                persistFavorite(foodItem);
            });

            // Populate Sizes
            binding.chipGroupSizes.removeAllViews();
            if (foodItem.getAvailableSizes() != null) {
                for (int i = 0; i < foodItem.getAvailableSizes().size(); i++) {
                    String size = foodItem.getAvailableSizes().get(i);
                    Chip chip = new Chip(getContext());
                    chip.setText(size);
                    chip.setCheckable(true);
                    if (i == 0) {
                        chip.setChecked(true);
                        selectedSize = size;
                    }
                    chip.setOnCheckedChangeListener((btn, isChecked) -> {
                        if (isChecked) {
                            selectedSize = size;
                            updatePriceButton();
                        }
                    });
                    binding.chipGroupSizes.addView(chip);
                }
            }
            boolean hasSizes = binding.chipGroupSizes.getChildCount() > 0;
            binding.tvSizeHeading.setVisibility(hasSizes ? View.VISIBLE : View.GONE);
            binding.chipGroupSizes.setVisibility(hasSizes ? View.VISIBLE : View.GONE);

            // Populate Toppings
            binding.chipGroupToppings.removeAllViews();
            if (foodItem.getAvailableToppings() != null) {
                for (String topping : foodItem.getAvailableToppings()) {
                    Chip chip = new Chip(getContext());
                    chip.setText(topping);
                    chip.setCheckable(true);
                    chip.setOnCheckedChangeListener((btn, isChecked) -> {
                        if (isChecked) {
                            selectedToppings.add(topping);
                        } else {
                            selectedToppings.remove(topping);
                        }
                        updatePriceButton();
                    });
                    binding.chipGroupToppings.addView(chip);
                }
            }
            boolean hasToppings = binding.chipGroupToppings.getChildCount() > 0;
            binding.tvToppingHeading.setVisibility(hasToppings ? View.VISIBLE : View.GONE);
            binding.chipGroupToppings.setVisibility(hasToppings ? View.VISIBLE : View.GONE);

            // Spicy levels
            binding.chipGroupSpicy.removeAllViews();
            String[] spicyLabels = {"Không cay", "Cay nhẹ", "Vừa", "Cay nồng"};
            for (int i = 0; i < spicyLabels.length; i++) {
                final int lvl = i;
                Chip chip = new Chip(getContext());
                chip.setText(spicyLabels[i]);
                chip.setCheckable(true);
                if (i == foodItem.getSpicyLevel()) chip.setChecked(true);
                chip.setOnCheckedChangeListener((btn, isChecked) -> {
                    if (isChecked) selectedSpicy = lvl;
                });
                binding.chipGroupSpicy.addView(chip);
            }

            // Quantity
            binding.btnMinus.setOnClickListener(v -> {
                if (quantity > 1) {
                    quantity--;
                    binding.tvQuantity.setText(String.valueOf(quantity));
                    updatePriceButton();
                }
            });

            binding.btnPlus.setOnClickListener(v -> {
                quantity++;
                binding.tvQuantity.setText(String.valueOf(quantity));
                updatePriceButton();
            });

            binding.btnSubmitAddToCart.setOnClickListener(v -> {
                String note = binding.etNote.getText() != null ? binding.etNote.getText().toString().trim() : "";
                appStateManager.addToCart(foodItem, quantity, selectedSize, selectedToppings, selectedSpicy, note);
                showToast("Đã thêm " + quantity + " " + foodItem.getName() + " vào giỏ!");
                navigateBack();
            });

            updatePriceButton();
        }
    }

    private void updateFavoriteIcon() {
        if (foodItem != null && foodItem.isFavorite()) {
            binding.btnFavorite.setImageResource(R.drawable.ic_favorite);
        } else {
            binding.btnFavorite.setImageResource(R.drawable.ic_favorite_border);
        }
        binding.btnFavorite.setContentDescription(getString(
                foodItem != null && foodItem.isFavorite()
                        ? R.string.a11y_remove_favorite : R.string.a11y_add_favorite));
    }

    private void updatePriceButton() {
        if (foodItem == null) return;
        long unit = foodItem.getPrice();
        if ("Lớn".equalsIgnoreCase(selectedSize) || selectedSize.contains("15k")) unit += 15000;
        if ("Đặc biệt".equalsIgnoreCase(selectedSize) || selectedSize.contains("25k") || selectedSize.contains("20k")) unit += 25000;
        unit += (selectedToppings.size() * 15000L);
        long total = unit * quantity;
        binding.btnSubmitAddToCart.setText("Thêm vào giỏ • " + String.format(java.util.Locale.GERMANY, "%,dđ", total));
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
