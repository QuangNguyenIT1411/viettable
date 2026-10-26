package com.restaurant.viettable.data;

import android.content.Context;
import android.content.SharedPreferences;

import androidx.annotation.Nullable;

import com.restaurant.viettable.BuildConfig;
import com.restaurant.viettable.util.HomeDisplayRules;

/**
 * The given name Trang chủ greets with ("Chào Vinh,"), kept on this phone so it shows at once on the next start
 * instead of "Chào bạn," until the profile loads (owner, 26/10). Tied to the account that owns the session;
 * cleared on logout, and never shown for another account.
 */
public final class GreetingNameStore {
    private static final String KEY_ACCOUNT = "account";
    private static final String KEY_NAME = "name";
    private static GreetingNameStore instance;

    private SharedPreferences preferences;

    public static synchronized GreetingNameStore getInstance() {
        if (instance == null) instance = new GreetingNameStore();
        return instance;
    }

    public void initialize(Context context) {
        preferences = context.getApplicationContext().getSharedPreferences(
                "greeting_name_" + Integer.toHexString(BuildConfig.API_BASE_URL.hashCode()), Context.MODE_PRIVATE);
    }

    /** Called whenever the profile arrives: stores the capitalised given name for that account. */
    public void remember(@Nullable String accountId, @Nullable String fullName) {
        if (preferences == null || accountId == null || accountId.trim().isEmpty()) return;
        String name = HomeDisplayRules.greetingName(fullName);
        if (name.isEmpty()) return;
        preferences.edit().putString(KEY_ACCOUNT, accountId.trim()).putString(KEY_NAME, name).apply();
    }

    /** The saved name if it belongs to {@code sessionAccount}, otherwise "". */
    public String nameFor(@Nullable String sessionAccount) {
        if (preferences == null) return "";
        return pick(preferences.getString(KEY_ACCOUNT, null), preferences.getString(KEY_NAME, null), sessionAccount);
    }

    public void clear() {
        if (preferences != null) preferences.edit().clear().apply();
    }

    static String pick(@Nullable String savedAccount, @Nullable String savedName, @Nullable String sessionAccount) {
        if (savedAccount == null || savedName == null || sessionAccount == null) return "";
        return savedAccount.equals(sessionAccount.trim()) ? savedName : "";
    }
}
