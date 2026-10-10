package com.restaurant.viettable.data;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Handler;
import android.os.Looper;
import com.restaurant.viettable.BuildConfig;
import org.json.JSONObject;
import org.json.JSONTokener;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Versioned, origin/branch-scoped raw public responses. No user, favorite or session data. */
final class PublicCatalogCache {
    private final Map<String, JSONObject> entries = new HashMap<>(); // Main-thread owned.
    private final ExecutorService disk = Executors.newSingleThreadExecutor();
    private Handler main;
    private SharedPreferences preferences;
    private boolean initialized;

    void initialize(Context context, Runnable ready) {
        if (initialized) { ready.run(); return; }
        initialized = true;
        main = new Handler(Looper.getMainLooper());
        Context app = context.getApplicationContext();
        disk.execute(() -> {
            Map<String, JSONObject> loaded = new HashMap<>();
            preferences = app.getSharedPreferences("public_catalog_v1_" + originKey(BuildConfig.API_BASE_URL), Context.MODE_PRIVATE);
            for (Map.Entry<String, ?> row : preferences.getAll().entrySet()) {
                if (!row.getKey().equals("branches") && !row.getKey().matches("menu_[0-9]+")) continue;
                try {
                    JSONObject entry = new JSONObject(String.valueOf(row.getValue()));
                    if (CatalogCachePolicy.usable(entry.optLong("saved"), System.currentTimeMillis())) loaded.put(row.getKey(), entry);
                } catch (Exception ignored) { /* Corrupt public cache is a miss, not an auth failure. */ }
            }
            main.post(() -> { entries.putAll(loaded); ready.run(); });
        });
    }

    Object read(String key) {
        JSONObject entry = entries.get(key);
        if (entry == null || !CatalogCachePolicy.usable(entry.optLong("saved"), System.currentTimeMillis())) return null;
        try { return new JSONTokener(entry.getString("payload")).nextValue(); }
        catch (Exception ignored) { entries.remove(key); return null; }
    }

    boolean fresh(String key) {
        JSONObject entry = entries.get(key);
        return entry != null && CatalogCachePolicy.fresh(entry.optLong("saved"), System.currentTimeMillis());
    }

    void write(String key, Object response) {
        try {
            JSONObject entry = new JSONObject().put("saved", System.currentTimeMillis()).put("payload", response.toString());
            entries.put(key, entry);
            String serialized = entry.toString();
            disk.execute(() -> { if (preferences != null) preferences.edit().putString(key, serialized).apply(); });
        } catch (Exception ignored) { /* Storage failure must not turn a successful GET into an error. */ }
    }

    private static String originKey(String origin) {
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256").digest(origin.getBytes(StandardCharsets.UTF_8));
            StringBuilder result = new StringBuilder();
            for (byte b : digest) result.append(String.format(java.util.Locale.ROOT, "%02x", b));
            return result.toString();
        } catch (Exception impossible) { throw new IllegalStateException(impossible); }
    }
}
