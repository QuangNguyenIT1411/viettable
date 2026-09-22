package com.restaurant.viettable.network;

import org.junit.Test;

import java.util.Arrays;
import java.util.HashSet;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class RealtimeEventPolicyTest {
    @Test public void orderEventMustBelongToCurrentSessionOrKnownOrder() {
        HashSet<Integer> currentOrders = new HashSet<>(Arrays.asList(21, 22));
        assertTrue(RealtimeEventPolicy.appliesToOrder(7, currentOrders, 7, 99));
        assertTrue(RealtimeEventPolicy.appliesToOrder(7, currentOrders, null, 21));
        assertFalse(RealtimeEventPolicy.appliesToOrder(7, currentOrders, 8, 21));
        assertFalse(RealtimeEventPolicy.appliesToOrder(7, currentOrders, null, 99));
        assertFalse(RealtimeEventPolicy.appliesToOrder(0, currentOrders, 7, 21));
    }

    @Test public void serviceEventMustBelongToCurrentSession() {
        assertTrue(RealtimeEventPolicy.appliesToService(7, 7));
        assertFalse(RealtimeEventPolicy.appliesToService(7, 8));
        assertFalse(RealtimeEventPolicy.appliesToService(7, null));
    }

    @Test public void duplicateSignatureIsSuppressedOnlyInsideWindow() {
        RealtimeEventPolicy.Deduplicator deduplicator = new RealtimeEventPolicy.Deduplicator(1_500);
        assertTrue(deduplicator.shouldProcess("order:1:READY", 1_000));
        assertFalse(deduplicator.shouldProcess("order:1:READY", 2_000));
        assertTrue(deduplicator.shouldProcess("order:1:SERVED", 2_000));
        assertTrue(deduplicator.shouldProcess("order:1:READY", 4_000));
    }

    @Test public void fallbackPollingStopsOnlyWhileConnected() {
        assertFalse(RealtimeEventPolicy.shouldFallbackPoll(SignalRManager.ConnectionState.CONNECTED));
        assertTrue(RealtimeEventPolicy.shouldFallbackPoll(SignalRManager.ConnectionState.RECONNECTING));
        assertTrue(RealtimeEventPolicy.shouldFallbackPoll(SignalRManager.ConnectionState.DISCONNECTED));
    }
}
