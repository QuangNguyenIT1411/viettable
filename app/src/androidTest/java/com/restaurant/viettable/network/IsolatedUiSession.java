package com.restaurant.viettable.network;

import android.content.Context;
import android.os.Bundle;
import androidx.test.platform.app.InstrumentationRegistry;
import com.restaurant.viettable.data.AppStateManager;
import org.json.JSONObject;
import java.lang.reflect.Field;
import java.util.Collections;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import static org.junit.Assert.*;

/** Replaces only the in-process ApiClient singleton with a test Keystore/store.
 * The user's real encrypted store is never cleared, read into test assertions, or sent to logout.
 */
final class IsolatedUiSession implements AutoCloseable {
    final Context context;
    final ApiClient client;
    final String username;
    final String password;
    private final ApiClient original;
    private final SecureTokenStore store;
    private final String secureName;
    private final String legacyName;

    IsolatedUiSession() throws Exception {
        context = InstrumentationRegistry.getInstrumentation().getTargetContext();
        Bundle args = InstrumentationRegistry.getArguments();
        username = args.getString("testUsername", "").trim();
        password = args.getString("testPassword", "");
        ApiClient.initialize(context);
        original = ApiClient.getInstance();
        SignalRManager.getInstance().stopAndClear();
        String id = "ui_test_" + UUID.randomUUID().toString().replace("-", "");
        secureName = id + "_secure";
        legacyName = id + "_legacy";
        store = new SecureTokenStore(context, secureName, legacyName, id + ".aes.v1", () -> { });
        client = new ApiClient(store);
        setSingleton(client);
        resetMemoryState();
    }

    void requireCredentials() {
        assertFalse("Pass -e testUsername for backend-connected UI tests", username.isEmpty());
        assertFalse("Pass -e testPassword for backend-connected UI tests", password.isEmpty());
    }

    void loginThroughGateway() throws Exception {
        requireCredentials();
        JSONObject response = post("/api/mobile/v1/auth/login",
                new JSONObject().put("username", username).put("password", password));
        assertTrue("Secure test session write failed", client.saveTokenResponse(response));
    }

    String encryptedBlob() {
        return context.getSharedPreferences(secureName, 0).getString("auth_blob", "");
    }

    JSONObject post(String path, JSONObject body) throws Exception {
        Reply reply = new Reply();
        client.post(path, body, false, null, reply);
        return reply.awaitObject();
    }

    JSONObject getObject(String path, boolean authenticated) throws Exception {
        Reply reply = new Reply();
        client.get(path, authenticated, reply);
        return reply.awaitObject();
    }

    private void logoutTestSession() {
        if (!client.hasSession()) return;
        try {
            post("/api/mobile/v1/auth/logout",
                    new JSONObject().put("refreshToken", client.getRefreshToken()));
        } catch (Exception ignored) {
            // Local secure cleanup is mandatory even if Gateway cleanup is unavailable.
        }
    }

    @Override public void close() throws Exception {
        SignalRManager.getInstance().stopAndClear();
        logoutTestSession();
        store.clear();
        context.getSharedPreferences(secureName, 0).edit().clear().commit();
        context.getSharedPreferences(legacyName, 0).edit().clear().commit();
        setSingleton(original);
        resetMemoryState();
        AppStateManager.getInstance().setLoggedIn(original.hasSession());
    }

    private static void setSingleton(ApiClient value) throws Exception {
        Field field = ApiClient.class.getDeclaredField("instance");
        field.setAccessible(true);
        field.set(null, value);
    }

    private static void resetMemoryState() {
        AppStateManager state = AppStateManager.getInstance();
        state.clearAuthenticatedData();
        state.replaceBranches(Collections.emptyList());
        state.replaceCatalog(Collections.emptyList(), Collections.emptyList());
    }

    private static final class Reply implements ApiClient.Callback {
        private final CountDownLatch ready = new CountDownLatch(1);
        private Object value;
        private ApiException error;
        @Override public void onSuccess(Object body) { value = body; ready.countDown(); }
        @Override public void onError(ApiException failure) { error = failure; ready.countDown(); }
        JSONObject awaitObject() throws Exception {
            assertTrue("Gateway callback timed out", ready.await(40, TimeUnit.SECONDS));
            assertNull("Gateway failed: " + (error == null ? "" : error.getCode()), error);
            return (JSONObject) value;
        }
    }
}
