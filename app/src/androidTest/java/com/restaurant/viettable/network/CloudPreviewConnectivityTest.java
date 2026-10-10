package com.restaurant.viettable.network;

import android.util.Log;
import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;
import com.restaurant.viettable.BuildConfig;
import org.junit.Test;
import org.junit.runner.RunWith;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import static org.junit.Assert.assertEquals;
import static org.junit.Assume.assumeTrue;

/** Read-only device probe: uses normal system TLS, no bypass, credentials or business mutations. */
@RunWith(AndroidJUnit4.class)
public class CloudPreviewConnectivityTest {
    @Test public void realApiClientReadsPublicCloud() throws Exception {
        assumeTrue(BuildConfig.API_BASE_URL.startsWith("https://"));
        ApiClient client = new ApiClient(new SecureTokenStore(
                InstrumentationRegistry.getInstrumentation().getTargetContext(),
                "cloud_probe_session", "cloud_probe_legacy", "cloud_probe_key", () -> { }));
        for (String path : new String[]{"/healthz", "/api/mobile/v1/branches"}) {
            CountDownLatch done = new CountDownLatch(1);
            AtomicReference<ApiException> failure = new AtomicReference<>();
            client.get(path, false, new ApiClient.Callback() {
                public void onSuccess(Object body) { done.countDown(); }
                public void onError(ApiException error) { failure.set(error); done.countDown(); }
            });
            org.junit.Assert.assertTrue("Cloud callback deadline", done.await(75, TimeUnit.SECONDS));
            org.junit.Assert.assertNull("Cloud ApiClient request failed", failure.get());
        }
    }
    @Test public void publicHttpsAndAnonymousAuthOnDevice() throws Exception {
        assumeTrue(BuildConfig.API_BASE_URL.startsWith("https://"));
        assertEquals(200, probe("/healthz", "GET"));
        assertEquals(200, probe("/api/mobile/v1/branches", "GET"));
        assertEquals(401, probe("/api/mobile/v1/me", "GET"));
        // Empty login is rejected validation, not a fake customer/token or persisted mutation.
        assertEquals(400, probe("/api/mobile/v1/auth/login", "POST"));
    }

    private int probe(String path, String method) throws Exception {
        HttpURLConnection connection = (HttpURLConnection) new URL(BuildConfig.API_BASE_URL + path).openConnection();
        connection.setInstanceFollowRedirects(false);
        connection.setConnectTimeout(10_000);
        // Optional diagnostic budget; does not alter ApiClient's production timeout.
        // Read the installed target variant, not debug test compiler's inlined BuildConfig.
        int installedTimeout = InstrumentationRegistry.getInstrumentation().getTargetContext()
                .getClassLoader().loadClass("com.restaurant.viettable.BuildConfig")
                .getField("API_READ_TIMEOUT_MS").getInt(null);
        int readTimeout = Integer.parseInt(InstrumentationRegistry.getArguments()
                .getString("cloudReadTimeoutMs", String.valueOf(installedTimeout)));
        if (readTimeout < 20_000 || readTimeout > 60_000) throw new IllegalArgumentException("Invalid diagnostic timeout");
        connection.setReadTimeout(readTimeout);
        connection.setRequestMethod(method);
        connection.setRequestProperty("Accept", "application/json");
        if ("POST".equals(method)) {
            connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            connection.setDoOutput(true);
        }
        long started = System.nanoTime();
        String phase = "connect_tls";
        try {
            connection.connect();
            Log.i("VietTableCloudProbe", "System TLS connected; elapsedMs="
                    + (System.nanoTime() - started) / 1_000_000);
            phase = "response";
            if ("POST".equals(method)) {
                try (java.io.OutputStream stream = connection.getOutputStream()) {
                    stream.write("{}".getBytes(StandardCharsets.UTF_8));
                }
            }
            int status = connection.getResponseCode();
            Log.i("VietTableCloudProbe", method + " " + path + " status=" + status
                    + " elapsedMs=" + (System.nanoTime() - started) / 1_000_000);
            return status;
        } catch (Exception error) {
            Log.e("VietTableCloudProbe", method + " " + path + " phase=" + phase
                    + " exception=" + error.getClass().getSimpleName()
                    + " elapsedMs=" + (System.nanoTime() - started) / 1_000_000);
            // Avoid printing exception messages/stack traces that could contain sensitive URLs.
            throw new AssertionError("Cloud device probe failed: " + error.getClass().getSimpleName());
        } finally {
            connection.disconnect();
        }
    }
}
