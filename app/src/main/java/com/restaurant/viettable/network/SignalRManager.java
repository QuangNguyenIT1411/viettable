package com.restaurant.viettable.network;

import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import com.microsoft.signalr.HubConnection;
import com.microsoft.signalr.HubConnectionBuilder;
import com.microsoft.signalr.HubConnectionState;
import com.restaurant.viettable.data.AppStateManager;
import com.restaurant.viettable.data.MobileRepository;
import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Order;

import java.util.HashSet;
import java.util.Set;
import java.util.concurrent.CopyOnWriteArraySet;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

import io.reactivex.rxjava3.core.Single;

/** One authenticated, app-scoped connection to the customer notification Hub. */
public final class SignalRManager {
    public enum ConnectionState { STOPPED, CONNECTING, CONNECTED, RECONNECTING, DISCONNECTED }
    public interface Listener { void onConnectionStateChanged(ConnectionState state); }

    public static final class OrderUpdatedEvent {
        public Integer orderId;
        public Integer diningSessionId;
        public String diningSessionCode;
        public String status;
        public String statusCode;
        public OrderItemEvent[] items;
    }

    public static final class OrderItemEvent {
        public Integer itemId;
        public String status;
    }

    public static final class ServiceRequestUpdatedEvent {
        public Integer serviceRequestId;
        public Integer diningSessionId;
        public String status;
        public String updatedAtUtc;
    }

    private static final String TAG = "VietTableSignalR";
    private static final long[] RETRY_DELAYS_MS = { 2_000L, 5_000L, 10_000L, 30_000L };
    private static final SignalRManager INSTANCE = new SignalRManager();

    private final Handler main = new Handler(Looper.getMainLooper());
    private final ExecutorService authExecutor = Executors.newSingleThreadExecutor();
    private final Set<Listener> listeners = new CopyOnWriteArraySet<>();
    private final RealtimeEventPolicy.Deduplicator deduplicator =
            new RealtimeEventPolicy.Deduplicator(1_500L);
    private final Runnable reconnectRunnable = () -> startConnection(true);

    private HubConnection hub;
    private volatile ConnectionState state = ConnectionState.STOPPED;
    private boolean initialized;
    private boolean foreground;
    private boolean desired;
    private boolean startInProgress;
    private boolean stopInProgress;
    private boolean stopAfterStart;
    private boolean authRefreshInProgress;
    private long sessionGeneration;
    private boolean authRefreshAttempted;
    private boolean orderRefreshInFlight;
    private boolean orderRefreshPending;
    private boolean serviceRefreshInFlight;
    private boolean serviceRefreshPending;
    private volatile String attemptedAccessToken = "";
    private int retryAttempt;

    public static SignalRManager getInstance() { return INSTANCE; }
    private SignalRManager() {}

    public synchronized void initialize() { initialized = true; }
    public ConnectionState getState() { return state; }
    public boolean isConnected() { return state == ConnectionState.CONNECTED; }

    public void addListener(Listener listener) {
        listeners.add(listener);
        main.post(() -> { if (listeners.contains(listener)) listener.onConnectionStateChanged(state); });
    }

    public void removeListener(Listener listener) { listeners.remove(listener); }

    public synchronized void resume() {
        foreground = true;
        startIfAuthenticated();
    }

    public synchronized void startIfAuthenticated() {
        if (!initialized || !foreground || !ApiClient.getInstance().hasSession()) return;
        desired = true;
        if (state == ConnectionState.CONNECTED || startInProgress || stopInProgress
                || authRefreshInProgress || state == ConnectionState.RECONNECTING) return;
        startConnection(retryAttempt > 0);
    }

    public synchronized void pause() {
        foreground = false;
        desired = false;
        main.removeCallbacks(reconnectRunnable);
        stopHub();
    }

    public synchronized void stopAndClear() {
        desired = false;
        sessionGeneration++;
        retryAttempt = 0;
        authRefreshAttempted = false;
        main.removeCallbacks(reconnectRunnable);
        deduplicator.clear();
        stopHub();
    }

    private synchronized void ensureHub() {
        if (hub != null) return;
        hub = HubConnectionBuilder.create(ApiClient.getInstance().absoluteUrl("/hubs/mobile-notifications"))
                .withAccessTokenProvider(Single.defer(() -> {
                    attemptedAccessToken = ApiClient.getInstance().getAccessToken();
                    return Single.just(attemptedAccessToken);
                }))
                .build();
        HubConnection current = hub;
        current.on("orderUpdated", event -> handleOrderUpdated(current, event), OrderUpdatedEvent.class);
        current.on("serviceRequestUpdated", event -> handleServiceRequestUpdated(current, event),
                ServiceRequestUpdatedEvent.class);
        current.onClosed(error -> main.post(() -> handleClosed(current, error)));
    }

