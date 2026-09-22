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
    private boolean desired;
    private boolean startInProgress;
    private boolean stopInProgress;
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
        main.post(() -> listener.onConnectionStateChanged(state));
    }

    public void removeListener(Listener listener) { listeners.remove(listener); }

    public synchronized void startIfAuthenticated() {
        if (!initialized || !ApiClient.getInstance().hasSession()) return;
        desired = true;
        main.removeCallbacks(reconnectRunnable);
        if (state == ConnectionState.CONNECTED || startInProgress || stopInProgress) return;
        startConnection(retryAttempt > 0);
    }

    public synchronized void pause() {
        desired = false;
        main.removeCallbacks(reconnectRunnable);
        stopHub(false);
    }

    public synchronized void stopAndClear() {
        desired = false;
        retryAttempt = 0;
        authRefreshAttempted = false;
        main.removeCallbacks(reconnectRunnable);
        deduplicator.clear();
        stopHub(true);
    }

    private synchronized void ensureHub() {
        if (hub != null) return;
        hub = HubConnectionBuilder.create(ApiClient.getInstance().absoluteUrl("/hubs/mobile-notifications"))
                .withAccessTokenProvider(Single.defer(() -> {
                    attemptedAccessToken = ApiClient.getInstance().getAccessToken();
                    return Single.just(attemptedAccessToken);
                }))
                .build();
        hub.on("orderUpdated", this::handleOrderUpdated, OrderUpdatedEvent.class);
        hub.on("serviceRequestUpdated", this::handleServiceRequestUpdated,
                ServiceRequestUpdatedEvent.class);
        hub.onClosed(error -> main.post(() -> handleClosed(error)));
    }

    private synchronized void startConnection(boolean reconnecting) {
        if (!desired || startInProgress || stopInProgress || !ApiClient.getInstance().hasSession()) return;
        ensureHub();
        if (hub.getConnectionState() == HubConnectionState.CONNECTED) {
            changeState(ConnectionState.CONNECTED);
            return;
        }
        startInProgress = true;
        changeState(reconnecting ? ConnectionState.RECONNECTING : ConnectionState.CONNECTING);
        hub.start().subscribe(
                () -> main.post(this::handleConnected),
                error -> main.post(() -> handleStartFailure(error)));
    }

    private synchronized void handleConnected() {
        startInProgress = false;
        retryAttempt = 0;
        authRefreshAttempted = false;
        changeState(ConnectionState.CONNECTED);
        Log.i(TAG, "connected");
        resyncCanonicalState();
    }

    private synchronized void handleStartFailure(Throwable error) {
        startInProgress = false;
        if (!desired) return;
        String message = error == null ? "" : String.valueOf(error.getMessage());
        Log.w(TAG, "connection failed: " + safeFailure(message));
        if (!authRefreshAttempted && isAuthenticationFailure(message)) {
            authRefreshAttempted = true;
            authExecutor.execute(() -> {
                boolean refreshed = ApiClient.getInstance().refreshAccessTokenBlocking(attemptedAccessToken);
                main.post(() -> {
                    if (refreshed && desired) startConnection(true);
                    else scheduleReconnect();
                });
            });
            return;
        }
        scheduleReconnect();
    }

    private synchronized void handleClosed(Exception error) {
        startInProgress = false;
        if (stopInProgress) return;
        if (!desired) {
            changeState(ConnectionState.STOPPED);
            return;
        }
        Log.w(TAG, "disconnected: " + safeFailure(error == null ? "closed" : error.getMessage()));
        scheduleReconnect();
    }

    private synchronized void scheduleReconnect() {
        if (!desired) return;
        changeState(ConnectionState.RECONNECTING);
        main.removeCallbacks(reconnectRunnable);
        long delay = RETRY_DELAYS_MS[Math.min(retryAttempt, RETRY_DELAYS_MS.length - 1)];
        retryAttempt++;
        main.postDelayed(reconnectRunnable, delay);
    }

    private synchronized void stopHub(boolean discard) {
        main.removeCallbacks(reconnectRunnable);
        startInProgress = false;
        HubConnection current = hub;
        if (discard) hub = null;
        if (current == null || current.getConnectionState() == HubConnectionState.DISCONNECTED) {
            stopInProgress = false;
            changeState(ConnectionState.STOPPED);
            return;
        }
        stopInProgress = true;
        current.stop().subscribe(
                () -> main.post(() -> {
                    stopInProgress = false;
                    changeState(ConnectionState.STOPPED);
                    if (desired) startIfAuthenticated();
                }),
                error -> main.post(() -> {
                    stopInProgress = false;
                    changeState(ConnectionState.STOPPED);
                    if (desired) startIfAuthenticated();
                }));
    }

    private void handleOrderUpdated(OrderUpdatedEvent event) {
        main.post(() -> {
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

    private void handleServiceRequestUpdated(ServiceRequestUpdatedEvent event) {
        main.post(() -> {
            DiningSession session = AppStateManager.getInstance().getActiveDiningSession();
            int sessionId = parseId(session == null ? null : session.getId());
            if (!RealtimeEventPolicy.appliesToService(sessionId, event.diningSessionId)) return;
            String signature = "service:" + event.serviceRequestId + ":" + event.status + ":" + event.updatedAtUtc;
            if (!deduplicator.shouldProcess(signature, System.currentTimeMillis())) return;
            Log.i(TAG, "serviceRequestUpdated requestId=" + event.serviceRequestId);
            refreshServiceCanonical();
        });
    }

    private void resyncCanonicalState() {
        MobileRepository.getInstance().loadActiveSession(new MobileRepository.Callback<DiningSession>() {
            @Override public void onSuccess(DiningSession value) {
                if (value != null) refreshServiceCanonical();
            }
            @Override public void onError(ApiException error) {
                Log.w(TAG, "REST resync failed: " + error.getCode());
            }
        });
    }

    private void refreshOrdersCanonical() {
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
        for (Listener listener : listeners) main.post(() -> listener.onConnectionStateChanged(next));
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
