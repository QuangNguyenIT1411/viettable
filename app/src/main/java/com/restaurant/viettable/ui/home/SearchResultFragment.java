package com.restaurant.viettable.ui.home;

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
import com.restaurant.viettable.databinding.FragmentSearchResultBinding;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.food.FoodDetailFragment;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

public class SearchResultFragment extends BaseFragment {

    private static final String ARG_QUERY = "arg_query";
    private FragmentSearchResultBinding binding;
    private String query = "";
    private final List<FoodItem> searchResults = new ArrayList<>();
    private FoodAdapter adapter;

    public static SearchResultFragment newInstance(String query) {
        SearchResultFragment f = new SearchResultFragment();
        Bundle b = new Bundle();
        b.putString(ARG_QUERY, query);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            query = getArguments().getString(ARG_QUERY, "");
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentSearchResultBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);

        binding.btnBack.setOnClickListener(v -> navigateBack());
        binding.btnSearchAgain.setOnClickListener(v -> navigateBack());

        binding.tvSearchQueryTitle.setText("Kết quả: \"" + query + "\"");
        if (appStateManager.getSelectedBranch() != null) {
            binding.tvBranchContext.setText("Chi nhánh: " + appStateManager.getSelectedBranch().getName());
        }

        executeSearch();

        adapter = new FoodAdapter(searchResults, new FoodAdapter.OnFoodActionListener() {
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

        binding.rvSearchResults.setLayoutManager(new GridLayoutManager(getContext(), 2));
        binding.rvSearchResults.setAdapter(adapter);
        PdMotion.staggerCards(binding.rvSearchResults);

        binding.btnSortPrice.setOnClickListener(v -> {
            Collections.sort(searchResults, Comparator.comparingLong(FoodItem::getPrice));
            adapter.notifyDataSetChanged();
            showToast("Đã sắp xếp theo giá tăng dần");
        });

        binding.btnFilterRating.setOnClickListener(v -> {
            Collections.sort(searchResults, (a, b) -> Double.compare(b.getRating(), a.getRating()));
            adapter.notifyDataSetChanged();
            showToast("Đã sắp xếp theo đánh giá cao");
        });
    }

    private void executeSearch() {
        searchResults.clear();
        String q = query.toLowerCase().trim();
        for (FoodItem fi : appStateManager.getFoodItems()) {
            if (fi.getName().toLowerCase().contains(q) ||
                    fi.getDescription().toLowerCase().contains(q) ||
                    fi.getIngredients().toLowerCase().contains(q)) {
                searchResults.add(fi);
            }
        }

        binding.tvResultCount.setText(searchResults.size() + " món tìm thấy");
        if (searchResults.isEmpty()) {
            binding.layoutEmptyState.setVisibility(View.VISIBLE);
            binding.rvSearchResults.setVisibility(View.GONE);
        } else {
            binding.layoutEmptyState.setVisibility(View.GONE);
            binding.rvSearchResults.setVisibility(View.VISIBLE);
        }
    }

    @Override
    public void onDestroyView() {
        super.onDestroyView();
        binding = null;
    }
}
