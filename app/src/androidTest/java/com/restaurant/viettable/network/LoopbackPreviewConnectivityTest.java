package com.restaurant.viettable.network;

import androidx.test.ext.junit.runners.AndroidJUnit4;
import com.restaurant.viettable.BuildConfig;
import org.json.JSONObject;
import org.junit.Test;
import org.junit.runner.RunWith;

import java.io.InputStream;
import java.io.OutputStream;
import java.io.ByteArrayOutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;
import static org.junit.Assume.assumeTrue;

/** Isolated emulator QA probe for a loopback bridge to a protected Preview. */
@RunWith(AndroidJUnit4.class)
public class LoopbackPreviewConnectivityTest {
    @Test public void catalogAndIdentityReachCandidateBackend() throws Exception {
        assumeTrue(BuildConfig.DEBUG && BuildConfig.API_BASE_URL.equals("http://127.0.0.1:48127"));
        assertEquals(200, request("GET", "/healthz", null).status);
        Probe branches = request("GET", "/api/mobile/v1/branches", null);
        assertEquals(200, branches.status);
        assertTrue(branches.body.startsWith("["));
        assertTrue(branches.body.contains("branchId"));

        // Empty credentials trigger request validation, not account mutation.
        Probe identity = request("POST", "/api/mobile/v1/auth/login", "{}");
        assertEquals(400, identity.status);
        JSONObject error = new JSONObject(identity.body);
        assertFalse("Vercel protection page reached Android", identity.body.contains("Vercel Authentication"));
        assertTrue("Expected Gateway/Identity error contract", error.has("code"));
    }

    private static Probe request(String method, String path, String body) throws Exception {
        HttpURLConnection connection = (HttpURLConnection) new URL(BuildConfig.API_BASE_URL + path).openConnection();
        connection.setInstanceFollowRedirects(false);
        connection.setConnectTimeout(10_000);
        connection.setReadTimeout(60_000);
        connection.setRequestMethod(method);
        connection.setRequestProperty("Accept", "application/json");
        if (body != null) {
            connection.setDoOutput(true);
            connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            try (OutputStream stream = connection.getOutputStream()) {
                stream.write(body.getBytes(StandardCharsets.UTF_8));
            }
        }
        try {
            int status = connection.getResponseCode();
            InputStream stream = status >= 400 ? connection.getErrorStream() : connection.getInputStream();
            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            if (stream != null) {
                byte[] chunk = new byte[4096];
                int length;
                while ((length = stream.read(chunk)) != -1) bytes.write(chunk, 0, length);
                stream.close();
            }
            String payload = bytes.toString(StandardCharsets.UTF_8.name());
            return new Probe(status, payload);
        } finally {
            connection.disconnect();
        }
    }

    private static final class Probe {
        final int status;
        final String body;
        Probe(int status, String body) { this.status = status; this.body = body; }
    }
}
