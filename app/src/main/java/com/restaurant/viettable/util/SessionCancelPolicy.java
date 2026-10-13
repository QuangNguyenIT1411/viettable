package com.restaurant.viettable.util;

import com.restaurant.viettable.model.DiningSession;

/** UI hint for the "Hủy bàn" button; the backend re-checks every condition before cancelling. */
public final class SessionCancelPolicy {
    private SessionCancelPolicy() {}

    public static boolean canCancel(DiningSession.Status status, int orderRounds, boolean fromReservation) {
        return status == DiningSession.Status.ACTIVE && orderRounds == 0 && !fromReservation;
    }
}
