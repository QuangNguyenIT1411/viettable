package com.restaurant.viettable.network;

import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import com.restaurant.viettable.BuildConfig;

import org.json.JSONObject;
import org.json.JSONTokener;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public final class ApiClient {
    private static final String GOOGLE_AUTH_PATH = "/api/mobile/v1/auth/google";
    private static final String GOOGLE_AUTH_TAG = "GoogleSignIn";
    public interface Callback {
        void onSuccess(Object body);
        void onError(ApiException error);
    }

    private static ApiClient instance;

    private final SecureTokenStore tokens;
    private final Runnable invalidateAppSession;
    private volatile Runnable sessionInvalidatedListener;
    private final ExecutorService executor = Executors.newFixedThreadPool(4);
    // Anonymous Catalog reads must not queue behind slow history/auth refresh requests.
    private final ExecutorService publicExecutor = Executors.newFixedThreadPool(2);
    private final Handler mainHandler = new Handler(Looper.getMainLooper());

    private ApiClient(Context context) {
        invalidateAppSession = () -> com.restaurant.viettable.data.MobileRepository.getInstance().clearLocalSession();
        tokens = new SecureTokenStore(context.getApplicationContext(), this::notifySessionInvalidated);
    }

    /** Isolated client for instrumentation; never replaces the application singleton/session. */
    ApiClient(SecureTokenStore tokens) {
        this.tokens = tokens;
        invalidateAppSession = () -> { };
    }

    public static synchronized void initialize(Context context) {
        if (instance == null) instance = new ApiClient(context);
    }

    public static ApiClient getInstance() {
        if (instance == null) throw new IllegalStateException("ApiClient chưa được khởi tạo.");
        return instance;
    }

    public boolean hasSession() {
        return tokens.hasSession();
    }

    public String getAccessToken() { return tokens.getAccessToken(); }

    public String getRefreshToken() { return tokens.getRefreshToken(); }

    /** Uses the same synchronized refresh-token rotation as authenticated REST requests. */
    public boolean refreshAccessTokenBlocking(String rejectedToken) { return refreshBlocking(rejectedToken); }

    public void setSessionInvalidatedListener(Runnable listener) { sessionInvalidatedListener = listener; }

    private void notifySessionInvalidated() {
        mainHandler.post(() -> {
            if (hasSession()) return;
            invalidateAppSession.run();
            Runnable listener = sessionInvalidatedListener;
            if (listener != null) listener.run();
        });
    }

    public String absoluteUrl(String path) {
        if (path == null || path.isEmpty()) return "";
        if (path.startsWith("http://") || path.startsWith("https://")) return path;
        return BuildConfig.API_BASE_URL.replaceAll("/$", "") + (path.startsWith("/") ? path : "/" + path);
    }

    public synchronized boolean saveTokenResponse(JSONObject response) {
        return tokens.save(response.optString("accessToken"), response.optString("refreshToken"));
    }

    public synchronized void clearSession() {
        tokens.clear();
    }

    public void get(String path, boolean authenticated, Callback callback) {
        request("GET", path, null, authenticated, null, callback);
    }

    public void post(String path, JSONObject body, boolean authenticated, String idempotencyKey, Callback callback) {
        request("POST", path, body, authenticated, idempotencyKey, callback);
    }

    public void put(String path, JSONObject body, boolean authenticated, String idempotencyKey, Callback callback) {
        request("PUT", path, body, authenticated, idempotencyKey, callback);
    }

    public void delete(String path, boolean authenticated, Callback callback) {
        request("DELETE", path, null, authenticated, null, callback);
    }

    private void request(String method, String path, JSONObject body, boolean authenticated,
                         String idempotencyKey, Callback callback) {
        boolean googleAuthRequest = BuildConfig.DEBUG && "POST".equals(method) && GOOGLE_AUTH_PATH.equals(path);
        if (googleAuthRequest) Log.d(GOOGLE_AUTH_TAG, "Gateway auth request queued; method=POST path=" + GOOGLE_AUTH_PATH);
        boolean publicRead = !authenticated && "GET".equals(method);
        (publicRead ? publicExecutor : executor).execute(() -> {
            try {
                String attemptedToken = authenticated ? getAccessToken() : "";
                try {
                    Object result = perform(method, path, body, authenticated, idempotencyKey, attemptedToken);
                    mainHandler.post(() -> callback.onSuccess(result));
                } catch (ApiException first) {
                    if (authenticated && first.getStatusCode() == 401 && refreshBlocking(attemptedToken)) {
                        Object result = perform(method, path, body, true, idempotencyKey);
                        mainHandler.post(() -> callback.onSuccess(result));
                    } else {
                        throw first;
                    }
                }
            } catch (ApiException error) {
                if (googleAuthRequest) {
                    Log.w(GOOGLE_AUTH_TAG, "Gateway auth returned HTTP/API error; status=" + error.getStatusCode()
                            + "; code=" + error.getCode());
                }
                mainHandler.post(() -> callback.onError(error));
            } catch (Exception error) {
                if (BuildConfig.DEBUG) {
                    Log.w("VietTableApi", "Request transport/parse failure; exception="
                            + error.getClass().getSimpleName());
                }
                if (googleAuthRequest) {
                    Log.e(GOOGLE_AUTH_TAG, "Gateway auth request failed before a classified API response; exception="
                            + error.getClass().getName());
                }
                ApiException wrapped = ApiFailureMapper.fromRequestFailure(error);
                mainHandler.post(() -> callback.onError(wrapped));
            }
        });
    }

    private Object perform(String method, String path, JSONObject body, boolean authenticated,
                           String idempotencyKey) throws Exception {
        return perform(method, path, body, authenticated, idempotencyKey, authenticated ? getAccessToken() : "");
    }

    private Object perform(String method, String path, JSONObject body, boolean authenticated,
                           String idempotencyKey, String token) throws Exception {
        URL target = new URL(absoluteUrl(path));
        URL base = new URL(BuildConfig.API_BASE_URL);
        if (!target.getProtocol().equals(base.getProtocol()) || !target.getAuthority().equals(base.getAuthority()))
            throw new ApiException(0, "untrusted_endpoint", "Địa chỉ API không hợp lệ.", null);
        if (!BuildConfig.DEBUG && !"https".equals(target.getProtocol()))
            throw new ApiException(0, "https_required", "Máy chủ cần kết nối HTTPS.", null);
        HttpURLConnection connection = (HttpURLConnection) target.openConnection();
        connection.setInstanceFollowRedirects(false);
        connection.setRequestMethod(method);
        connection.setConnectTimeout(10_000);
        connection.setReadTimeout(BuildConfig.API_READ_TIMEOUT_MS);
        connection.setRequestProperty("Accept", "application/json");
        connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
        connection.setRequestProperty("X-Correlation-Id", "android-" + UUID.randomUUID());
        boolean catalogTiming = "GET".equals(method) && path.matches("/api/mobile/v1/branches(?:/[0-9]+/menu)?");
        long requestStarted = android.os.SystemClock.elapsedRealtime();
        if (authenticated) {
            if (!token.isEmpty()) connection.setRequestProperty("Authorization", "Bearer " + token);
        }
        if (idempotencyKey != null) connection.setRequestProperty("Idempotency-Key", idempotencyKey);
        if (body != null) {
            connection.setDoOutput(true);
            byte[] bytes = body.toString().getBytes(StandardCharsets.UTF_8);
            try (OutputStream stream = connection.getOutputStream()) { stream.write(bytes); }
        }

        int status = connection.getResponseCode();
        long headersAt = android.os.SystemClock.elapsedRealtime();
        if (BuildConfig.DEBUG) {
            Log.d("VietTableApi", "HTTP response; host=" + target.getHost()
                    + "; method=" + method + "; status=" + status);
        }
        if (BuildConfig.DEBUG && "POST".equals(method) && GOOGLE_AUTH_PATH.equals(path)) {
            Log.d(GOOGLE_AUTH_TAG, "Gateway auth HTTP response received; status=" + status);
        }
        String text = read(status >= 200 && status < 300 ? connection.getInputStream() : connection.getErrorStream());
        connection.disconnect();
        Object parsed = text.isEmpty() ? new JSONObject() : new JSONTokener(text).nextValue();
        if (catalogTiming) Log.i("CatalogPerf", "http path=" + path + " status=" + status
                + " ttfbMs=" + (headersAt - requestStarted) + " totalMs="
                + (android.os.SystemClock.elapsedRealtime() - requestStarted) + " chars=" + text.length());
        if (status < 200 || status >= 300) {
            JSONObject error = parsed instanceof JSONObject ? (JSONObject) parsed : new JSONObject();
            throw new ApiException(status, error.optString("code", "request_failed"),
                    error.optString("message", "Yêu cầu không thành công."), error.optString("correlationId", null));
        }
        return parsed;
    }

    private synchronized boolean refreshBlocking(String rejectedToken) {
        String currentAccess = getAccessToken();
        if (!currentAccess.isEmpty() && !currentAccess.equals(rejectedToken)) return true;
        String refreshToken = getRefreshToken();
        if (refreshToken.isEmpty()) return false;
        try {
            JSONObject request = new JSONObject().put("refreshToken", refreshToken);
            Object result = perform("POST", "/api/mobile/v1/auth/refresh", request, false, null);
            if (!(result instanceof JSONObject)) return false;
            return saveTokenResponse((JSONObject) result);
        } catch (ApiException error) {
            if (error.getStatusCode() == 401 || error.getStatusCode() == 400) {
                clearSession();
                notifySessionInvalidated();
            }
            return false;
        } catch (Exception ignored) {
            // Connectivity failures do not revoke a valid persisted refresh token.
            return false;
        }
    }

    private static String read(InputStream stream) throws Exception {
        if (stream == null) return "";
        StringBuilder value = new StringBuilder();
        try (BufferedReader reader = new BufferedReader(new InputStreamReader(stream, StandardCharsets.UTF_8))) {
            String line;
            while ((line = reader.readLine()) != null) value.append(line);
        }
        return value.toString();
    }
}
