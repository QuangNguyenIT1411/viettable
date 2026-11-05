package com.restaurant.viettable.ui.food;

import android.os.Bundle;
import android.text.TextUtils;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.core.widget.NestedScrollView;

import com.google.android.material.chip.Chip;
import com.restaurant.viettable.R;
import com.restaurant.viettable.databinding.FragmentFoodDetailBinding;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.util.HomeDisplayRules;
import com.restaurant.viettable.util.ImageLoader;
import com.restaurant.viettable.util.PdMotion;

import java.util.ArrayList;
import java.util.List;
import com.restaurant.viettable.util.SafeText;

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
        darkStatusIcons(true); // the yellow header needs dark clock and battery

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
            binding.tvFoodPrice.setText(HomeDisplayRules.exactShortPrice(foodItem.getPrice()));
            binding.tvFoodPrice.setContentDescription(foodItem.getFormattedPrice());
            // Stars come from real reviews of this dish (owner 05/11), filled by loadReviewSummary(); hidden until then.
            binding.layoutFoodRating.setVisibility(View.GONE);
            loadReviewSummary(foodItem.getId());
            binding.tvDescription.setText(SafeText.clean(foodItem.getDescription()));
            binding.tvDescription.setVisibility(SafeText.isBlank(foodItem.getDescription()) ? View.GONE : View.VISIBLE);
            binding.tvIngredients.setText("Thành phần: " + SafeText.clean(foodItem.getIngredients()));
            binding.tvIngredients.setVisibility(SafeText.isBlank(foodItem.getIngredients()) ? View.GONE : View.VISIBLE);
            binding.tvAllergy.setText("Dị ứng: " + SafeText.clean(foodItem.getAllergyInfo()));
            binding.tvAllergy.setVisibility(SafeText.isBlank(foodItem.getAllergyInfo()) ? View.GONE : View.VISIBLE);
            binding.tvNutrition.setText("Dinh dưỡng: " + SafeText.clean(foodItem.getNutrition()));
            binding.tvNutrition.setVisibility(SafeText.isBlank(foodItem.getNutrition()) ? View.GONE : View.VISIBLE);
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
                showCartAdded(quantity + " " + foodItem.getName());
                navigateBack();
            });

            updatePriceButton();
            compactAddButtonAtLargeFont();
            watchHeaderForStatusBar();
            PdMotion.dishEnter(binding.plateFoodHero, binding.tvFoodPrice);
            PdMotion.enterBlocks(binding.layoutFoodSheet, 6);
        }
    }

    /** At 130% font the label no longer fits next to the arrow circle (the design allows a centred label on a small button). */
    private void compactAddButtonAtLargeFont() {
        if (getResources().getConfiguration().fontScale > 1.15f) {
            binding.btnSubmitAddToCart.setIcon(null);
            binding.btnSubmitAddToCart.setGravity(android.view.Gravity.CENTER);
            binding.btnSubmitAddToCart.setPadding(0, 0, 0, 0);
        }
    }

    /** Status bar icons are dark over the yellow header and turn light once the dark sheet scrolls under the bar. */
    private void watchHeaderForStatusBar() {
        final boolean[] light = {false};
        binding.scrollFoodDetail.setOnScrollChangeListener((NestedScrollView.OnScrollChangeListener)
                (v, x, y, oldX, oldY) -> {
                    boolean wantLight = y > binding.layoutFoodHeader.getHeight()
                            - (int) (80 * getResources().getDisplayMetrics().density);
                    if (wantLight != light[0]) {
                        light[0] = wantLight;
                        darkStatusIcons(!wantLight);
                    }
                });
    }

    /**
     * The page colour decides the system icons (light on Tối, dark on Sáng); this screen is yellow on top in both, so it
     * asks for dark ones itself and gives the page's own choice back on leaving.
     */
    private void darkStatusIcons(boolean dark) {
        if (getActivity() == null) return;
        boolean night = (getResources().getConfiguration().uiMode
                & android.content.res.Configuration.UI_MODE_NIGHT_MASK) == android.content.res.Configuration.UI_MODE_NIGHT_YES;
        androidx.core.view.WindowCompat.getInsetsController(getActivity().getWindow(), getActivity().getWindow().getDecorView())
                .setAppearanceLightStatusBars(dark || !night);
    }

    /** Average, count and the newest comments of this dish from the server; "Chưa có đánh giá" when there are none. */
    private void loadReviewSummary(String dishId) {
        com.restaurant.viettable.data.MobileRepository.getInstance().loadDishReviewSummary(dishId,
                new com.restaurant.viettable.data.MobileRepository.Callback<org.json.JSONObject>() {
            @Override public void onSuccess(org.json.JSONObject summary) {
                if (binding == null) return;
                int count = summary.optInt("count");
                double average = summary.isNull("average") ? 0 : summary.optDouble("average", 0);
                binding.layoutDishReviewList.removeAllViews();
                if (count <= 0 || average <= 0) {
                    binding.tvDishReviewSummary.setText("Chưa có đánh giá");
                    binding.layoutFoodRating.setVisibility(View.GONE);
                    return;
                }
                String avg = String.valueOf(average).replace('.', ',');
                binding.tvDishReviewSummary.setText("★ " + avg + " / 5 · " + count + " lượt đánh giá");
                binding.tvFoodRating.setText(avg + " (" + count + ")");
                binding.layoutFoodRating.setVisibility(View.VISIBLE);
                org.json.JSONArray latest = summary.optJSONArray("latest");
                if (latest != null) for (int i = 0; i < latest.length(); i++) {
                    org.json.JSONObject row = latest.optJSONObject(i);
                    if (row == null) continue;
                    android.widget.TextView view = new android.widget.TextView(requireContext());
                    androidx.core.widget.TextViewCompat.setTextAppearance(view, R.style.TextAppearance_VietTable_Body);
                    String author = SafeText.json(row, "authorName", "Khách hàng");
                    String text = "★" + row.optInt("rating") + " · " + author + "\n" + SafeText.json(row, "comment");
                    String reply = SafeText.json(row, "replyText", null);
                    if (reply != null) text += "\nNhà hàng: " + reply;
                    view.setText(text);
                    int pad = Math.round(10 * getResources().getDisplayMetrics().density);
                    view.setPadding(0, pad, 0, 0);
                    binding.layoutDishReviewList.addView(view);
                }
            }
            @Override public void onError(com.restaurant.viettable.network.ApiException error) {
                if (binding == null) return;
                binding.tvDishReviewHeading.setVisibility(View.GONE);
                binding.layoutDishReviews.setVisibility(View.GONE);
            }
        });
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
        long total = foodItem.getPrice() * quantity; // server price; no surcharge added in the app
        binding.btnSubmitAddToCart.setText("Thêm · " + HomeDisplayRules.exactShortPrice(total));
    }

    @Override
    public void onDestroyView() {
        darkStatusIcons(false);
        super.onDestroyView();
        binding = null;
    }
}
