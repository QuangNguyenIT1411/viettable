package com.restaurant.viettable.ui.home;

import android.os.Bundle;
import android.os.SystemClock;
import android.util.Log;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.restaurant.viettable.R;
import com.restaurant.viettable.BuildConfig;
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
import com.restaurant.viettable.util.DisplayFormatters;
import com.restaurant.viettable.network.ApiException;

import java.util.ArrayList;
import java.util.List;

public class HomeFragment extends BaseFragment implements AppStateManager.StateChangeListener {

    private FragmentHomeBinding binding;
    private boolean loadingBranches;
    private String branchError;
    private int renderCount;
    private List<Category> renderedCategories;
    private List<FoodItem> renderedFoods;

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
        binding.btnRetryCatalog.setOnClickListener(v -> loadCatalog(true));
        loadCatalog(false);
    }

    private void setupViews() {
        if (binding == null) return;
        long renderStarted = SystemClock.elapsedRealtime();

        // 1. Header Branch
        Branch branch = appStateManager.getSelectedBranch();
        if (branch != null) branchError = null;
        binding.tvHeaderBranchName.setText(branch == null ? getString(R.string.catalog_select_branch) : branch.getName());
        binding.btnHeaderBranch.setOnClickListener(v -> navigateTo(new ChangeBranchFragment()));
        binding.btnHeaderSearch.setOnClickListener(v -> navigateTo(new SearchFragment()));
        binding.btnHeaderNotification.setOnClickListener(v -> navigateTo(new NotificationFragment()));
        String badge = com.restaurant.viettable.util.NotificationRules.badge(
                com.restaurant.viettable.util.NotificationRules.unread(appStateManager.getNotifications()));
        binding.viewNotifBadge.setText(badge);
        binding.viewNotifBadge.setVisibility(badge == null ? View.GONE : View.VISIBLE);
        binding.btnHeaderNotification.setContentDescription(badge == null ? getString(R.string.a11y_notifications)
                : getString(R.string.a11y_notifications) + ", " + badge + " chưa đọc");
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
        // The soonest open booking, not the newest one (#18 at 14:30 was shown before #15 at 14:00).
        Reservation upcomingRes = appStateManager.getUpcomingReservation();

        if (upcomingRes != null) {
            binding.cardUpcomingReservation.setVisibility(View.VISIBLE);
            binding.tvResCode.setText("#" + upcomingRes.getId());
            binding.tvResDateTime.setText(upcomingRes.getTimeSlot() + " • " + DisplayFormatters.date(upcomingRes.getDate())
                    + " • " + upcomingRes.getGuestCount() + " khách • " + upcomingRes.getTableLabel());
            binding.tvResBranchName.setText(upcomingRes.getBranchName());

            final Reservation resToOpen = upcomingRes;
            binding.btnViewResDetail.setOnClickListener(v -> navigateTo(ResDetailFragment.newInstance(resToOpen)));
            binding.btnResPreOrder.setOnClickListener(v -> navigateTo(PreorderMenuFragment.newInstance(resToOpen)));
            // Ordering ahead opens once the restaurant has confirmed the booking.
            binding.btnResPreOrder.setVisibility(upcomingRes.getStatus() == Reservation.Status.CONFIRMED ? View.VISIBLE : View.GONE);
        } else {
            binding.cardUpcomingReservation.setVisibility(View.GONE);
        }

        // 4. Quick Actions
        binding.btnQuickReservation.setOnClickListener(v -> switchTab(R.id.nav_reservation));
        binding.btnQuickQr.setOnClickListener(v -> switchTab(R.id.nav_qr));
        binding.btnQuickMenu.setOnClickListener(v -> navigateTo(new MenuFragment()));
        binding.btnQuickOrders.setOnClickListener(v -> switchTab(R.id.nav_orders));
        binding.btnSeeAllMenu.setOnClickListener(v -> navigateTo(new MenuFragment()));
        // Promotions are hidden until they are supplied by a real backend source.
        binding.bannerPromo.setVisibility(View.GONE);

        // Session, reservation and profile updates must not discard 3 RecyclerView
        // adapters or rebuild every category chip. Only Catalog replacement changes
        // the displayed set; favorite changes still rebind the visible cards.
        List<Category> categories = appStateManager.getCategories();
        List<FoodItem> foods = appStateManager.getFoodItems();
        if (renderedCategories == null || !renderedCategories.equals(categories)) {
            setupCategories();
            renderedCategories = new ArrayList<>(categories);
        }
        if (renderedFoods == null || !renderedFoods.equals(foods)) {
            setupFoodRecyclers();
            renderedFoods = new ArrayList<>(foods);
        } else {
            refreshVisibleFavorites(binding.rvFeaturedFoods);
            refreshVisibleFavorites(binding.rvBestSellers);
            refreshVisibleFavorites(binding.rvRecommendations);
        }
        renderCatalogState();
        if (BuildConfig.DEBUG) Log.i("HomePerf", "render count=" + (++renderCount)
                + " ms=" + (SystemClock.elapsedRealtime() - renderStarted)
                + " categories=" + appStateManager.getCategories().size()
                + " dishes=" + appStateManager.getFoodItems().size()
                + " active=" + (appStateManager.getActiveDiningSession() != null));
    }

    private void loadCatalog(boolean retry) {
        if (binding == null || loadingBranches) return;
        Branch branch = appStateManager.getSelectedBranch();
        if (branch == null) {
            if (!appStateManager.getBranches().isEmpty()) return;
            if (!retry && branchError != null) return;
            loadingBranches = true;
            branchError = null;
            renderCatalogState();
            MobileRepository.getInstance().loadBranches(new MobileRepository.Callback<List<Branch>>() {
                @Override public void onSuccess(List<Branch> branches) {
                    loadingBranches = false;
                    if (binding == null) return;
                    setupViews();
                    loadCatalog(false);
                }
                @Override public void onError(ApiException error) {
                    loadingBranches = false;
                    branchError = apiError(error);
                    if (binding != null) renderCatalogState();
                }
            });
            return;
        }
        if (appStateManager.isCatalogLoading()) return;
        if (!retry && (branch.getId().equals(appStateManager.getCatalogBranchId())
                || appStateManager.getCatalogError() != null)) return;
        MobileRepository.getInstance().loadMenu(branch.getId(), retry, new MobileRepository.Callback<Void>() {
            @Override public void onSuccess(Void value) { if (binding != null) setupViews(); }
            @Override public void onError(ApiException error) { if (binding != null) renderCatalogState(); }
        });
    }

    private void renderCatalogState() {
        if (binding == null) return;
        boolean loading = loadingBranches || appStateManager.isCatalogLoading();
        String error = branchError != null ? branchError : appStateManager.getCatalogError();
        boolean empty = appStateManager.getFoodItems().isEmpty();
        binding.progressCatalog.setVisibility(loading ? View.VISIBLE : View.GONE);
        binding.tvCatalogState.setVisibility(loading || error != null || empty ? View.VISIBLE : View.GONE);
        binding.tvCatalogState.setText(loading ? getString(R.string.catalog_loading)
                : error != null ? error : appStateManager.getSelectedBranch() == null
                ? getString(R.string.catalog_select_branch) : getString(R.string.catalog_empty));
        binding.btnRetryCatalog.setVisibility(!loading && error != null ? View.VISIBLE : View.GONE);
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
                showCartAdded(food.getName());
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

    private void refreshVisibleFavorites(RecyclerView list) {
        if (list.getAdapter() != null) list.getAdapter().notifyDataSetChanged();
    }

    /** The bell counts the server inbox: refresh it whenever Trang chủ comes back into view. */
    @Override
    public void onResume() {
        super.onResume();
        if (!appStateManager.isLoggedIn()) return;
        MobileRepository.getInstance().loadNotifications(new MobileRepository.Callback<java.util.List<com.restaurant.viettable.model.NotificationItem>>() {
            @Override public void onSuccess(java.util.List<com.restaurant.viettable.model.NotificationItem> value) { }
            @Override public void onError(com.restaurant.viettable.network.ApiException error) { }
        });
    }

    @Override
    public void onStateChanged() {
        if (isAdded()) {
            setupViews();
            loadCatalog(false);
        }
    }

    @Override
    public void onDestroyView() {
        appStateManager.removeListener(this);
        super.onDestroyView();
        binding = null;
        renderedCategories = null;
        renderedFoods = null;
    }
}
