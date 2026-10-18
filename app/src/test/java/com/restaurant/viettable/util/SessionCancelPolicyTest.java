package com.restaurant.viettable.util;

import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Order;

import java.util.Arrays;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class SessionCancelPolicyTest {
    @Test
    public void allowsActiveWalkInSessionWithoutOrders() {
        assertTrue(SessionCancelPolicy.canCancel(DiningSession.Status.ACTIVE, 0, false));
    }

    @Test
    public void hidesWhenAnyRoundWasSent() {
        assertFalse(SessionCancelPolicy.canCancel(DiningSession.Status.ACTIVE, 1, false));
    }

    @Test
    public void hidesDuringPaymentOrAfterClose() {
        assertFalse(SessionCancelPolicy.canCancel(DiningSession.Status.PAYMENT_REQUESTED, 0, false));
        assertFalse(SessionCancelPolicy.canCancel(DiningSession.Status.CLOSED, 0, false));
        assertFalse(SessionCancelPolicy.canCancel(DiningSession.Status.CANCELLED, 0, false));
    }

    @Test
    public void hidesForReservationSessions() {
        assertFalse(SessionCancelPolicy.canCancel(DiningSession.Status.ACTIVE, 0, true));
    }

    @Test
    public void roundsTheKitchenCancelledDoNotCount() {
        Order cancelled = new Order(); cancelled.setStatus(Order.Status.CANCELLED);
        Order live = new Order(); live.setStatus(Order.Status.IN_PROGRESS);
        assertEquals(0, SessionCancelPolicy.liveRounds(Arrays.asList(cancelled, cancelled)));
        assertTrue(SessionCancelPolicy.canCancel(DiningSession.Status.ACTIVE,
                SessionCancelPolicy.liveRounds(Arrays.asList(cancelled)), false));
        assertEquals(1, SessionCancelPolicy.liveRounds(Arrays.asList(cancelled, live)));
        assertEquals(0, SessionCancelPolicy.liveRounds(null));
    }
}
