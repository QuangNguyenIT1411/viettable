package com.restaurant.viettable.ui.food;

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
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentMenuBinding;
import com.restaurant.viettable.model.Category;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.session.DineinCartFragment;
import com.restaurant.viettable.ui.home.SearchFragment;
import com.restaurant.viettable.R;
import com.restaurant.viettable.ui.branch.ChangeBranchFragment;
import com.restaurant.viettable.util.PdMotion;

import java.util.ArrayList;
import java.util.List;

public class MenuFragment extends BaseFragment implements AppStateManager.StateChangeListener {

    private FragmentMenuBinding binding;
    private final List<FoodItem> displayedFoods = new ArrayList<>();
    private FoodAdapter adapter;
    private boolean renderingTabs;

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentMenuBinding.inflate(inflater, container, false);
        // The floating cart bar takes the place of the floating navigation bar here (the screen has its own back button).
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        appStateManager.addListener(this);

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnSearchMenu.setOnClickListener(v -> navigateTo(new SearchFragment()));
        binding.tvMenuBranch.setOnClickListener(v -> navigateTo(new ChangeBranchFragment()));
        binding.btnRetryMenuCatalog.setOnClickListener(v -> {
            if (appStateManager.getSelectedBranch() == null) navigateTo(new ChangeBranchFragment());
            else loadMenu(true);
        });

        if (appStateManager.getSelectedBranch() != null) {
            binding.tvMenuBranch.setText(appStateManager.getSelectedBranch().getName());
        }

        // Setup Category Tabs
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
                showCartAdded(food.getName());
            }

            @Override
            public void onToggleFavorite(FoodItem food) {
                persistFavorite(food);
            }
        }, R.layout.item_food_plate);

        binding.rvMenuFoods.setLayoutManager(new GridLayoutManager(getContext(), 2));
        PdMotion.staggerCards(binding.rvMenuFoods);
        binding.rvMenuFoods.setAdapter(adapter);

        binding.tabLayoutCategories.addOnTabSelectedListener(new TabLayout.OnTabSelectedListener() {
            @Override
            public void onTabSelected(TabLayout.Tab tab) {
                filterByCategory(tab.getPosition());
            }
            @Override public void onTabUnselected(TabLayout.Tab tab) {}
            @Override public void onTabReselected(TabLayout.Tab tab) {}
        });

        binding.cardCartBottomBar.setOnClickListener(v -> navigateTo(new DineinCartFragment()));

        updateCartBottomBar();

        renderMenu();
        loadMenu();
    }

    private void loadMenu() {
        loadMenu(false);
    }

    private void loadMenu(boolean retry) {
        if (binding != null && appStateManager.getSelectedBranch() != null) {
            MobileRepository.getInstance().loadMenu(appStateManager.getSelectedBranch().getId(), retry, new MobileRepository.Callback<Void>() {
                @Override public void onSuccess(Void value) {
                    if (binding == null) return;
                    renderMenu();
                    if (MobileRepository.getInstance().hasSession()) {
                        MobileRepository.getInstance().loadFavorites(new MobileRepository.Callback<Void>() {
                            @Override public void onSuccess(Void ignored) {}
                            @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
                        });
                    }
                }
                @Override public void onError(ApiException error) { if (binding != null) renderMenu(); }
            });
        }
    }

    private void renderMenu() {
        if (binding == null || adapter == null) return;
        binding.tvMenuBranch.setText(appStateManager.getSelectedBranch() == null
                ? getString(R.string.catalog_select_branch) : appStateManager.getSelectedBranch().getName());
        int position = Math.max(0, binding.tabLayoutCategories.getSelectedTabPosition());
        renderingTabs = true;
        binding.tabLayoutCategories.removeAllTabs();
        binding.tabLayoutCategories.addTab(binding.tabLayoutCategories.newTab().setText("Tất cả"));
        for (Category category : appStateManager.getCategories())
            binding.tabLayoutCategories.addTab(binding.tabLayoutCategories.newTab().setText(category.getName()));
        position = Math.min(position, appStateManager.getCategories().size());
        binding.tabLayoutCategories.selectTab(binding.tabLayoutCategories.getTabAt(position));
        renderingTabs = false;
        filterByCategory(position);
        boolean loading = appStateManager.isCatalogLoading();
        String error = appStateManager.getCatalogError();
        boolean empty = appStateManager.getFoodItems().isEmpty();
        binding.layoutMenuState.setVisibility(loading || error != null || empty ? View.VISIBLE : View.GONE);
        binding.progressMenuCatalog.setVisibility(loading ? View.VISIBLE : View.GONE);
        binding.tvMenuCatalogState.setText(loading ? getString(R.string.catalog_loading) : error != null ? error
                : appStateManager.getSelectedBranch() == null ? getString(R.string.catalog_select_branch)
                : getString(R.string.catalog_empty));
        binding.btnRetryMenuCatalog.setVisibility(!loading && (error != null || appStateManager.getSelectedBranch() == null)
                ? View.VISIBLE : View.GONE);
        binding.btnRetryMenuCatalog.setText(appStateManager.getSelectedBranch() == null
                ? R.string.catalog_select_branch : R.string.catalog_retry);
        binding.rvMenuFoods.setVisibility(empty ? View.GONE : View.VISIBLE);
    }

    private void filterByCategory(int position) {
        if (renderingTabs || adapter == null || position < 0 || position > appStateManager.getCategories().size()) return;
        displayedFoods.clear();
        if (position == 0) {
            displayedFoods.addAll(appStateManager.getFoodItems());
        } else {
            Category selectedCat = appStateManager.getCategories().get(position - 1);
            for (FoodItem fi : appStateManager.getFoodItems()) {
                if (fi.getCategoryId().equals(selectedCat.getId())) {
                    displayedFoods.add(fi);
                }
            }
        }
        adapter.notifyDataSetChanged();
    }

    private void updateCartBottomBar() {
        if (binding == null) return;
        int count = appStateManager.getCartItemCount();
        if (count > 0) {
            binding.cardCartBottomBar.setVisibility(View.VISIBLE);
            binding.tvCartBadgeCount.setText(String.valueOf(count));
            long total = appStateManager.calculateCartSubtotal();
            binding.tvCartBottomPrice.setText(String.format(java.util.Locale.GERMANY, "%,dđ", total));
        } else {
            binding.cardCartBottomBar.setVisibility(View.GONE);
        }
    }

    @Override
    public void onStateChanged() {
        if (isAdded()) {
            updateCartBottomBar();
            renderMenu();
        }
    }

    @Override
    public void onDestroyView() {
        appStateManager.removeListener(this);
        super.onDestroyView();
        binding = null;
    }
}
