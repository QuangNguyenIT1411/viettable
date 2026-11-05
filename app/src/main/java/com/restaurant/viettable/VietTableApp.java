package com.restaurant.viettable;

import android.app.Application;

import com.restaurant.viettable.data.ThemeStore;

public class VietTableApp extends Application {
    @Override
    public void onCreate() {
        super.onCreate();
        // Before any screen exists, so the first frame is already in the saved look (default Tối).
        ThemeStore.getInstance(this).apply();
    }
}
