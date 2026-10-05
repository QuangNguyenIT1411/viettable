package com.restaurant.viettable.util;

import com.restaurant.viettable.model.DiningSessionHistoryGroup;
import com.restaurant.viettable.model.DiningSessionSummary;
import com.restaurant.viettable.model.Order;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class DiningSessionHistoryGrouper {
    private DiningSessionHistoryGrouper() {}

    public static List<DiningSessionHistoryGroup> group(List<Order> orders) {
        Map<String, DiningSessionHistoryGroup> groups = new LinkedHashMap<>();
        if (orders == null) return new ArrayList<>();
        for (Order order : orders) {
            if (order == null) continue;
            DiningSessionSummary summary = order.getDiningSessionSummary();
            String sessionId = order.getDiningSessionId();
            String key = sessionId == null || sessionId.trim().isEmpty() ? "order:" + order.getId() : sessionId;
            DiningSessionHistoryGroup group = groups.get(key);
            if (group == null) {
                String branchName = order.getBranchName();
                group = new DiningSessionHistoryGroup(summary, branchName);
                groups.put(key, group);
            }
            group.getOrders().add(order);
        }
        List<DiningSessionHistoryGroup> result = new ArrayList<>(groups.values());
        for (DiningSessionHistoryGroup group : result) {
            List<Order> sortedOrders = sortChronologically(group.getOrders());
            group.getOrders().clear();
            group.getOrders().addAll(sortedOrders);
        }
        return result;
    }

    public static List<Order> sortChronologically(List<Order> orders) {
        List<Order> sorted = orders == null ? new ArrayList<>() : new ArrayList<>(orders);
        Collections.sort(sorted, Comparator.comparing(
                order -> order == null || order.getOrderTime() == null ? "" : order.getOrderTime()));
        return sorted;
    }
}
