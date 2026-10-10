package com.restaurant.viettable.data;

import android.content.Context;
import android.content.ContextWrapper;
import android.content.SharedPreferences;
import androidx.test.platform.app.InstrumentationRegistry;
import org.json.JSONObject;
import org.junit.Test;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import static org.junit.Assert.*;

/** Dedicated preferences namespace: never clears user auth or application catalog. */
public class PublicCatalogCacheDeviceTest {
    @Test public void diskRestoreRejectsExpiredCorruptAndKeepsBranchKeysSeparate() throws Exception {
        Context target = InstrumentationRegistry.getInstrumentation().getTargetContext();
        String suffix = "_cache_test_" + java.util.UUID.randomUUID();
        java.util.List<SharedPreferences> touched = new java.util.ArrayList<>();
        Context isolated = new ContextWrapper(target) {
            @Override public Context getApplicationContext() { return this; }
            @Override public SharedPreferences getSharedPreferences(String name, int mode) {
                SharedPreferences prefs = target.getSharedPreferences(name + suffix, mode);
                if (!touched.contains(prefs)) touched.add(prefs);
                return prefs;
            }
        };
        try {
            PublicCatalogCache first = new PublicCatalogCache();
            CountDownLatch initialized = new CountDownLatch(1);
            InstrumentationRegistry.getInstrumentation().runOnMainSync(() -> first.initialize(isolated, initialized::countDown));
            assertTrue(initialized.await(10, TimeUnit.SECONDS));
            long now = System.currentTimeMillis();
            SharedPreferences prefs = touched.get(0);
            prefs.edit().putString("menu_1", new JSONObject().put("saved", now).put("payload", "{\"categories\":[]}").toString())
                    .putString("menu_2", new JSONObject().put("saved", now - CatalogCachePolicy.MAX_STALE_MS - 1000).put("payload", "{}").toString())
                    .putString("menu_3", "broken")
                    .putString("private_session", new JSONObject().put("saved", now).put("payload", "{}").toString()).commit();
            PublicCatalogCache restored = new PublicCatalogCache();
            CountDownLatch loaded = new CountDownLatch(1);
            InstrumentationRegistry.getInstrumentation().runOnMainSync(() -> restored.initialize(isolated, loaded::countDown));
            assertTrue(loaded.await(10, TimeUnit.SECONDS));
            InstrumentationRegistry.getInstrumentation().runOnMainSync(() -> {
                assertTrue(restored.read("menu_1") instanceof JSONObject);
                assertTrue(restored.fresh("menu_1"));
                assertNull(restored.read("menu_2"));
                assertNull(restored.read("menu_3"));
                assertNull(restored.read("private_session"));
                assertNull(restored.read("menu_4"));
            });
        } finally { for (SharedPreferences prefs : touched) prefs.edit().clear().commit(); }
    }
}