    private synchronized void startConnection(boolean reconnecting) {
        if (!desired || startInProgress || stopInProgress || authRefreshInProgress
                || !ApiClient.getInstance().hasSession()) return;
        main.removeCallbacks(reconnectRunnable);
        ensureHub();
        if (hub.getConnectionState() == HubConnectionState.CONNECTED) {
            changeState(ConnectionState.CONNECTED);
            return;
        }
        startInProgress = true;
        HubConnection current = hub;
        long generation = sessionGeneration;
        changeState(reconnecting ? ConnectionState.RECONNECTING : ConnectionState.CONNECTING);
        current.start().subscribe(
                () -> main.post(() -> handleConnected(current)),
                error -> main.post(() -> handleStartFailure(current, generation, error)));
    }

    private synchronized void handleConnected(HubConnection current) {
        if (hub != current) return;
        startInProgress = false;
        if (stopAfterStart || !desired || !ApiClient.getInstance().hasSession()) {
            stopHub();
            return;
        }
        if (current.getConnectionState() != HubConnectionState.CONNECTED) {
            handleClosed(current, null);
            return;
        }
        retryAttempt = 0;
        authRefreshAttempted = false;
        changeState(ConnectionState.CONNECTED);
        Log.i(TAG, "connected");
        resyncCanonicalState(current);
    }

    private synchronized void handleStartFailure(HubConnection current, long generation, Throwable error) {
        if (hub != current) return;
        startInProgress = false;
        stopAfterStart = false;
        hub = null;
        if (!desired) { changeState(ConnectionState.STOPPED); return; }
        if (generation != sessionGeneration) {
            changeState(ConnectionState.STOPPED);
            startIfAuthenticated();
            return;
        }
        String message = error == null ? "" : String.valueOf(error.getMessage());
        Log.w(TAG, "connection failed: " + safeFailure(message));
        if (!authRefreshAttempted && isAuthenticationFailure(message)) {
            authRefreshAttempted = true;
            authRefreshInProgress = true;
            String rejectedToken = attemptedAccessToken;
            authExecutor.execute(() -> {
                boolean refreshed = ApiClient.getInstance().refreshAccessTokenBlocking(rejectedToken);
                main.post(() -> {
                    synchronized (SignalRManager.this) {
                        authRefreshInProgress = false;
                        if (!desired) return;
                        if (generation != sessionGeneration) {
                            changeState(ConnectionState.STOPPED);
                            startIfAuthenticated();
                        } else if (refreshed) startConnection(true);
                        else scheduleReconnect();
                    }
                });
            });
            return;
        }
        scheduleReconnect();
    }

    private synchronized void handleClosed(HubConnection current, Exception error) {
        // start()/stop() subscriptions own their terminal transition. An old Hub
        // must never clear the flags or reconnect a newer authenticated session.
        if (hub != current || startInProgress || stopInProgress) return;
        hub = null;
        if (!desired) {
            changeState(ConnectionState.STOPPED);
            return;
        }
        Log.w(TAG, "disconnected: " + safeFailure(error == null ? "closed" : error.getMessage()));
        scheduleReconnect();
    }

    private synchronized void scheduleReconnect() {
        if (!desired || !ApiClient.getInstance().hasSession()) {
            changeState(ConnectionState.STOPPED);
            return;
        }
        changeState(ConnectionState.RECONNECTING);
        main.removeCallbacks(reconnectRunnable);
        long delay = RETRY_DELAYS_MS[Math.min(retryAttempt, RETRY_DELAYS_MS.length - 1)];
        retryAttempt++;
        main.postDelayed(reconnectRunnable, delay);
    }

    private synchronized void stopHub() {
        main.removeCallbacks(reconnectRunnable);
        if (startInProgress) {
            // SignalR 8.0.30 stop-during-negotiate dereferences a null transport
            // when negotiate fails. Drain start first; never overlap start/stop.
            stopAfterStart = true;
            changeState(ConnectionState.STOPPED);
            return;
        }
        if (stopInProgress) return;
        HubConnection current = hub;
        stopAfterStart = false;
        if (current == null || current.getConnectionState() == HubConnectionState.DISCONNECTED) {
            hub = null;
            changeState(ConnectionState.STOPPED);
            if (desired) startIfAuthenticated();
            return;
        }
        stopInProgress = true;
        current.stop().subscribe(
                () -> main.post(() -> finishStop(current)),
                error -> main.post(() -> {
                    Log.w(TAG, "stop failed: " + safeFailure(error.getMessage()));
                    finishStop(current);
                }));
    }

    private synchronized void finishStop(HubConnection current) {
        if (hub != current) return;
        hub = null;
        stopInProgress = false;
        changeState(ConnectionState.STOPPED);
        if (desired) startIfAuthenticated();
    }

