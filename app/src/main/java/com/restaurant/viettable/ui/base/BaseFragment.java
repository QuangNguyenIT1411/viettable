package com.restaurant.viettable.ui.base;

import android.content.Context;
import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.fragment.app.Fragment;

import com.google.android.material.appbar.AppBarLayout;
import com.restaurant.viettable.R;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.FoodItem;
import com.restaurant.viettable.network.ApiException;
import com.restaurant.viettable.navigation.NavigationCoordinator;

public abstract class BaseFragment extends Fragment {
    protected NavigationCoordinator navigationCoordinator;
    protected AppStateManager appStateManager;

    @Override
    public void onAttach(@NonNull Context context) {
        super.onAttach(context);
        if (context instanceof NavigationCoordinator) {
            this.navigationCoordinator = (NavigationCoordinator) context;
        }
        this.appStateManager = AppStateManager.getInstance();
    }

    @Override
    public void onViewCreated(@NonNull View view, @Nullable Bundle savedInstanceState) {
        super.onViewCreated(view, savedInstanceState);
        setupWindowInsets(view);
    }

    protected void setupWindowInsets(@NonNull View root) {
        ViewCompat.setOnApplyWindowInsetsListener(root, (v, insets) -> {
            Insets statusBars = insets.getInsets(WindowInsetsCompat.Type.statusBars());
            Insets navigationBars = insets.getInsets(WindowInsetsCompat.Type.navigationBars());

            onApplyInsets(root, statusBars, navigationBars);
            return insets;
        });
        ViewCompat.requestApplyInsets(root);
    }

    private int[] getOrCreateInitialPadding(View view) {
        int[] initial = (int[]) view.getTag(R.id.tag_initial_padding);
        if (initial == null) {
            initial = new int[]{view.getPaddingLeft(), view.getPaddingTop(), view.getPaddingRight(), view.getPaddingBottom()};
            view.setTag(R.id.tag_initial_padding, initial);
        }
        return initial;
    }

    /**
     * Called when window insets are applied. Subclasses can override if needed.
     */
    protected void onApplyInsets(@NonNull View root, Insets statusBars, Insets navigationBars) {
        // 1. Automatic AppBarLayout handling (found across ~26 fragments)
        View appBar = findViewByClass(root, AppBarLayout.class);
        if (appBar != null) {
            int[] p = getOrCreateInitialPadding(appBar);
            appBar.setPadding(p[0], p[1] + statusBars.top, p[2], p[3]);
        }

        // 2. Tag-based safe top views (android:tag="safe_top")
        View safeTop = root.findViewWithTag("safe_top");
        if (safeTop != null && safeTop != appBar) {
            int[] p = getOrCreateInitialPadding(safeTop);
            safeTop.setPadding(p[0], p[1] + statusBars.top, p[2], p[3]);
        }

        // 3. Tag-based safe bottom views (android:tag="safe_bottom")
        View safeBottom = root.findViewWithTag("safe_bottom");
        if (safeBottom != null) {
            int[] p = getOrCreateInitialPadding(safeBottom);
            safeBottom.setPadding(p[0], p[1], p[2], p[3] + navigationBars.bottom);
        }

        // 4. Tag-based safe all views (android:tag="safe_all")
        View safeAll = root.findViewWithTag("safe_all");
        if (safeAll != null) {
            int[] p = getOrCreateInitialPadding(safeAll);
            safeAll.setPadding(p[0], p[1] + statusBars.top, p[2], p[3] + navigationBars.bottom);
        }
    }

    protected void applyTopInset(View targetView) {
        if (targetView == null) return;
        ViewCompat.setOnApplyWindowInsetsListener(targetView, (v, insets) -> {
            Insets statusBars = insets.getInsets(WindowInsetsCompat.Type.statusBars());
            int[] p = getOrCreateInitialPadding(v);
            v.setPadding(p[0], p[1] + statusBars.top, p[2], p[3]);
            return insets;
        });
        ViewCompat.requestApplyInsets(targetView);
    }

    protected void applyBottomInset(View targetView) {
        if (targetView == null) return;
        ViewCompat.setOnApplyWindowInsetsListener(targetView, (v, insets) -> {
            Insets navigationBars = insets.getInsets(WindowInsetsCompat.Type.navigationBars());
            int[] p = getOrCreateInitialPadding(v);
            v.setPadding(p[0], p[1], p[2], p[3] + navigationBars.bottom);
            return insets;
        });
        ViewCompat.requestApplyInsets(targetView);
    }

    private View findViewByClass(View root, Class<?> clazz) {
        if (root == null) return null;
        if (clazz.isInstance(root)) return root;
        if (root instanceof ViewGroup) {
            ViewGroup vg = (ViewGroup) root;
            for (int i = 0; i < vg.getChildCount(); i++) {
                View child = vg.getChildAt(i);
                View found = findViewByClass(child, clazz);
                if (found != null) return found;
            }
        }
        return null;
    }

    protected void showToast(String message) {
        if (getContext() != null) {
            Toast.makeText(getContext(), message, Toast.LENGTH_SHORT).show();
        }
    }

    protected void showCartAdded(String what) {
        showToast(com.restaurant.viettable.util.CartMessages.added(what, appStateManager.hasActiveDiningSession()));
    }

    protected String apiError(ApiException error) {
        if (error == null || error.getMessage() == null) return "Không thể thực hiện yêu cầu.";
        return error.getMessage();
    }

    protected void persistFavorite(FoodItem food) {
        boolean desired = food.isFavorite();
        MobileRepository.getInstance().setFavorite(food, desired, new MobileRepository.Callback<Void>() {
            @Override public void onSuccess(Void value) {
                showToast(desired ? "Đã thêm vào mục yêu thích" : "Đã bỏ yêu thích");
            }
            @Override public void onError(ApiException error) { showToast(apiError(error)); }
        });
    }

    protected void navigateTo(Fragment fragment) {
        if (navigationCoordinator != null) {
            navigationCoordinator.navigateTo(fragment, true);
        }
    }

    protected void navigateBack() {
        if (navigationCoordinator != null) {
            navigationCoordinator.navigateBack();
        }
    }

    protected void switchTab(int tabId) {
        if (navigationCoordinator != null) {
            navigationCoordinator.switchTab(tabId);
        }
    }

    protected void openDiningSession() {
        if (navigationCoordinator != null) {
            navigationCoordinator.openDiningSession();
        }
    }

    protected void setBottomNavVisible(boolean visible) {
        if (navigationCoordinator != null) {
            navigationCoordinator.setBottomNavVisible(visible);
        }
    }

    protected void setFabVisible(boolean visible) {
        if (navigationCoordinator != null) {
            navigationCoordinator.setFabVisible(visible);
        }
    }

    protected void setStatusBarLightIcons(boolean isLightIcons) {
        if (navigationCoordinator != null) {
            navigationCoordinator.setStatusBarLightIcons(isLightIcons);
        }
    }
}
