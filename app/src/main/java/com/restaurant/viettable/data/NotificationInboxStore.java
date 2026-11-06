package com.restaurant.viettable.data;

import android.content.Context;
import android.content.SharedPreferences;

import androidx.annotation.Nullable;

import com.restaurant.viettable.BuildConfig;

import org.json.JSONArray;

/**
 * The last inbox the server sent, kept on this phone so "Thông báo" opens with it at once while the fresh list loads
 * (owner 07/11: the inbox loaded slowly). Tied to the account that owns the session; cleared on logout, never shown
 * for another account. Only what the server already sent this account is kept.
 */
public final class NotificationInboxStore {
    private static final String KEY_ACCOUNT = "account";
    private static final String KEY_ROWS = "rows";
    private static NotificationInboxStore instance;

    private SharedPreferences preferences;

    public static synchronized NotificationInboxStore getInstance() {
        if (instance == null) instance = new NotificationInboxStore();
        return instance;
    }

    public void initialize(Context context) {
        preferences = context.getApplicationContext().getSharedPreferences(
                "notification_inbox_" + Integer.toHexString(BuildConfig.API_BASE_URL.hashCode()), Context.MODE_PRIVATE);
    }

    public void remember(@Nullable String accountId, @Nullable JSONArray rows) {
        if (preferences == null || accountId == null || accountId.trim().isEmpty() || rows == null) return;
        preferences.edit().putString(KEY_ACCOUNT, accountId.trim()).putString(KEY_ROWS, rows.toString()).apply();
    }

    /** The saved rows if they belong to {@code sessionAccount}, otherwise null. */
    @Nullable public JSONArray rowsFor(@Nullable String sessionAccount) {
        if (preferences == null) return null;
        String raw = pick(preferences.getString(KEY_ACCOUNT, null), preferences.getString(KEY_ROWS, null), sessionAccount);
        if (raw == null) return null;
        try { return new JSONArray(raw); } catch (Exception e) { return null; }
    }

    public void clear() {
        if (preferences != null) preferences.edit().clear().apply();
    }

    @Nullable static String pick(@Nullable String savedAccount, @Nullable String savedRows, @Nullable String sessionAccount) {
        if (savedAccount == null || savedRows == null || sessionAccount == null) return null;
        return savedAccount.trim().equals(sessionAccount.trim()) ? savedRows : null;
    }
}
