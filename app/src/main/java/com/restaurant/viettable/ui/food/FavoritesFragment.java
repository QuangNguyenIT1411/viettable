package com.restaurant.viettable.ui.food;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.GridLayoutManager;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.google.android.material.tabs.TabLayout;
import com.restaurant.viettable.adapter.BranchAdapter;
import com.restaurant.viettable.adapter.FoodAdapter;
import com.restaurant.viettable.databinding.FragmentFavoritesBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.Branch;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.branch.BranchDetailFragment;

import java.util.ArrayList;
import java.util.List;

public class FavoritesFragment extends BaseFragment {

    private FragmentFavoritesBinding binding;
    private final List<FoodItem> favFoods = new ArrayList<>();
    private final List<Branch> favBranches = new ArrayList<>();

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentFavoritesBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        binding.tabLayoutFavorites.addTab(binding.tabLayoutFavorites.newTab().setText("Món yêu thích"));
        binding.tabLayoutFavorites.addTab(binding.tabLayoutFavorites.newTab().setText("Chi nhánh yêu thích"));

        showFavoriteFoods();

        if (MobileRepository.getInstance().hasSession()) {
            MobileRepository.getInstance().loadFavorites(new MobileRepository.Callback<Void>() {
                @Override public void onSuccess(Void value) { if (binding != null) showFavoriteFoods(); }
                @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
            });
        }

        binding.tabLayoutFavorites.addOnTabSelectedListener(new TabLayout.OnTabSelectedListener() {
            @Override
            public void onTabSelected(TabLayout.Tab tab) {
                if (tab.getPosition() == 0) {
                    showFavoriteFoods();
                } else {
                    showFavoriteBranches();
                }
            }
            @Override public void onTabUnselected(TabLayout.Tab tab) {}
            @Override public void onTabReselected(TabLayout.Tab tab) {}
        });
    }

    private void showFavoriteFoods() {
        favFoods.clear();
        for (FoodItem fi : appStateManager.getFoodItems()) {
            if (fi.isFavorite()) favFoods.add(fi);
        }
        binding.rvFavorites.setLayoutManager(new GridLayoutManager(getContext(), 2));
        binding.rvFavorites.setAdapter(new FoodAdapter(favFoods, new FoodAdapter.OnFoodActionListener() {
            @Override
            public void onFoodClicked(FoodItem food) {
                navigateTo(FoodDetailFragment.newInstance(food));
            }

            @Override
            public void onAddToCart(FoodItem food) {
                appStateManager.addToCart(food, 1, "Tiêu chuẩn", new ArrayList<>(), 0, "");
                showToast("Đã thêm " + food.getName() + " vào giỏ!");
            }

            @Override
            public void onToggleFavorite(FoodItem food) {
                persistFavorite(food);
                if (!food.isFavorite()) showFavoriteFoods();
            }
        }));
    }

    private void showFavoriteBranches() {
        favBranches.clear();

        binding.rvFavorites.setLayoutManager(new LinearLayoutManager(getContext()));
        binding.rvFavorites.setAdapter(new BranchAdapter(favBranches, new BranchAdapter.OnBranchActionListener() {
            @Override
            public void onSelectBranch(Branch branch) {
                appStateManager.setSelectedBranch(branch);
                showToast("Đã chọn chi nhánh: " + branch.getName());
                navigateBack();
            }

            @Override
            public void onViewDetail(Branch branch) {
                navigateTo(BranchDetailFragment.newInstance(branch));
            }
        }));
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
