package com.restaurant.viettable.ui.home;

import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.restaurant.viettable.R;
import com.restaurant.viettable.adapter.FoodAdapter;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.databinding.FragmentHomeBinding;
import com.restaurant.viettable.model.*;
import com.restaurant.viettable.ui.base.BaseFragment;
import com.restaurant.viettable.ui.branch.ChangeBranchFragment;
import com.restaurant.viettable.ui.common.ActiveDiningCardBinder;
import com.restaurant.viettable.ui.food.CategoryFragment;
import com.restaurant.viettable.ui.food.FoodDetailFragment;
import com.restaurant.viettable.ui.food.MenuFragment;
import com.restaurant.viettable.ui.order.CurrentOrdersFragment;
import com.restaurant.viettable.ui.payment.PaymentCheckoutFragment;
import com.restaurant.viettable.ui.preorder.PreorderMenuFragment;
import com.restaurant.viettable.ui.reservation.ResDetailFragment;
import com.restaurant.viettable.ui.session.DineinMenuFragment;

import java.util.ArrayList;
import java.util.List;

public class HomeFragment extends BaseFragment implements AppStateManager.StateChangeListener {

    private FragmentHomeBinding binding;

    public static HomeFragment newInstance() {
        return new HomeFragment();
    }

    @Nullable
    @Override
    public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
        binding = FragmentHomeBinding.inflate(inflater, container, false);
        setBottomNavVisible(true);
        return binding.getRoot();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        appStateManager.addListener(this);
        setupViews();
    }

    private void setupViews() {
        if (binding == null) return;

        // 1. Header Branch
        Branch branch = appStateManager.getSelectedBranch();
        if (branch != null) {
            binding.tvHeaderBranchName.setText(branch.getName());
        }
        binding.btnHeaderBranch.setOnClickListener(v -> navigateTo(new ChangeBranchFragment()));
        binding.btnHeaderSearch.setOnClickListener(v -> navigateTo(new SearchFragment()));
        binding.btnHeaderNotification.setOnClickListener(v -> navigateTo(new NotificationFragment()));
        binding.btnHomeSearchBox.setOnClickListener(v -> navigateTo(new SearchFragment()));

        // 2. Active Dining Session Card (Screen 15)
        ActiveDiningCardBinder.bind(binding.layoutActiveDining, appStateManager.getActiveDiningSession(), new ActiveDiningCardBinder.OnActionListener() {
            @Override
            public void onCardClicked() {
                openDiningSession();
            }

            @Override
            public void onOrderMoreClicked() {
                navigateTo(new DineinMenuFragment());
            }

            @Override
            public void onTrackOrdersClicked() {
                navigateTo(new CurrentOrdersFragment());
            }

            @Override
            public void onPayClicked() {
                navigateTo(new PaymentCheckoutFragment());
            }
        });

        // 3. Upcoming Reservation Card
        List<Reservation> resList = appStateManager.getReservations();
        Reservation upcomingRes = null;
        for (Reservation r : resList) {
            if (r.getStatus() == Reservation.Status.CONFIRMED) {
                upcomingRes = r;
                break;
            }
        }

        if (upcomingRes != null) {
            binding.cardUpcomingReservation.setVisibility(View.VISIBLE);
            binding.tvResCode.setText("#" + upcomingRes.getId());
            binding.tvResDateTime.setText(upcomingRes.getDate() + " • " + upcomingRes.getGuestCount() + " khách • Bàn " + upcomingRes.getTableId());
            binding.tvResBranchName.setText(upcomingRes.getBranchName());

            final Reservation resToOpen = upcomingRes;
            binding.btnViewResDetail.setOnClickListener(v -> navigateTo(ResDetailFragment.newInstance(resToOpen)));
            binding.btnResPreOrder.setOnClickListener(v -> navigateTo(PreorderMenuFragment.newInstance(resToOpen)));
        } else {
            binding.cardUpcomingReservation.setVisibility(View.GONE);
        }

        // 4. Quick Actions
        binding.btnQuickReservation.setOnClickListener(v -> switchTab(R.id.nav_reservation));
        binding.btnQuickQr.setOnClickListener(v -> switchTab(R.id.nav_qr));
        binding.btnQuickMenu.setOnClickListener(v -> navigateTo(new MenuFragment()));
        binding.btnQuickOrders.setOnClickListener(v -> switchTab(R.id.nav_orders));
        binding.btnSeeAllMenu.setOnClickListener(v -> navigateTo(new MenuFragment()));
        binding.btnCollectVoucher.setOnClickListener(v -> showToast("Đã lưu mã VIETTABLE20 vào kho voucher của bạn!"));

        // 5. Category Chips
        setupCategories();

        // 6. Food Sections
        setupFoodRecyclers();
    }

    private void setupCategories() {
        binding.layoutCategoryChips.removeAllViews();
        List<Category> categories = appStateManager.getCategories();
        int padH = (int) (16 * getResources().getDisplayMetrics().density);
        int padV = (int) (8 * getResources().getDisplayMetrics().density);
        int marginEnd = (int) (10 * getResources().getDisplayMetrics().density);

        for (Category cat : categories) {
            TextView chip = new TextView(getContext());
            chip.setText(cat.getName());
            chip.setTextColor(androidx.core.content.ContextCompat.getColor(requireContext(), R.color.text_primary));
            chip.setTextSize(13);
            chip.setTypeface(null, android.graphics.Typeface.BOLD);
            chip.setBackgroundResource(R.drawable.bg_category_chip);
            chip.setPadding(padH, padV, padH, padV);

            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT
            );
            lp.setMargins(0, 0, marginEnd, 0);
            chip.setLayoutParams(lp);

            chip.setOnClickListener(v -> navigateTo(CategoryFragment.newInstance(cat)));
            binding.layoutCategoryChips.addView(chip);
        }
    }

    private void setupFoodRecyclers() {
        List<FoodItem> allFoods = appStateManager.getFoodItems();

        FoodAdapter.OnFoodActionListener foodListener = new FoodAdapter.OnFoodActionListener() {
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
            }
        };

        // Featured: foods with high rating
        binding.rvFeaturedFoods.setLayoutManager(new LinearLayoutManager(getContext(), LinearLayoutManager.HORIZONTAL, false));
        binding.rvFeaturedFoods.setAdapter(new FoodAdapter(allFoods, foodListener));

        // Best Sellers: reverse sublist
        List<FoodItem> bestSellers = new ArrayList<>(allFoods);
        java.util.Collections.reverse(bestSellers);
        binding.rvBestSellers.setLayoutManager(new LinearLayoutManager(getContext(), LinearLayoutManager.HORIZONTAL, false));
        binding.rvBestSellers.setAdapter(new FoodAdapter(bestSellers, foodListener));

        // Recommendations
        binding.rvRecommendations.setLayoutManager(new LinearLayoutManager(getContext(), LinearLayoutManager.HORIZONTAL, false));
        binding.rvRecommendations.setAdapter(new FoodAdapter(allFoods, foodListener));
    }

    @Override
    public void onStateChanged() {
        if (isAdded()) {
            setupViews();
        }
    }

    @Override
    public void onDestroyView() {
        appStateManager.removeListener(this);
        super.onDestroyView();
        binding = null;
    }
}
