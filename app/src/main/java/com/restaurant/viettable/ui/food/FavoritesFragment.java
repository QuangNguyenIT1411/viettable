package com.restaurant.viettable.ui.food;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import com.restaurant.viettable.ui.common.ListState;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.GridLayoutManager;
import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.FoodAdapter;
import com.restaurant.viettable.util.PdMotion;
import com.restaurant.viettable.databinding.FragmentFavoritesBinding;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;

import java.util.ArrayList;
import java.util.List;

public class FavoritesFragment extends BaseFragment {

    private FragmentFavoritesBinding binding;
    private final List<FoodItem> favFoods = new ArrayList<>();
    private FoodAdapter adapter;

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

        binding.tabLayoutFavorites.setVisibility(View.GONE);
        binding.rvFavorites.setLayoutManager(new GridLayoutManager(getContext(), 2));
        adapter = new FoodAdapter(favFoods, new FoodAdapter.OnFoodActionListener() {
            @Override public void onFoodClicked(FoodItem food) {
                navigateTo(FoodDetailFragment.newInstance(food));
            }
            @Override public void onAddToCart(FoodItem food) {
                appStateManager.addToCart(food, 1, "Tiêu chuẩn", new ArrayList<>(), 0, "");
                showCartAdded(food.getName());
            }
            @Override public void onToggleFavorite(FoodItem food) {
                persistFavorite(food);
                if (!food.isFavorite()) showFavoriteFoods();
            }
        }, R.layout.item_food_plate);
        binding.rvFavorites.setAdapter(adapter);
        PdMotion.staggerCards(binding.rvFavorites);
        binding.btnRetryFavorites.setOnClickListener(v -> loadFavorites());
        showFavoriteFoods();
        loadFavorites();
    }

    private void loadFavorites() {
        if (!MobileRepository.getInstance().hasSession()) {
            renderState(false, null);
            return;
        }
        renderState(true, null);
        MobileRepository.getInstance().loadFavorites(new MobileRepository.Callback<Void>() {
            @Override public void onSuccess(Void value) { if (binding != null) showFavoriteFoods(); }
            @Override public void onError(ApiException error) {
                if (binding != null) renderState(false, apiError(error));
            }
        });
    }

    private void showFavoriteFoods() {
        favFoods.clear();
        for (FoodItem fi : appStateManager.getFoodItems()) {
            if (fi.isFavorite()) favFoods.add(fi);
        }
        adapter.notifyDataSetChanged();
        renderState(false, null);
    }

    private void renderState(boolean loading, @Nullable String error) {
        if (binding == null) return;
        boolean hasContent = !favFoods.isEmpty();
        ListState.render(binding.rvFavorites, binding.progressFavorites, binding.tvFavoriteState,
                binding.btnRetryFavorites, hasContent, loading, error,
                getString(com.restaurant.viettable.R.string.state_no_favorites));
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
