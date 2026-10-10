package com.restaurant.viettable.ui.preorder;

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
import com.restaurant.viettable.databinding.FragmentPreorderMenuBinding;
import com.restaurant.viettable.model.Category;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.model.Reservation;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.food.FoodDetailFragment;

import java.util.ArrayList;
import java.util.List;

public class PreorderMenuFragment extends BaseFragment implements AppStateManager.StateChangeListener {

    private static final String ARG_RES = "arg_res";
    private FragmentPreorderMenuBinding binding;
    private Reservation reservation;
    private final List<FoodItem> displayedFoods = new ArrayList<>();
    private FoodAdapter adapter;

    public static PreorderMenuFragment newInstance(Reservation res) {
        PreorderMenuFragment f = new PreorderMenuFragment();
        Bundle b = new Bundle();
        b.putSerializable(ARG_RES, res);
        f.setArguments(b);
        return f;
    }

    @Override
    public void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (getArguments() != null) {
            reservation = (Reservation) getArguments().getSerializable(ARG_RES);
        }
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentPreorderMenuBinding.inflate(inflater, container, false);
        setBottomNavVisible(false);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        appStateManager.addListener(this);

        binding.btnBack.setOnClickListener(v -> navigateBack());

        if (reservation != null) {
            binding.tvPreorderResSummary.setText("Lịch đặt: #" + reservation.getId() + " • " + reservation.getTableLabel());
        }

        binding.tabLayoutPreorderCat.addTab(binding.tabLayoutPreorderCat.newTab().setText("Tất cả"));
        for (Category cat : appStateManager.getCategories()) {
            binding.tabLayoutPreorderCat.addTab(binding.tabLayoutPreorderCat.newTab().setText(cat.getName()));
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
                showToast("Đã thêm " + food.getName() + " vào Pre-order!");
            }

            @Override
            public void onToggleFavorite(FoodItem food) {
                persistFavorite(food);
            }
        });

        binding.rvPreorderFoods.setLayoutManager(new GridLayoutManager(getContext(), 2));
        binding.rvPreorderFoods.setAdapter(adapter);

        binding.tabLayoutPreorderCat.addOnTabSelectedListener(new TabLayout.OnTabSelectedListener() {
            @Override
            public void onTabSelected(TabLayout.Tab tab) {
                displayedFoods.clear();
                if (tab.getPosition() == 0) {
                    displayedFoods.addAll(appStateManager.getFoodItems());
                } else {
                    Category cat = appStateManager.getCategories().get(tab.getPosition() - 1);
                    for (FoodItem fi : appStateManager.getFoodItems()) {
                        if (fi.getCategoryId().equals(cat.getId())) displayedFoods.add(fi);
                    }
                }
                adapter.notifyDataSetChanged();
            }
            @Override public void onTabUnselected(TabLayout.Tab tab) {}
            @Override public void onTabReselected(TabLayout.Tab tab) {}
        });

        binding.cardPreorderBottomBar.setOnClickListener(v -> {
            navigateTo(PreorderCartFragment.newInstance(reservation));
        });

        updateCartBar();
        if (reservation != null) MobileRepository.getInstance().loadPreOrder(reservation,
                new MobileRepository.Callback<org.json.JSONObject>() {
                    @Override public void onSuccess(org.json.JSONObject value) { if (binding != null) updateCartBar(); }
                    @Override public void onError(ApiException error) { if (binding != null) showToast(apiError(error)); }
                });
    }

    private void updateCartBar() {
        if (binding == null) return;
        int count = appStateManager.getCartItemCount();
        if (count > 0) {
            binding.cardPreorderBottomBar.setVisibility(View.VISIBLE);
            binding.tvPreorderCartCount.setText(String.valueOf(count));
            long total = appStateManager.calculateCartSubtotal();
            binding.tvPreorderCartTotal.setText(String.format(java.util.Locale.GERMANY, "%,dđ", total));
        } else {
            binding.cardPreorderBottomBar.setVisibility(View.GONE);
        }
    }

    @Override
    public void onStateChanged() {
        if (isAdded()) {
            updateCartBar();
        }
    }

    @Override
    public void onDestroyView() {
        appStateManager.removeListener(this);
        super.onDestroyView();
        binding = null;
    }
}
