package com.restaurant.viettable.util;

import static org.junit.Assert.assertEquals;

import com.restaurant.viettable.model.DiningSessionHistoryGroup;
import com.restaurant.viettable.model.DiningSessionSummary;
import com.restaurant.viettable.model.Order;

import org.junit.Test;

import java.util.Arrays;
import java.util.List;

public class DiningSessionHistoryGrouperTest {
    @Test
    public void groupsRoundsBySessionAndKeepsSessionsSeparate() {
        Order firstRound = order("11", "501", "2026-09-26T08:00:00Z", 12000);
        Order secondRound = order("12", "501", "2026-09-26T08:10:00Z", 8000);
        Order anotherSession = order("13", "502", "2026-09-26T08:20:00Z", 9000);

        List<DiningSessionHistoryGroup> groups = DiningSessionHistoryGrouper.group(
                Arrays.asList(anotherSession, secondRound, firstRound));

        assertEquals(2, groups.size());
        assertEquals("502", groups.get(0).getSession().getId());
        assertEquals(1, groups.get(0).getOrders().size());
        assertEquals("501", groups.get(1).getSession().getId());
        assertEquals(2, groups.get(1).getOrders().size());
        assertEquals("11", groups.get(1).getOrders().get(0).getId());
        assertEquals(20000, groups.get(1).getTotalAmount());
    }

    @Test
    public void sortsActiveSessionRoundsChronologicallyWithoutMutatingSourceList() {
        Order firstRound = order("11", "501", "2026-09-26T08:00:00Z", 12000);
        Order secondRound = order("12", "501", "2026-09-26T08:10:00Z", 8000);
        List<Order> apiOrder = Arrays.asList(secondRound, firstRound);

        List<Order> rounds = DiningSessionHistoryGrouper.sortChronologically(apiOrder);

        assertEquals("11", rounds.get(0).getId());
        assertEquals("12", rounds.get(1).getId());
        assertEquals("12", apiOrder.get(0).getId());
    }

    private static Order order(String orderId, String sessionId, String time, long amount) {
        Order order = new Order(orderId, sessionId, "Chi nhánh Q1", "Bàn 5", Order.Type.DINE_IN,
                Order.Status.COMPLETED, time, null, amount, "");
        order.setDiningSessionSummary(new DiningSessionSummary(sessionId, "SES-" + sessionId, "1",
                "5", "CLOSED", "2026-09-26T07:50:00Z", "2026-09-26T08:30:00Z"));
        return order;
    }
}
