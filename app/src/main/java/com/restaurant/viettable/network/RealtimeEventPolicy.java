package com.restaurant.viettable.network;

import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Set;

/** Pure filtering/deduplication rules shared by SignalR runtime code and unit tests. */
public final class RealtimeEventPolicy {
    private RealtimeEventPolicy() {}

    public static boolean appliesToOrder(int activeSessionId, Set<Integer> currentOrderIds,
                                         Integer eventSessionId, Integer eventOrderId) {
        if (activeSessionId <= 0) return false;
        if (eventSessionId != null && eventSessionId > 0) return eventSessionId == activeSessionId;
        return eventOrderId != null && eventOrderId > 0 && currentOrderIds.contains(eventOrderId);
    }

    public static boolean appliesToService(int activeSessionId, Integer eventSessionId) {
        return activeSessionId > 0 && eventSessionId != null && eventSessionId == activeSessionId;
    }

    public static boolean shouldFallbackPoll(SignalRManager.ConnectionState state) {
        return state != SignalRManager.ConnectionState.CONNECTED;
    }

    public static final class Deduplicator {
        private final long windowMs;
        private final Map<String, Long> seen = new LinkedHashMap<String, Long>() {
            @Override protected boolean removeEldestEntry(Map.Entry<String, Long> eldest) {
                return size() > 64;
            }
        };

        public Deduplicator(long windowMs) { this.windowMs = windowMs; }

        public synchronized boolean shouldProcess(String signature, long nowMs) {
            Long previous = seen.put(signature, nowMs);
            return previous == null || nowMs - previous >= windowMs;
        }

        public synchronized void clear() { seen.clear(); }
    }
}
