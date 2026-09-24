package com.restaurant.viettable.network;

import android.content.Context;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;
import org.json.JSONObject;
import org.junit.Test;
import org.junit.Assume;
import org.junit.runner.RunWith;
import java.util.UUID;
import java.util.concurrent.*;
import static org.junit.Assert.*;
import com.restaurant.viettable.testing.BackendIntegrationTest;

/** Opt-in: requires the local Gateway and an existing app session. Never clears that session.
 * Creates one isolated backend test account; logs out its sessions, but does not delete server data.
 * Run explicitly with adb am instrument, not UTP (which may uninstall the main APK).
 */
@RunWith(AndroidJUnit4.class)
@BackendIntegrationTest
public class AuthSecurityRuntimeTest {
    private static final class Reply implements ApiClient.Callback {
        final CountDownLatch ready = new CountDownLatch(1);
        Object value;
        ApiException error;
        public void onSuccess(Object body) { value = body; ready.countDown(); }
        public void onError(ApiException failure) { error = failure; ready.countDown(); }
        JSONObject await() throws Exception {
            assertTrue("API timed out", ready.await(40, TimeUnit.SECONDS));
            assertTrue("API rejected request: " + (error == null ? "" : error.getStatusCode()), error == null);
            return (JSONObject) value;
        }
    }
    private static JSONObject post(ApiClient client, String path, JSONObject body) throws Exception {
        Reply result = new Reply(); client.post(path, body, false, null, result); return result.await();
    }

    @Test public void existingEncryptedSessionCanLoadProfileAndConnectSignalR() throws Exception {
        Assume.assumeTrue("Opt-in because this reads the user's existing session",
                "true".equalsIgnoreCase(InstrumentationRegistry.getArguments()
                        .getString("runExistingSessionSecurity", "false")));
        Context ctx = InstrumentationRegistry.getInstrumentation().getTargetContext();
        ApiClient.initialize(ctx);
        ApiClient client = ApiClient.getInstance();
        assertTrue("Existing encrypted session required", client.hasSession());
        Reply profile = new Reply(); client.get("/api/mobile/v1/me", true, profile);
        assertNotNull(profile.await());
        CountDownLatch connected = new CountDownLatch(1);
        SignalRManager manager = SignalRManager.getInstance();
        SignalRManager.Listener listener = state -> {
            if (state == SignalRManager.ConnectionState.CONNECTED) connected.countDown();
        };
        try {
            manager.initialize(); manager.addListener(listener); manager.resume();
            assertTrue("SignalR did not connect", connected.await(30, TimeUnit.SECONDS));
        } finally { manager.removeListener(listener); manager.pause(); manager.stopAndClear(); }
        assertTrue(client.hasSession());
        assertFalse(ctx.getSharedPreferences("viettable_session",0).contains("access_token"));
        assertFalse(ctx.getSharedPreferences("viettable_session",0).contains("refresh_token"));
    }

    @Test public void isolatedLoginConcurrent401RotationLogoutAndRelogin() throws Exception {
        Context ctx = InstrumentationRegistry.getInstrumentation().getTargetContext();
        String id = "auth_security_" + UUID.randomUUID().toString().replace("-", "");
        SecureTokenStore store = new SecureTokenStore(ctx,id+"_secure",id+"_legacy",id+".v1",()->{});
        ApiClient client = new ApiClient(store);
        ExecutorService refreshers = Executors.newFixedThreadPool(2);
        JSONObject credentials = new JSONObject().put("username",id).put("password",UUID.randomUUID()+"aA9!");
        try {
            JSONObject registration = new JSONObject(credentials.toString()).put("name","Security runtime test")
                    .put("email",id+"@example.test").put("phoneNumber","09"+String.format(java.util.Locale.ROOT,"%08d",Math.floorMod(UUID.randomUUID().getLeastSignificantBits(),100000000L)));
            assertTrue(client.saveTokenResponse(post(client,"/api/mobile/v1/auth/register",registration)));
            post(client,"/api/mobile/v1/auth/logout",new JSONObject().put("refreshToken",client.getRefreshToken()));
            client.clearSession();
            assertFalse(client.hasSession());
            assertTrue(client.saveTokenResponse(post(client,"/api/mobile/v1/auth/login",credentials)));
            String oldRefresh = client.getRefreshToken();
            // Only the isolated test store receives an invalid access value; the real refresh stays intact.
            assertTrue(store.save("invalid-access-test-only",oldRefresh));
            Reply[] replies = new Reply[4];
            for (int i=0;i<replies.length;i++) {
                replies[i]=new Reply(); client.get("/api/mobile/v1/me",true,replies[i]);
            }
            for (Reply reply:replies) assertNotNull(reply.await());
            assertTrue("Rotation must replace refresh", !oldRefresh.equals(client.getRefreshToken()));
            String rotated = client.getRefreshToken();
            Future<Boolean> first=refreshers.submit(()->client.refreshAccessTokenBlocking("invalid-access-test-only"));
            Future<Boolean> second=refreshers.submit(()->client.refreshAccessTokenBlocking("invalid-access-test-only"));
            assertTrue(first.get(30,TimeUnit.SECONDS)); assertTrue(second.get(30,TimeUnit.SECONDS));
            assertTrue("Late REST/SignalR failures must not rotate again",rotated.equals(client.getRefreshToken()));
            SecureTokenStore reopened=new SecureTokenStore(ctx,id+"_secure",id+"_legacy",id+".v1",()->{});
            assertTrue(reopened.getRefreshToken().equals(rotated));
            post(client,"/api/mobile/v1/auth/logout",new JSONObject().put("refreshToken",rotated));
            client.clearSession();
            assertFalse(ctx.getSharedPreferences(id+"_secure",0).contains("auth_blob"));
            assertTrue(client.saveTokenResponse(post(client,"/api/mobile/v1/auth/login",credentials)));
            assertTrue(client.hasSession());
        } finally {
            try {
                if (client.hasSession()) post(client,"/api/mobile/v1/auth/logout",new JSONObject().put("refreshToken",client.getRefreshToken()));
            } finally {
                client.clearSession(); refreshers.shutdownNow();
                ctx.getSharedPreferences(id+"_secure",0).edit().clear().commit();
                ctx.getSharedPreferences(id+"_legacy",0).edit().clear().commit();
            }
        }
    }
}
