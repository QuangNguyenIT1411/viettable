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
            Insets ime = insets.getInsets(WindowInsetsCompat.Type.ime());
            if (liftBottomAboveKeyboard()) {
                // Edge-to-edge ignores adjustResize: lift the "safe_bottom" bar (the input) above the keyboard.
                navigationBars = Insets.of(navigationBars.left, navigationBars.top, navigationBars.right,
                        Math.max(navigationBars.bottom, ime.bottom));
            } else {
                // Every other screen (owner 07/11: the keyboard covered the password box of "Đăng ký"): edge-to-edge
                // ignores adjustResize, so pad the screen by the part of the keyboard that overlaps it. Scroll views
                // shrink and keep the focused box in view; the bottom bar sits on the keyboard.
                int overlap = keyboardOverlap(v, ime.bottom);
                int[] p = getOrCreateInitialPadding(v);
                v.setPadding(p[0], p[1], p[2], p[3] + overlap);
                if (overlap > 0) navigationBars = Insets.of(navigationBars.left, navigationBars.top, navigationBars.right, 0);
                if (overlap > 0) v.post(() -> revealFocused(v));
            }

            onApplyInsets(root, statusBars, navigationBars);
            return insets;
        });
        ViewCompat.requestApplyInsets(root);
    }

    /** How much of the keyboard covers this screen (the part below the screen, e.g. the bottom menu, is not counted). */
    static int keyboardOverlap(View screen, int keyboardHeight) {
        if (keyboardHeight <= 0 || screen.getHeight() == 0) return 0;
        int[] at = new int[2];
        screen.getLocationInWindow(at);
        int below = screen.getRootView().getHeight() - (at[1] + screen.getHeight());
        return Math.max(0, keyboardHeight - Math.max(0, below));
    }

    /** After the screen shrinks, scroll the box being typed in back into view. */
    private static void revealFocused(View screen) {
        View focused = screen.findFocus();
        if (focused == null) return;
        android.graphics.Rect r = new android.graphics.Rect();
        focused.getDrawingRect(r);
        r.bottom += (int) (16 * screen.getResources().getDisplayMetrics().density); // a little room under the box
        focused.requestRectangleOnScreen(r, false);
    }

    /** Screens whose bottom bar holds a text box (a chat) return true so the keyboard never covers it. */
    protected boolean liftBottomAboveKeyboard() { return false; }

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

    /** For a sentence the guest must read in full (e.g. why the chosen table is gone). */
    protected void showLongToast(String message) {
        if (getContext() != null) {
            Toast.makeText(getContext(), message, Toast.LENGTH_LONG).show();
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

    protected void resetTo(Fragment fragment) {
        if (navigationCoordinator != null) {
            navigationCoordinator.resetTo(fragment);
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
