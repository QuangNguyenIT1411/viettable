package com.restaurant.viettable.network;

import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;
import com.restaurant.viettable.BuildConfig;
import org.json.JSONObject;
import org.junit.Assume;
import org.junit.Test;
import org.junit.runner.RunWith;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import static org.junit.Assert.*;

/** Opt-in only AFTER real Google chooser login. No token injection or test-account substitution. */
@RunWith(AndroidJUnit4.class)
public class GoogleSessionAcceptanceTest {
    @Test public void realGoogleSession_profileRefreshAndSignalR() throws Exception {
        android.os.Bundle args = InstrumentationRegistry.getArguments();
        Assume.assumeTrue("Requires explicit real Google session acceptance",
                "true".equals(args.getString("runGoogleSessionAcceptance")));
        String expectedEmail = args.getString("expectedGoogleEmail", "");
        String expectedClientId = args.getString("expectedGoogleWebClientId", "");
        assertFalse("Expected test email required", expectedEmail.isEmpty());
        assertFalse("Expected Web client ID required", expectedClientId.isEmpty());
        assertEquals(expectedClientId, BuildConfig.GOOGLE_WEB_CLIENT_ID);
        ApiClient.initialize(InstrumentationRegistry.getInstrumentation().getTargetContext());
        ApiClient client = ApiClient.getInstance();
        assertTrue("Real Google login must already have persisted a session", client.hasSession());
        JSONObject before = profile(client);
        assertTrue("Profile must belong to the chosen test account",
                expectedEmail.equalsIgnoreCase(before.optString("email")));
        String previousRefresh = client.getRefreshToken();
        assertTrue("Real session refresh rejected", client.refreshAccessTokenBlocking(client.getAccessToken()));
        assertTrue("Refresh must rotate", !previousRefresh.equals(client.getRefreshToken()));
        JSONObject after = profile(client);
        assertEquals(before.getInt("customerId"), after.getInt("customerId"));
        CountDownLatch connected = new CountDownLatch(1);
        SignalRManager manager = SignalRManager.getInstance();
        SignalRManager.Listener listener = state -> {
            if (state == SignalRManager.ConnectionState.CONNECTED) connected.countDown();
        };
        try {
            manager.initialize();
            manager.addListener(listener);
            manager.resume();
            assertTrue("Refreshed Google session must connect to Hub", connected.await(30, TimeUnit.SECONDS));
        } finally {
            manager.removeListener(listener);
            manager.pause();
        }
        assertTrue(client.hasSession());
    }

    private static JSONObject profile(ApiClient client) throws Exception {
        CountDownLatch done = new CountDownLatch(1);
        java.util.concurrent.atomic.AtomicReference<JSONObject> response = new java.util.concurrent.atomic.AtomicReference<>();
        java.util.concurrent.atomic.AtomicReference<String> error = new java.util.concurrent.atomic.AtomicReference<>();
        client.get("/api/mobile/v1/me", true, new ApiClient.Callback() {
            @Override public void onSuccess(Object body) { response.set((JSONObject) body); done.countDown(); }
            @Override public void onError(ApiException failure) { error.set(failure.getCode()); done.countDown(); }
        });
        assertTrue("Profile timed out", done.await(40, TimeUnit.SECONDS));
        assertNull("Profile rejected", error.get());
        assertNotNull(response.get());
        return response.get();
    }
}
