package com.restaurant.viettable.util;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatDelegate;

/**
 * "Cài đặt → Giao diện": Theo hệ thống / Sáng ("Phố ngày") / Tối ("Phố đêm"). The default is Tối so the demo build
 * looks exactly as before until the guest picks something else (owner, 5/11).
 */
public enum ThemeChoice {
    SYSTEM("system", "Theo hệ thống", AppCompatDelegate.MODE_NIGHT_FOLLOW_SYSTEM),
    LIGHT("light", "Sáng", AppCompatDelegate.MODE_NIGHT_NO),
    DARK("dark", "Tối", AppCompatDelegate.MODE_NIGHT_YES);

    public static final ThemeChoice DEFAULT = DARK;

    public final String key;
    public final String label;
    /** The {@link AppCompatDelegate} night mode that gives this look. */
    public final int nightMode;

    ThemeChoice(String key, String label, int nightMode) {
        this.key = key;
        this.label = label;
        this.nightMode = nightMode;
    }

    /** The saved key back to a choice; anything unknown or missing is the default (Tối). */
    public static ThemeChoice fromKey(@Nullable String key) {
        if (key != null) for (ThemeChoice value : values()) if (value.key.equals(key.trim())) return value;
        return DEFAULT;
    }

    public static String[] labels() {
        ThemeChoice[] all = values();
        String[] result = new String[all.length];
        for (int i = 0; i < all.length; i++) result[i] = all[i].label;
        return result;
    }
}
