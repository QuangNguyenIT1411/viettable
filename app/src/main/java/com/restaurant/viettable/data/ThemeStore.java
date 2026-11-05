package com.restaurant.viettable.data;

import android.content.Context;
import android.content.SharedPreferences;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatDelegate;

import com.restaurant.viettable.util.ThemeChoice;

/**
 * The chosen look, kept on this phone only (not tied to an account: it is a device taste, and it must apply before
 * anyone logs in). {@link #apply} switches at once through AppCompatDelegate, no restart of the app needed.
 */
public final class ThemeStore {
    /** Where the key lives: a SharedPreferences in the app, a plain field in unit tests. */
    public interface Backing {
        @Nullable String read();
        void write(String key);
    }

    private static final String PREFS = "appearance";
    private static final String KEY = "theme";
    private static ThemeStore instance;

    private final Backing backing;

    public ThemeStore(Backing backing) {
        this.backing = backing;
    }

    public static synchronized ThemeStore getInstance(Context context) {
        if (instance == null) {
            SharedPreferences preferences = context.getApplicationContext().getSharedPreferences(PREFS, Context.MODE_PRIVATE);
            instance = new ThemeStore(new Backing() {
                @Nullable @Override public String read() { return preferences.getString(KEY, null); }
                @Override public void write(String value) { preferences.edit().putString(KEY, value).apply(); }
            });
        }
        return instance;
    }

    public ThemeChoice get() {
        return ThemeChoice.fromKey(backing.read());
    }

    public void save(ThemeChoice choice) {
        backing.write(choice.key);
    }

    /** Puts the saved choice into effect (open activities recreate themselves). */
    public void apply() {
        AppCompatDelegate.setDefaultNightMode(get().nightMode);
    }
}