    private synchronized boolean acceptsEvents(HubConnection current) {
        return desired && hub == current && state == ConnectionState.CONNECTED;
    }

    private void handleOrderUpdated(HubConnection current, OrderUpdatedEvent event) {
        main.post(() -> {
            if (!acceptsEvents(current) || event == null) return;
            DiningSession session = AppStateManager.getInstance().getActiveDiningSession();
            int sessionId = parseId(session == null ? null : session.getId());
            Set<Integer> orderIds = new HashSet<>();
            if (session != null) for (Order order : session.getOrders())
                orderIds.add(parseId(order.getId()));
            if (!RealtimeEventPolicy.appliesToOrder(sessionId, orderIds,
                    event.diningSessionId, event.orderId)) return;
            String signature = "order:" + event.orderId + ":" + event.status + ":" +
                    event.statusCode + ":" + itemSignature(event.items);
            if (!deduplicator.shouldProcess(signature, System.currentTimeMillis())) return;
            Log.i(TAG, "orderUpdated orderId=" + event.orderId);
            refreshOrdersCanonical();
        });
    }

    private void handleServiceRequestUpdated(HubConnection current, ServiceRequestUpdatedEvent event) {
        main.post(() -> {
            if (!acceptsEvents(current) || event == null) return;
            DiningSession session = AppStateManager.getInstance().getActiveDiningSession();
            int sessionId = parseId(session == null ? null : session.getId());
            if (!RealtimeEventPolicy.appliesToService(sessionId, event.diningSessionId)) return;
            String signature = "service:" + event.serviceRequestId + ":" + event.status + ":" + event.updatedAtUtc;
            if (!deduplicator.shouldProcess(signature, System.currentTimeMillis())) return;
            Log.i(TAG, "serviceRequestUpdated requestId=" + event.serviceRequestId);
            refreshServiceCanonical();
        });
    }

    private void resyncCanonicalState(HubConnection current) {
        MobileRepository.getInstance().loadActiveSession(new MobileRepository.Callback<DiningSession>() {
            @Override public void onSuccess(DiningSession value) {
                if (!acceptsEvents(current)) return;
                Log.i(TAG, "REST resync complete");
                if (value != null) refreshServiceCanonical();
            }
            @Override public void onError(ApiException error) {
                Log.w(TAG, "REST resync failed: " + error.getCode());
            }
        });
    }

    private void refreshOrdersCanonical() {
        if (!desired || !ApiClient.getInstance().hasSession()) return;
        if (orderRefreshInFlight) { orderRefreshPending = true; return; }
        orderRefreshInFlight = true;
        MobileRepository.getInstance().loadOrders("active", new MobileRepository.Callback<java.util.List<Order>>() {
            @Override public void onSuccess(java.util.List<Order> value) { finishOrderRefresh(); }
            @Override public void onError(ApiException error) { finishOrderRefresh(); }
        });
    }

    private void finishOrderRefresh() {
        orderRefreshInFlight = false;
        if (orderRefreshPending) { orderRefreshPending = false; refreshOrdersCanonical(); }
    }

    private void refreshServiceCanonical() {
        if (!desired || !ApiClient.getInstance().hasSession()) return;
        if (serviceRefreshInFlight) { serviceRefreshPending = true; return; }
        if (AppStateManager.getInstance().getActiveDiningSession() == null) return;
        serviceRefreshInFlight = true;
        MobileRepository.getInstance().loadServiceRequests(new MobileRepository.Callback<java.util.List<com.restaurant.viettable.model.ServiceRequest>>() {
            @Override public void onSuccess(java.util.List<com.restaurant.viettable.model.ServiceRequest> value) { finishServiceRefresh(); }
            @Override public void onError(ApiException error) { finishServiceRefresh(); }
        });
    }

    private void finishServiceRefresh() {
        serviceRefreshInFlight = false;
        if (serviceRefreshPending) { serviceRefreshPending = false; refreshServiceCanonical(); }
    }

    private void changeState(ConnectionState next) {
        state = next;
        for (Listener listener : listeners) main.post(() -> {
            if (listeners.contains(listener)) listener.onConnectionStateChanged(state);
        });
    }

    private static int parseId(String value) {
        try { return Integer.parseInt(value); } catch (Exception ignored) { return 0; }
    }

    private static String itemSignature(OrderItemEvent[] items) {
        if (items == null) return "";
        StringBuilder result = new StringBuilder();
        for (OrderItemEvent item : items) result.append(item.itemId).append('=').append(item.status).append(';');
        return result.toString();
    }

    private static boolean isAuthenticationFailure(String message) {
        String lower = message == null ? "" : message.toLowerCase(java.util.Locale.ROOT);
        return lower.contains("401") || lower.contains("unauthorized");
    }

    private static String safeFailure(String message) {
        return isAuthenticationFailure(message) ? "authentication rejected" : "transport unavailable";
    }
}
