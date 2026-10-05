package com.restaurant.viettable.model;

import java.util.ArrayList;
import java.util.List;

public class DiningSessionHistoryGroup {
    private final DiningSessionSummary session;
    private final String branchName;
    private final List<Order> orders = new ArrayList<>();

    public DiningSessionHistoryGroup(DiningSessionSummary session, String branchName) {
        this.session = session;
        this.branchName = branchName;
    }

    public DiningSessionSummary getSession() { return session; }
    public String getBranchName() { return branchName; }
    public List<Order> getOrders() { return orders; }

    public long getTotalAmount() {
        long total = 0;
        for (Order order : orders) total += order.getTotalAmount();
        return total;
    }
}
