package com.restaurant.viettable.ui.session;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.GridLayoutManager;

import com.google.android.material.tabs.TabLayout;
import com.restaurant.viettable.adapter.FoodAdapter;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.databinding.FragmentDineinMenuBinding;
import com.restaurant.viettable.model.Category;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.food.FoodDetailFragment;

import java.util.ArrayList;
import java.util.List;

public class DineinMenuFragment extends BaseFragment implements AppStateManager.StateChangeListener {

    private FragmentDineinMenuBinding binding;
    private final List<FoodItem> displayedFoods = new ArrayList<>();
    private FoodAdapter adapter;

    public static DineinMenuFragment newInstance() {
        return new DineinMenuFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentDineinMenuBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        appStateManager.addListener(this);

        binding.toolbar.setNavigationOnClickListener(v -> navigateBack());

        DiningSession session = appStateManager.getActiveDiningSession();
        if (session != null) {
            binding.tvTableContext.setText(session.getTableLabel() + " • " + session.getBranchName());
        }

        // Setup tabs
        binding.tabLayoutCategories.removeAllTabs();
        binding.tabLayoutCategories.addTab(binding.tabLayoutCategories.newTab().setText("Tất cả"));
        for (Category cat : appStateManager.getCategories()) {
            binding.tabLayoutCategories.addTab(binding.tabLayoutCategories.newTab().setText(cat.getName()));
        }

        displayedFoods.clear();
        displayedFoods.addAll(appStateManager.getFoodItems());

        adapter = new FoodAdapter(displayedFoods, new FoodAdapter.OnFoodActionListener() {
            @Override
            public void onFoodClicked(FoodItem food) {
                navigateTo(FoodDetailFragment.newInstance(food));
            }

            @Override
            public void onAddToCart(FoodItem food) {
                appStateManager.addToCart(food, 1, "Tiêu chuẩn", new ArrayList<>(), 0, "");
                showToast("Đã thêm " + food.getName() + " vào giỏ!");
                updateStickyCart();
            }

            @Override
            public void onToggleFavorite(FoodItem food) {
                persistFavorite(food);
            }
        });

        binding.rvDineinFoods.setLayoutManager(new GridLayoutManager(requireContext(), 2));
        binding.rvDineinFoods.setAdapter(adapter);

        binding.tabLayoutCategories.addOnTabSelectedListener(new TabLayout.OnTabSelectedListener() {
            @Override
            public void onTabSelected(TabLayout.Tab tab) {
                displayedFoods.clear();
                if (tab.getPosition() == 0) {
                    displayedFoods.addAll(appStateManager.getFoodItems());
                } else {
                    String catId = appStateManager.getCategories().get(tab.getPosition() - 1).getId();
                    for (FoodItem item : appStateManager.getFoodItems()) {
                        if (item.getCategoryId().equals(catId)) {
                            displayedFoods.add(item);
                        }
                    }
                }
                adapter.notifyDataSetChanged();
            }

            @Override
            public void onTabUnselected(TabLayout.Tab tab) {}

            @Override
            public void onTabReselected(TabLayout.Tab tab) {}
        });

        binding.btnViewDineinCart.setOnClickListener(v -> {
            navigateTo(DineinCartFragment.newInstance());
        });

        updateStickyCart();
    }

    private void updateStickyCart() {
        if (appStateManager.getCartItemCount() > 0) {
            binding.cardStickyCart.setVisibility(View.VISIBLE);
            binding.tvCartItemCount.setText(appStateManager.getCartItemCount() + " món mới đã chọn");
            binding.tvCartTotalPrice.setText(String.format(java.util.Locale.GERMANY, "%,dđ", appStateManager.calculateCartSubtotal()));
        } else {
            binding.cardStickyCart.setVisibility(View.GONE);
        }
    }

    @Override
    public void onStateChanged() {
        if (binding != null) {
            updateStickyCart();
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        appStateManager.removeListener(this);
        binding = null;
    }
}
