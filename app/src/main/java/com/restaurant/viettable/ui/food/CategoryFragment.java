package com.restaurant.viettable.ui.food;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.GridLayoutManager;

import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.FoodAdapter;
import com.restaurant.viettable.util.PdMotion;
import com.restaurant.viettable.databinding.FragmentCategoryBinding;
import com.restaurant.viettable.model.Category;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.List;

public class CategoryFragment extends BaseFragment {

    private static final String ARG_CAT = "arg_cat";
    private FragmentCategoryBinding binding;
    private Category category;
    private final List<FoodItem> categoryFoods = new ArrayList<>();
    private FoodAdapter adapter;

    public static CategoryFragment newInstance(Category category) {
        CategoryFragment f = new CategoryFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_CAT, category);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            category = (Category) getArguments().getSerializable(ARG_CAT);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentCategoryBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        if (category != null) {
            binding.tvCategoryTitle.setText(category.getName());
            categoryFoods.clear();
            for (FoodItem fi : appStateManager.getFoodItems()) {
                if (fi.getCategoryId().equals(category.getId())) {
                    categoryFoods.add(fi);
                }
            }
        }

        adapter = new FoodAdapter(categoryFoods, new FoodAdapter.OnFoodActionListener() {
            @Override
            public void onFoodClicked(FoodItem food) {
                navigateTo(FoodDetailFragment.newInstance(food));
            }

            @Override
            public void onAddToCart(FoodItem food) {
                appStateManager.addToCart(food, 1, "Tiêu chuẩn", new ArrayList<>(), 0, "");
                showCartAdded(food.getName());
            }

            @Override
            public void onToggleFavorite(FoodItem food) {
                persistFavorite(food);
            }
        }, R.layout.item_food_plate);

        binding.rvCategoryFoods.setLayoutManager(new GridLayoutManager(getContext(), 2));
        binding.rvCategoryFoods.setAdapter(adapter);
        PdMotion.staggerCards(binding.rvCategoryFoods);
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
