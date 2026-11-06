package com.restaurant.viettable.data;

import java.util.HashMap;
import java.util.Map;

/** Catalog display metadata only; never translates IDs used in commands. */
public final class TableNumberDirectory {
    private final Map<String, Map<String, Integer>> branches = new HashMap<>();

    public void put(String branchId, String tableId, int number) {
        if (branchId == null || branchId.isEmpty() || tableId == null || tableId.isEmpty() || number <= 0) return;
        branches.computeIfAbsent(branchId, ignored -> new HashMap<>()).put(tableId, number);
    }

    public String label(String branchId, String tableId) {
        if (tableId == null || tableId.isEmpty() || "0".equals(tableId)) return "Chưa xếp bàn";
        Map<String, Integer> tables = branches.get(branchId);
        Integer number = tables == null ? null : tables.get(tableId);
        return number == null ? "Chưa có thông tin bàn" : "Bàn " + number;
    }

    public void clear() { branches.clear(); }

    /** "Bàn 5 + 6" from "Bàn 5", "Bàn 6"; null when a number is unknown (the caller keeps its single label). */
    public static String groupLabel(java.util.List<String> labels) {
        if (labels == null || labels.size() < 2) return null;
        StringBuilder text = new StringBuilder("Bàn ");
        for (int i = 0; i < labels.size(); i++) {
            String label = labels.get(i);
            if (label == null || !label.startsWith("Bàn ")) return null;
            if (i > 0) text.append(" + ");
            text.append(label.substring(4));
        }
        return text.toString();
    }
}
