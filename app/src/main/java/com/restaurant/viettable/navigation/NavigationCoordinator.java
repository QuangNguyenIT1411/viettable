package com.restaurant.viettable.navigation;

import androidx.fragment.app.Fragment;

public interface NavigationCoordinator {
    void navigateTo(Fragment fragment, boolean addToBackStack);
    void navigateBack();
    /** Drops the whole back stack (old screens of a logged-out user are never rebuilt), then shows fragment. */
    void resetTo(Fragment fragment);
    void switchTab(int navItemId);
    void openDiningSession();
    void setBottomNavVisible(boolean visible);
    void setFabVisible(boolean visible);
    void setStatusBarLightIcons(boolean isLightIcons);
}
