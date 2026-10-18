package com.restaurant.viettable.util;

import com.restaurant.viettable.model.DiningSession;
import com.restaurant.viettable.model.Order;

import java.util.List;

/** UI hint for the "Hủy bàn" button; the backend re-checks every condition before cancelling. */
public final class SessionCancelPolicy {
    private SessionCancelPolicy() {}

    public static boolean canCancel(DiningSession.Status status, int orderRounds, boolean fromReservation) {
        return status == DiningSession.Status.ACTIVE && orderRounds == 0 && !fromReservation;
    }

    /** Order rounds that still count: rounds the kitchen cancelled are as if nothing was ordered. */
    public static int liveRounds(List<Order> orders) {
        int count = 0;
        if (orders != null) for (Order order : orders) if (order != null && order.getStatus() != Order.Status.CANCELLED) count++;
        return count;
    }
}
